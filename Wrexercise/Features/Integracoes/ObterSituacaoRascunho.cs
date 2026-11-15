using Npgsql;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>O app pergunta se o rascunho de um treino continua pendente, já virou atividade ou venceu.</summary>
internal static class ObterSituacaoRascunho
{
    public static async Task<IResult> HandleAsync(
        string externalId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!AutorizacaoIntegracao.IsValidExternalId(externalId))
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        await using var command = dataSource.CreateCommand("""
            SELECT id, atividade_id, expires_at
            FROM rascunhos_atividade
            WHERE usuario_id = $1 AND app = $2 AND external_id = $3
            """);
        command.Parameters.AddWithValue(app.UsuarioId);
        command.Parameters.AddWithValue(app.App);
        command.Parameters.AddWithValue(externalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ErrosIntegracao.RascunhoNaoEncontrado();
        }

        var resposta = RespostasIntegracao.BuildSituacao(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            DateTimeOffset.UtcNow);
        return Results.Json(resposta, AppJsonContext.Default.RascunhoSituacaoResponse);
    }
}
