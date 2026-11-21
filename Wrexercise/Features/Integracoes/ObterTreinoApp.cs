using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O app pergunta se o treino ao vivo que ele começou continua aberto ou já virou atividade (ADR 0081).
/// Cancelado ou expirado, 404.
/// </summary>
internal static class ObterTreinoApp
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
        var treino = await TreinoAoVivoApp.ObterAsync(dataSource, app.UsuarioId, app.App, externalId, cancellationToken);
        return treino is null
            ? ErrosIntegracao.TreinoNaoEncontrado()
            : Results.Json(RespostasIntegracao.BuildTreino(treino), AppJsonContext.Default.TreinoAppResponse);
    }
}
