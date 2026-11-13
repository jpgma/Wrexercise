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

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var (status, atividadeId) = await ExecutarAsync(
            dataSource,
            grupoId,
            usuarioId,
            request,
            cancellationToken);

        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        var atividade = await ConsultasAtividade.GetDetalheAsync(dataSource, atividadeId, usuarioId, cancellationToken);
        return atividade is null
            ? OperacaoAtividade.AtividadeNaoEncontrada()
            : Results.Json(atividade, AppJsonContext.Default.AtividadeResponse, statusCode: StatusCodes.Status201Created);
    }

    private static bool IsValidRequest(CriarAtividadeRequest request) =>
        TipoAtividade.IsValid(request.Tipo)
        && TipoComprovacao.IsValid(request.TipoComprovacao)
        && ValidacaoAtividade.IsValidDuracao(request.DuracaoSegundos)
        && ValidacaoAtividade.IsValidDescricao(request.Descricao)
        && ValidacaoAtividade.IsValidStartedAt(request.StartedAt);

    private static async Task<(AtividadeOperacaoStatus Status, Guid Id)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid grupoId,
        Guid usuarioId,
        CriarAtividadeRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return (AtividadeOperacaoStatus.GrupoNaoEncontrado, default);
        }

        // Quem ainda deve avaliações ao grupo não registra a sua: assim ninguém fica esperando 48 horas.
        if (await ConsultasAtividade.ExistemParaAvaliarAsync(connection, transaction, grupoId, usuarioId, cancellationToken))
        {
            return (AtividadeOperacaoStatus.AvaliacoesPendentes, default);
        }

        await using var insert = new NpgsqlCommand("""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, descricao, tipo_comprovacao)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
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

        Guid id;
        DateTimeOffset submittedAt;
        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
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
        return (AtividadeOperacaoStatus.Concluida, id);
    }
}
