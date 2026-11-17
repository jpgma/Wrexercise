using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

internal sealed record CorrigirDuracaoRequest(int? DuracaoSegundos, string? Justificativa);

/// <summary>
/// O proprietário ou um administrador corrige a duração da atividade de outra pessoa (ADR 0068).
/// Só a duração muda: a situação, as reações e os avisos ficam como estavam.
/// </summary>
internal static class CorrigirDuracao
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        CorrigirDuracaoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!ValidacaoAtividade.IsValidDuracao(request.DuracaoSegundos)
            || !CorrecaoDuracao.IsValidJustificativa(request.Justificativa))
        {
            return OperacaoAtividade.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var status = await ExecutarAsync(
            dataSource,
            id,
            usuarioId,
            request.DuracaoSegundos!.Value,
            request.Justificativa!.Trim(),
            cancellationToken);
        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        var atividade = await ConsultasAtividade.GetDetalheAsync(dataSource, id, usuarioId, cancellationToken);
        return atividade is null
            ? OperacaoAtividade.AtividadeNaoEncontrada()
            : Results.Json(atividade, AppJsonContext.Default.AtividadeResponse);
    }

    private static async Task<AtividadeOperacaoStatus> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        int duracaoSegundos,
        string justificativa,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid grupoId;
        Guid autorId;
        string situacao;
        int duracaoAnterior;

        // FOR UPDATE: duas correções ao mesmo tempo esperam uma pela outra, e o histórico fica em ordem.
        await using (var load = new NpgsqlCommand("""
            SELECT grupo_id, usuario_id, situacao_verificacao, duracao_segundos
            FROM atividades
            WHERE id = $1
            FOR UPDATE
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(atividadeId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return AtividadeOperacaoStatus.AtividadeNaoEncontrada;
            }

            grupoId = reader.GetGuid(0);
            autorId = reader.GetGuid(1);
            situacao = reader.GetString(2);
            duracaoAnterior = reader.GetInt32(3);
        }

        var papel = await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken);
        if (papel is null)
        {
            return AtividadeOperacaoStatus.AtividadeNaoEncontrada;
        }

        if (!CorrecaoDuracao.PodeCorrigir(papel))
        {
            return AtividadeOperacaoStatus.AcessoNegado;
        }

        // Como na revisão (ADR 0052): ninguém mexe na própria atividade.
        if (autorId == usuarioId)
        {
            return AtividadeOperacaoStatus.CorrecaoPropria;
        }

        if (!CorrecaoDuracao.SituacaoPermite(situacao))
        {
            return AtividadeOperacaoStatus.CorrecaoNaoPermitida;
        }

        if (duracaoSegundos == duracaoAnterior)
        {
            return AtividadeOperacaoStatus.DadosInvalidos;
        }

        await using (var update = new NpgsqlCommand(
            "UPDATE atividades SET duracao_segundos = $2 WHERE id = $1", connection, transaction))
        {
            update.Parameters.AddWithValue(atividadeId);
            update.Parameters.AddWithValue(duracaoSegundos);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var registro = new NpgsqlCommand("""
            INSERT INTO correcoes_duracao (
                atividade_id, responsavel_id, duracao_anterior_segundos, duracao_nova_segundos, justificativa)
            VALUES ($1, $2, $3, $4, $5)
            """, connection, transaction))
        {
            registro.Parameters.AddWithValue(atividadeId);
            registro.Parameters.AddWithValue(usuarioId);
            registro.Parameters.AddWithValue(duracaoAnterior);
            registro.Parameters.AddWithValue(duracaoSegundos);
            registro.Parameters.AddWithValue(justificativa);
            await registro.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return AtividadeOperacaoStatus.Concluida;
    }
}
