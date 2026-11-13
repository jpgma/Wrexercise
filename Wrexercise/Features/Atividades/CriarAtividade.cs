using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

internal static class CriarAtividade
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        CriarAtividadeRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request))
        {
            return OperacaoAtividade.InvalidData();
        }

        if (!ValidacaoAtividade.IsStartedAtDentroDoLimite(request.StartedAt!.Value))
        {
            return OperacaoAtividade.ToResult(AtividadeOperacaoStatus.AtividadeMuitoAntiga);
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey))
        {
            return OperacaoAtividade.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var (status, atividadeId, repetida) = await ExecutarAsync(
            dataSource,
            grupoId,
            usuarioId,
            request,
            idempotencyKey,
            cancellationToken);

        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        // A repetição devolve a mesma atividade com 200: o cliente segue para as evidências como da primeira vez.
        var atividade = await ConsultasAtividade.GetDetalheAsync(dataSource, atividadeId, usuarioId, cancellationToken);
        return atividade is null
            ? OperacaoAtividade.AtividadeNaoEncontrada()
            : Results.Json(
                atividade,
                AppJsonContext.Default.AtividadeResponse,
                statusCode: repetida ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    /// <summary>
    /// Cabeçalho opcional <c>Idempotency-Key</c> (ADR 0053): um UUID gerado pelo navegador para cada registro.
    /// Ausente, a criação segue como antes. Presente e inválido, a requisição é recusada.
    /// </summary>
    private static bool TryGetIdempotencyKey(HttpContext context, out Guid? chave)
    {
        chave = null;
        var valor = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrEmpty(valor))
        {
            return true;
        }

        if (!Guid.TryParse(valor, out var lida))
        {
            return false;
        }

        chave = lida;
        return true;
    }

    private static bool IsValidRequest(CriarAtividadeRequest request) =>
        TipoAtividade.IsValid(request.Tipo)
        && TipoComprovacao.IsValid(request.TipoComprovacao)
        && ValidacaoAtividade.IsValidDuracao(request.DuracaoSegundos)
        && ValidacaoAtividade.IsValidDescricao(request.Descricao)
        && ValidacaoAtividade.IsValidStartedAt(request.StartedAt);

    private static async Task<(AtividadeOperacaoStatus Status, Guid Id, bool Repetida)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid grupoId,
        Guid usuarioId,
        CriarAtividadeRequest request,
        Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return (AtividadeOperacaoStatus.GrupoNaoEncontrado, default, false);
        }

        // Antes da fila: uma atividade nova de outra pessoa não pode transformar a repetição num 409.
        if (idempotencyKey is { } chave
            && await BuscarPorChaveAsync(connection, transaction, usuarioId, chave, cancellationToken) is { } existente)
        {
            return ResultadoRepeticao(existente, grupoId);
        }

        // Quem ainda deve avaliações ao grupo não registra a sua: assim ninguém fica esperando 48 horas.
        if (await ConsultasAtividade.ExistemParaAvaliarAsync(connection, transaction, grupoId, usuarioId, cancellationToken))
        {
            return (AtividadeOperacaoStatus.AvaliacoesPendentes, default, false);
        }

        // Duas requisições com a mesma chave ao mesmo tempo: a segunda espera a primeira e não insere nada.
        await using var insert = new NpgsqlCommand("""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, descricao, tipo_comprovacao, idempotency_key)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (usuario_id, idempotency_key) WHERE idempotency_key IS NOT NULL DO NOTHING
            RETURNING id, submitted_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(grupoId);
        insert.Parameters.AddWithValue(usuarioId);
        insert.Parameters.AddWithValue(request.Tipo!);
        insert.Parameters.AddWithValue(request.DuracaoSegundos!.Value);
        insert.Parameters.AddWithValue(request.StartedAt!.Value);
        insert.Parameters.Add(new NpgsqlParameter
        {
            Value = (object?)ValidacaoAtividade.NormalizeDescricao(request.Descricao) ?? DBNull.Value,
        });
        insert.Parameters.AddWithValue(request.TipoComprovacao!);
        insert.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)idempotencyKey ?? DBNull.Value);

        Guid id;
        DateTimeOffset submittedAt;
        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                // Só acontece com chave: outra requisição com ela acabou de criar a atividade.
                await reader.DisposeAsync();
                var criada = await BuscarPorChaveAsync(
                    connection, transaction, usuarioId, idempotencyKey!.Value, cancellationToken);
                return ResultadoRepeticao(criada!.Value, grupoId);
            }

            id = reader.GetGuid(0);
            submittedAt = reader.GetFieldValue<DateTimeOffset>(1);
        }

        if (AvisosEvidencia.IsRegistroTardio(request.StartedAt.Value, request.DuracaoSegundos.Value, submittedAt))
        {
            await ConsultasAtividade.InserirAvisoAsync(
                connection, transaction, id, TipoAviso.RegistroTardio, null, null, cancellationToken);
        }

        await using (var revisores = new NpgsqlCommand("""
            INSERT INTO revisores_atividade (atividade_id, usuario_id)
            SELECT $1, m.usuario_id
            FROM membros_grupo m
            WHERE m.grupo_id = $2 AND m.removed_at IS NULL AND m.usuario_id <> $3
            """, connection, transaction))
        {
            revisores.Parameters.AddWithValue(id);
            revisores.Parameters.AddWithValue(grupoId);
            revisores.Parameters.AddWithValue(usuarioId);
            await revisores.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var verificar = new NpgsqlCommand("""
            UPDATE atividades
            SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE id = $1
              AND tipo_comprovacao = 'RegistroManual'
              AND NOT EXISTS (SELECT 1 FROM revisores_atividade r WHERE r.atividade_id = $1)
            """, connection, transaction))
        {
            verificar.Parameters.AddWithValue(id);
            if (await verificar.ExecuteNonQueryAsync(cancellationToken) > 0)
            {
                await JogadasConcluidas.NotificarAsync(connection, transaction, id, cancellationToken);
            }
        }

        await NotificacoesAtividade.NotificarNovaAtividadeAsync(connection, transaction, id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return (AtividadeOperacaoStatus.Concluida, id, false);
    }

    /// <summary>A chave vale para quem registra; em outro grupo ela não devolve a atividade (ADR 0053).</summary>
    private static (AtividadeOperacaoStatus Status, Guid Id, bool Repetida) ResultadoRepeticao(
        (Guid Id, Guid GrupoId) existente,
        Guid grupoId) =>
        existente.GrupoId == grupoId
            ? (AtividadeOperacaoStatus.Concluida, existente.Id, true)
            : (AtividadeOperacaoStatus.ChaveIdempotenciaReutilizada, default, false);

    private static async Task<(Guid Id, Guid GrupoId)?> BuscarPorChaveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        Guid chave,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, grupo_id FROM atividades WHERE usuario_id = $1 AND idempotency_key = $2
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(chave);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetGuid(0), reader.GetGuid(1)) : null;
    }
}
