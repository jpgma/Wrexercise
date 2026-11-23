using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O app manda o Percurso do treino ao vivo que ele começou (<c>PUT</c> pelo identificador do treino no app,
/// ADR 0085): o mesmo corpo, as mesmas regras e os mesmos limites do navegador (ADR 0084). Repetir um lote não
/// duplica nada. As coordenadas nunca vão para o log.
/// </summary>
internal static class EnviarPontosPercursoApp
{
    public static async Task<IResult> HandleAsync(
        string externalId,
        EnviarPontosPercursoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!AutorizacaoIntegracao.IsValidExternalId(externalId)
            || request.Pontos is not { Count: > 0 and <= LotePercurso.MaxPontos } lote)
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        var (status, erro, resposta) = await TreinoAoVivoApp.EnviarPontosPercursoAsync(
            dataSource, app.UsuarioId, app.App, externalId, lote, cancellationToken);
        if (status == TreinoAppStatus.NaoEncontrado)
        {
            return ErrosIntegracao.TreinoNaoEncontrado();
        }

        return erro ?? Results.Json(resposta, AppJsonContext.Default.PontosPercursoResponse);
    }
}
