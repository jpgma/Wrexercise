using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal sealed record JogadaCriadaResponse(Guid Id, DateTimeOffset ExpiraEm);

internal static class JogarCarta
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        JogarCartaRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.CartaId is not { } cartaId || request.DestinatarioId is not { } destinatarioId)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var remetenteId = context.GetSessaoAtual().UsuarioId;
        if (destinatarioId == remetenteId)
        {
            return DestinatarioInvalido();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Trava os dois: assim nem o limite de enviadas nem o de recebidas passa do máximo.
        await EstadoJogo.LockUsuariosAsync(connection, transaction, [remetenteId, destinatarioId], cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, remetenteId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, destinatarioId, cancellationToken) is null)
        {
            return DestinatarioInvalido();
        }

        var (remetenteNoJogo, destinatarioNoJogo) =
            await LoadModoJogoAsync(connection, transaction, remetenteId, destinatarioId, cancellationToken);
        if (!remetenteNoJogo)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "MODO_JOGO_NECESSARIO",
                "Ative o modo Jogo para jogar cartas.");
        }

        if (!destinatarioNoJogo)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "DESTINATARIO_FORA_DO_JOGO",
                "Esta pessoa não está no modo Jogo.");
        }

        var estado = await EstadoJogo.LoadAsync(connection, transaction, remetenteId, cancellationToken);
        var comprada = estado.Cartas.FirstOrDefault(c => c.Id == cartaId);
        if (comprada is null || estado.IsGasta(comprada.Id))
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, "CARTA_NAO_ENCONTRADA", "Carta não encontrada.");
        }

        var carta = CatalogoCartas.Find(comprada.Codigo)!;
        if (carta.Tipo != TipoCarta.Provocacao)
        {
            return ErrosJogo.CartaInvalida();
        }

        if (estado.IsEmJogo(comprada.Id))
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "CARTA_EM_JOGO",
                "Esta carta já está numa Jogada aberta.");
        }

        var jogadas = await Jogadas.LoadAsync(connection, transaction, remetenteId, destinatarioId, cancellationToken);
        var agora = await LoadAgoraAsync(connection, transaction, grupoId, cancellationToken);
        if (LimitesJogada.Verificar(remetenteId, destinatarioId, jogadas.Select(j => j.ToLimite()).ToList(), agora.Utc)
            is { } limite)
        {
            return ErrosJogo.Limite(limite);
        }

        int? duracaoAlvo = null;
        if (CatalogoCartas.IsDuracao(carta.Codigo))
        {
            var desafio = await DesafioDestaqueGrupo.LoadAsync(connection, grupoId, cancellationToken);
            if (desafio is null || desafio.Situacao != SituacaoDesafio.Ativo)
            {
                return ApiProblem.Create(
                    StatusCodes.Status409Conflict,
                    "SEM_DESAFIO_ATIVO",
                    "Esta carta só pode ser jogada com um desafio ativo no grupo.");
            }

            duracaoAlvo = desafio.DuracaoMinimaSegundos + (CatalogoCartas.GetMinutosExtras(carta.Codigo) * 60);
        }

        var tipoEvitar = carta.Codigo == CodigoCarta.Variedade
            ? await LoadUltimoTipoVerificadoAsync(connection, transaction, grupoId, destinatarioId, cancellationToken)
            : null;

        var expiraLocal = PrazoJogada.Calcular(carta.Codigo, agora.Local);

        Guid jogadaId;
        DateTimeOffset expiraEm;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO jogadas (
                grupo_id, remetente_id, destinatario_id, carta_id, enviada_em, expira_em,
                duracao_alvo_segundos, tipo_evitar)
            SELECT g.id, $2, $3, $4, $5, $6 AT TIME ZONE g.fuso_horario, $7, $8
            FROM grupos g
            WHERE g.id = $1
            RETURNING id, expira_em
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(grupoId);
            insert.Parameters.AddWithValue(remetenteId);
            insert.Parameters.AddWithValue(destinatarioId);
            insert.Parameters.AddWithValue(cartaId);
            insert.Parameters.AddWithValue(agora.Utc);
            insert.Parameters.AddWithValue(DateTime.SpecifyKind(expiraLocal, DateTimeKind.Unspecified));
            insert.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)duracaoAlvo ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)tipoEvitar ?? DBNull.Value);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            jogadaId = reader.GetGuid(0);
            expiraEm = TimestampLeitura.FromReader(reader, 1);
        }

        await NotificacoesJogada.NotificarAsync(
            connection,
            transaction,
            jogadaId,
            destinatarioId,
            remetenteId,
            TipoNotificacaoJogada.CartaRecebida,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            new JogadaCriadaResponse(jogadaId, expiraEm),
            AppJsonContext.Default.JogadaCriadaResponse,
            statusCode: StatusCodes.Status201Created);
    }

    private static IResult DestinatarioInvalido() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DESTINATARIO_INVALIDO",
        "Escolha outro membro do grupo para receber a carta.");

    private static async Task<(bool Remetente, bool Destinatario)> LoadModoJogoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid remetenteId,
        Guid destinatarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT
                bool_or(id = $1 AND modo_inicio = 'Jogo'),
                bool_or(id = $2 AND modo_inicio = 'Jogo')
            FROM usuarios
            WHERE id IN ($1, $2)
            """, connection, transaction);
        command.Parameters.AddWithValue(remetenteId);
        command.Parameters.AddWithValue(destinatarioId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetBoolean(0), reader.GetBoolean(1));
    }

    /// <summary>O instante atual, também no horário local do grupo, de onde sai o prazo.</summary>
    private static async Task<(DateTimeOffset Utc, DateTime Local)> LoadAgoraAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT now(), now() AT TIME ZONE fuso_horario FROM grupos WHERE id = $1",
            connection,
            transaction);
        command.Parameters.AddWithValue(grupoId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (TimestampLeitura.FromReader(reader, 0), reader.GetDateTime(1));
    }

    /// <summary>O tipo da última atividade verificada do destinatário neste grupo, fixado no envio da VARIEDADE.</summary>
    private static async Task<string?> LoadUltimoTipoVerificadoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        Guid destinatarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT tipo
            FROM atividades
            WHERE grupo_id = $1 AND usuario_id = $2 AND situacao_verificacao = 'Verificada'
            ORDER BY started_at DESC, created_at DESC, id
            LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue(grupoId);
        command.Parameters.AddWithValue(destinatarioId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }
}
