using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Jogo;

internal static class ResponderJogada
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        ResponderJogadaRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request))
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await LoadRemetenteAsync(connection, transaction, id, usuarioId, cancellationToken) is not { } remetenteId)
        {
            return ErrosJogo.JogadaNaoEncontrada();
        }

        // Trava as duas pessoas antes da Jogada, na mesma ordem do envio: a Resposta sai da Mão de quem
        // responde, e o DESAFIO DUPLO cria uma Jogada para quem enviou.
        await EstadoJogo.LockUsuariosAsync(connection, transaction, [usuarioId, remetenteId], cancellationToken);

        var jogada = await LoadJogadaAsync(connection, transaction, id, cancellationToken);
        if (await GrupoAcesso.GetPapelAsync(connection, transaction, jogada.GrupoId, usuarioId, cancellationToken) is null)
        {
            return ErrosJogo.JogadaNaoEncontrada();
        }

        if (!jogada.Aberta)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "JOGADA_ENCERRADA",
                "Esta carta já foi respondida ou expirou.");
        }

        var mudanca = new MudancaJogada(null, null, null);
        if (request.CartaRespostaId is { } cartaRespostaId)
        {
            var estado = await EstadoJogo.LoadAsync(connection, transaction, usuarioId, cancellationToken);
            var comprada = estado.Cartas.FirstOrDefault(c => c.Id == cartaRespostaId);
            if (comprada is null || estado.IsGasta(comprada.Id))
            {
                return ErrosJogo.CartaNaoEncontrada();
            }

            if (CatalogoCartas.Find(comprada.Codigo)!.Tipo != TipoCarta.Resposta
                || (request.Objetivo is not null && comprada.Codigo != CodigoCarta.MudancaDePlanos))
            {
                return ErrosJogo.CartaInvalida();
            }

            (MudancaJogada? Mudanca, IResult? Erro) efeito = comprada.Codigo switch
            {
                CodigoCarta.DesafioDuplo =>
                    await DevolverAsync(connection, transaction, jogada, usuarioId, remetenteId, cancellationToken),
                CodigoCarta.PrazoExtra => (mudanca with { ExpiraLocal = jogada.ExpiraLocal + PrazoJogada.Extra }, null),
                CodigoCarta.MudancaDePlanos =>
                    await TrocarObjetivoAsync(connection, transaction, jogada, request.Objetivo, usuarioId, cancellationToken),
                // APOSTA: basta registrar a carta; a recompensa dobra na leitura.
                _ => (mudanca, null),
            };

            if (efeito.Erro is not null)
            {
                return efeito.Erro;
            }

            mudanca = efeito.Mudanca!;
        }

        await UpdateAsync(connection, transaction, id, request, mudanca, cancellationToken);

        await NotificacoesJogada.NotificarAsync(
            connection,
            transaction,
            id,
            remetenteId,
            usuarioId,
            request.Resposta == SituacaoJogada.RespostaAceita
                ? TipoNotificacaoJogada.CartaAceita
                : TipoNotificacaoJogada.CartaRecusada,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Recusar é sempre de graça: sem carta. O objetivo novo só vem com uma carta.</summary>
    private static bool IsValidRequest(ResponderJogadaRequest request) => request.Resposta switch
    {
        SituacaoJogada.RespostaAceita => request.Objetivo is null || request.CartaRespostaId is not null,
        SituacaoJogada.RespostaRecusada => request.CartaRespostaId is null && request.Objetivo is null,
        _ => false,
    };

    /// <summary>
    /// DESAFIO DUPLO: devolve o mesmo objetivo a quem enviou, numa Jogada nova com a Provocação original.
    /// Não confere os limites de envio, porque é uma resposta (decisão 0036).
    /// </summary>
    private static async Task<(MudancaJogada? Mudanca, IResult? Erro)> DevolverAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JogadaAberta jogada,
        Guid usuarioId,
        Guid remetenteId,
        CancellationToken cancellationToken)
    {
        if (jogada.OrigemId is not null)
        {
            return (null, ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "DESAFIO_DUPLO_INVALIDO",
                "Uma carta devolvida pelo DESAFIO DUPLO não pode ser devolvida de novo."));
        }

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, jogada.GrupoId, remetenteId, cancellationToken) is null)
        {
            return (null, ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "REMETENTE_FORA_DO_GRUPO",
                "Quem enviou a carta não está mais no grupo."));
        }

        var (_, remetenteNoJogo) =
            await Jogadas.LoadModoJogoAsync(connection, transaction, usuarioId, remetenteId, cancellationToken);
        if (!remetenteNoJogo)
        {
            return (null, ErrosJogo.DestinatarioForaDoJogo());
        }

        // A duração-alvo é copiada; o tipo a evitar da VARIEDADE é o da última atividade de quem vai cumprir.
        ParametrosObjetivo parametros;
        if (CatalogoCartas.IsDuracao(jogada.Codigo))
        {
            parametros = new ParametrosObjetivo(jogada.DuracaoAlvoSegundos, null);
        }
        else
        {
            var (fixados, erro) = await Jogadas.FixarObjetivoAsync(
                connection, transaction, jogada.Codigo, jogada.GrupoId, remetenteId, cancellationToken);
            if (erro is not null)
            {
                return (null, erro);
            }

            parametros = fixados!;
        }

        var agora = await Jogadas.LoadAgoraAsync(connection, transaction, jogada.GrupoId, cancellationToken);
        var (devolvidaId, _) = await Jogadas.InsertAsync(
            connection,
            transaction,
            new NovaJogada(jogada.GrupoId, usuarioId, remetenteId, jogada.CartaId, jogada.Id, agora.Utc,
                PrazoJogada.Calcular(jogada.Codigo, agora.Local), parametros),
            cancellationToken);

        await NotificacoesJogada.NotificarAsync(
            connection,
            transaction,
            devolvidaId,
            remetenteId,
            usuarioId,
            TipoNotificacaoJogada.CartaRecebida,
            cancellationToken);

        return (new MudancaJogada(null, null, null), null);
    }

    /// <summary>
    /// MUDANÇA DE PLANOS: troca o objetivo por um da mesma faixa, fixado para quem vai cumprir. O prazo nunca
    /// encurta (decisão 0036).
    /// </summary>
    private static async Task<(MudancaJogada? Mudanca, IResult? Erro)> TrocarObjetivoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JogadaAberta jogada,
        string? objetivo,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (objetivo is null || !CatalogoCartas.GetEquivalentes(jogada.Codigo).Contains(objetivo))
        {
            return (null, ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "OBJETIVO_INVALIDO",
                "Escolha um objetivo equivalente ao da carta."));
        }

        var (parametros, erro) = await Jogadas.FixarObjetivoAsync(
            connection, transaction, objetivo, jogada.GrupoId, usuarioId, cancellationToken);
        if (erro is not null)
        {
            return (null, erro);
        }

        return (new MudancaJogada(
            PrazoJogada.ComMudanca(objetivo, jogada.EnviadaLocal, jogada.ExpiraLocal),
            objetivo,
            parametros), null);
    }

    private static async Task<Guid?> LoadRemetenteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT remetente_id FROM jogadas WHERE id = $1 AND destinatario_id = $2",
            connection,
            transaction);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(usuarioId);
        return (Guid?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<JogadaAberta> LoadJogadaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT j.grupo_id, j.resposta IS NULL AND j.expira_em > now(), j.origem_id, j.carta_id,
                {Jogadas.ObjetivoSql}, j.duracao_alvo_segundos,
                j.enviada_em AT TIME ZONE g.fuso_horario, j.expira_em AT TIME ZONE g.fuso_horario
            FROM jogadas j
            JOIN cartas c ON c.id = j.carta_id
            JOIN grupos g ON g.id = j.grupo_id
            WHERE j.id = $1
            FOR UPDATE OF j
            """, connection, transaction);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new JogadaAberta(
            id,
            reader.GetGuid(0),
            reader.GetBoolean(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.GetDateTime(6),
            reader.GetDateTime(7));
    }

    private static async Task UpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        ResponderJogadaRequest request,
        MudancaJogada mudanca,
        CancellationToken cancellationToken)
    {
        // Só troca o que a Resposta mudou: o prazo convertido de volta do horário local poderia andar numa
        // mudança de horário de verão.
        await using var update = new NpgsqlCommand("""
            UPDATE jogadas j
            SET resposta = $2,
                respondida_em = now(),
                carta_resposta_id = $3,
                expira_em = COALESCE($4 AT TIME ZONE g.fuso_horario, j.expira_em),
                codigo_objetivo = COALESCE($5, j.codigo_objetivo),
                duracao_alvo_segundos = CASE WHEN $5 IS NULL THEN j.duracao_alvo_segundos ELSE $6 END,
                tipo_evitar = CASE WHEN $5 IS NULL THEN j.tipo_evitar ELSE $7 END
            FROM grupos g
            WHERE j.id = $1 AND g.id = j.grupo_id
            """, connection, transaction);
        update.Parameters.AddWithValue(id);
        update.Parameters.AddWithValue(request.Resposta!);
        update.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)request.CartaRespostaId ?? DBNull.Value);
        update.Parameters.AddWithValue(
            NpgsqlDbType.Timestamp,
            mudanca.ExpiraLocal is { } expira ? DateTime.SpecifyKind(expira, DateTimeKind.Unspecified) : (object)DBNull.Value);
        update.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)mudanca.Objetivo ?? DBNull.Value);
        update.Parameters.AddWithValue(
            NpgsqlDbType.Integer, (object?)mudanca.Parametros?.DuracaoAlvoSegundos ?? DBNull.Value);
        update.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)mudanca.Parametros?.TipoEvitar ?? DBNull.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <param name="Codigo">O objetivo em vigor.</param>
    private sealed record JogadaAberta(
        Guid Id,
        Guid GrupoId,
        bool Aberta,
        Guid? OrigemId,
        Guid CartaId,
        string Codigo,
        int? DuracaoAlvoSegundos,
        DateTime EnviadaLocal,
        DateTime ExpiraLocal);

    /// <summary>O que a Resposta muda na Jogada. Nulo: fica como está.</summary>
    private sealed record MudancaJogada(DateTime? ExpiraLocal, string? Objetivo, ParametrosObjetivo? Parametros);
}
