using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Reacoes;

internal static class RevisarAtividade
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        RevisaoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request))
        {
            return OperacaoReacao.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var decisao = request.Decisao!;
        var justificativa = ValidacaoRevisao.NormalizeJustificativa(request.Justificativa!);

        var resultado = await ExecutarAsync(dataSource, id, usuarioId, decisao, justificativa, cancellationToken);
        return resultado.Erro ?? Results.Json(resultado.Valor!, AppJsonContext.Default.RevisaoRegistradaResponse);
    }

    private static bool IsValidRequest(RevisaoRequest request) =>
        request.Decisao is SituacaoVerificacao.Verificada or SituacaoVerificacao.Rejeitada
        && ValidacaoRevisao.IsValidJustificativa(request.Justificativa);

    private static async Task<(IResult? Erro, RevisaoRegistradaResponse? Valor)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        string decisao,
        string justificativa,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid grupoId;
        string situacao;

        await using (var load = new NpgsqlCommand("""
            SELECT grupo_id, situacao_verificacao
            FROM atividades
            WHERE id = $1
            FOR UPDATE
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(atividadeId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada), null);
            }

            grupoId = reader.GetGuid(0);
            situacao = reader.GetString(1);
        }

        var papel = await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken);
        if (papel is null)
        {
            return (OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada), null);
        }

        if (papel is not (PapelMembro.Proprietario or PapelMembro.Administrador))
        {
            return (OperacaoReacao.ToResult(ReacaoOperacaoStatus.SemPermissaoRevisao), null);
        }

        if (situacao != SituacaoVerificacao.EmRevisao)
        {
            return (OperacaoReacao.ToResult(ReacaoOperacaoStatus.RevisaoNaoPendente), null);
        }

        await using (var update = new NpgsqlCommand(
            """
            UPDATE atividades
            SET situacao_verificacao = $2,
                verified_at = CASE WHEN $2 = 'Verificada' THEN coalesce(verified_at, now()) ELSE verified_at END
            WHERE id = $1
            """,
            connection,
            transaction))
        {
            update.Parameters.AddWithValue(atividadeId);
            update.Parameters.AddWithValue(decisao);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        DateTimeOffset registradaEm;
        await using (var historico = new NpgsqlCommand("""
            INSERT INTO historico_revisao (atividade_id, responsavel_id, decisao, justificativa)
            VALUES ($1, $2, $3, $4)
            RETURNING created_at
            """, connection, transaction))
        {
            historico.Parameters.AddWithValue(atividadeId);
            historico.Parameters.AddWithValue(usuarioId);
            historico.Parameters.AddWithValue(decisao);
            historico.Parameters.AddWithValue(justificativa);
            registradaEm = TimestampLeitura.FromScalar(
                (await historico.ExecuteScalarAsync(cancellationToken))!);
        }

        await NotificacoesAtividade.NotificarSituacaoAsync(
            connection,
            transaction,
            atividadeId,
            decisao,
            cancellationToken);

        if (decisao == SituacaoVerificacao.Verificada)
        {
            await JogadasConcluidas.NotificarAsync(connection, transaction, atividadeId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (null, new RevisaoRegistradaResponse(decisao, decisao, registradaEm));
    }
}
