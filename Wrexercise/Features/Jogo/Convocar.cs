using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>O que impede convocar agora; a prévia mostra, o envio devolve como erro.</summary>
internal sealed record BloqueioConvocacao(int Status, string Codigo, string Mensagem)
{
    public IResult ToResult() => ApiProblem.Create(Status, Codigo, Mensagem);
}

/// <summary>
/// Joga uma carta Global (decisão 0091): cria a Convocação do grupo e convida, num retrato fixo, o anfitrião e os
/// membros no modo Jogo. A carta já foi paga na compra: jogá-la só a gasta.
/// </summary>
internal static class Convocar
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        ConvocarRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.CartaId is not { } cartaId
            || request.Tipo is not { } tipo
            || !SituacaoConvocacao.Tipos.Contains(tipo))
        {
            return ErrosJogo.DadosInvalidos();
        }

        var anfitriaoId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // O grupo primeiro (a ordem do resto do app), depois a pessoa: duas Convocações no mesmo grupo, ou a mesma
        // carta duas vezes, esperam uma pela outra e a segunda vê a primeira.
        await GrupoAcesso.LockGrupoAsync(connection, transaction, grupoId, cancellationToken);
        await EstadoJogo.LockUsuariosAsync(connection, transaction, [anfitriaoId], cancellationToken);

        var (_, erro, bloqueio) = await ValidarAsync(connection, transaction, grupoId, anfitriaoId, cartaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (bloqueio is not null)
        {
            return bloqueio.ToResult();
        }

        Guid convocacaoId;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO convocacoes (grupo_id, anfitriao_id, carta_id, tipo_atividade, created_at, expira_em)
            VALUES ($1, $2, $3, $4, now(), now() + $5)
            RETURNING id
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(grupoId);
            insert.Parameters.AddWithValue(anfitriaoId);
            insert.Parameters.AddWithValue(cartaId);
            insert.Parameters.AddWithValue(tipo);
            insert.Parameters.AddWithValue(SituacaoConvocacao.Duracao);
            convocacaoId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        // O retrato: quem entrar no grupo ou no modo Jogo depois fica de fora. O anfitrião já participa.
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO participantes_convocacao (convocacao_id, usuario_id, decisao, decidida_em)
            SELECT $1, m.usuario_id,
                CASE WHEN m.usuario_id = $3 THEN 'Aceita' END,
                CASE WHEN m.usuario_id = $3 THEN now() END
            FROM membros_grupo m
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $2 AND m.removed_at IS NULL AND (u.modo_inicio = 'Jogo' OR m.usuario_id = $3)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(convocacaoId);
            insert.Parameters.AddWithValue(grupoId);
            insert.Parameters.AddWithValue(anfitriaoId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await NotificacoesConvocacao.NotificarAsync(connection, transaction, convocacaoId, cancellationToken);

        var convocacao = (await Convocacoes.LoadAsync(connection, transaction, grupoId, convocacaoId, cancellationToken))!;
        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            Convocacoes.ToResponse(convocacao, anfitriaoId),
            AppJsonContext.Default.ConvocacaoResponse,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Confere quem convoca e a carta. O erro é para o que a prévia também recusa (grupo, carta); o bloqueio é o
    /// que a prévia mostra como motivo: modo Jogo, FADIGA, Convocação ativa e ninguém para convidar.
    /// </summary>
    /// <returns>Quantos colegas seriam convidados, sem o anfitrião.</returns>
    public static async Task<(int Convidados, IResult? Erro, BloqueioConvocacao? Bloqueio)> ValidarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        Guid anfitriaoId,
        Guid cartaId,
        CancellationToken cancellationToken)
    {
        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, anfitriaoId, cancellationToken) is null)
        {
            return (0, GrupoAcesso.GrupoNaoEncontrado(), null);
        }

        var estado = await EstadoJogo.LoadAsync(connection, transaction, anfitriaoId, cancellationToken);
        var comprada = estado.Cartas.FirstOrDefault(c => c.Id == cartaId);
        if (comprada is null || estado.IsGasta(comprada.Id))
        {
            return (0, ErrosJogo.CartaNaoEncontrada(), null);
        }

        if (CatalogoCartas.Find(comprada.Codigo)?.Tipo != TipoCarta.Global)
        {
            return (0, ErrosJogo.CartaInvalida(), null);
        }

        var (anfitriaoNoJogo, _) =
            await Jogadas.LoadModoJogoAsync(connection, transaction, anfitriaoId, anfitriaoId, cancellationToken);
        var convidados = await CountConvidadosAsync(connection, transaction, grupoId, anfitriaoId, cancellationToken);

        BloqueioConvocacao? bloqueio = null;
        if (!anfitriaoNoJogo)
        {
            bloqueio = new(StatusCodes.Status409Conflict, "MODO_JOGO_NECESSARIO", "Ative o modo Jogo para jogar cartas.");
        }
        else if (estado.FadigaRestante > 0)
        {
            bloqueio = new(
                StatusCodes.Status409Conflict,
                "FADIGA_ATIVA",
                estado.FadigaRestante == 1
                    ? "Quem não aguenta, não provoca: falta 1 treino pra recarregar."
                    : $"Quem não aguenta, não provoca: faltam {estado.FadigaRestante} treinos pra recarregar.");
        }
        else if (await Convocacoes.ExistsAtivaAsync(connection, transaction, grupoId, cancellationToken))
        {
            bloqueio = new(
                StatusCodes.Status409Conflict,
                "CONVOCACAO_ATIVA",
                "Já tem uma Convocação rolando neste grupo. Espere ela acabar.");
        }
        else if (convidados == 0)
        {
            bloqueio = new(
                StatusCodes.Status409Conflict,
                "CONVOCACAO_SEM_CONVIDADOS",
                "Ninguém mais da galera está no modo Jogo.");
        }

        return (convidados, null, bloqueio);
    }

    private static async Task<int> CountConvidadosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        Guid anfitriaoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT count(*)::int
            FROM membros_grupo m
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $1 AND m.removed_at IS NULL AND u.modo_inicio = 'Jogo' AND m.usuario_id <> $2
            """, connection, transaction);
        command.Parameters.AddWithValue(grupoId);
        command.Parameters.AddWithValue(anfitriaoId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
