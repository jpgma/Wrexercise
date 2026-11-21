using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O resumo que não chegou no fim (ADR 0083): multipart com a imagem do resumo (<c>arquivo</c>, só foto) e a
/// <c>miniatura</c> JPEG, depois que a pessoa terminou o treino aqui. Entra uma vez só, com o selo
/// "Adicionada depois". Enquanto o treino está aberto, o resumo vai pelo fim.
/// </summary>
internal static class AnexarResumoTreinoApp
{
    public static async Task<IResult> HandleAsync(
        string externalId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        if (!AutorizacaoIntegracao.IsValidExternalId(externalId) || !context.Request.HasFormContentType)
        {
            return ErrosIntegracao.InvalidData();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        if (form.Files.GetFile("arquivo") is not { Length: > 0 } arquivo
            || form.Files.GetFile("miniatura") is not { Length: > 0 } miniatura)
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        var salvos = new List<string>();
        try
        {
            var resumo = await ResumoApp.SalvarAsync(arquivo, miniatura, armazenamento, salvos, cancellationToken);
            var (status, treino, usado) = await TreinoAoVivoApp.AnexarResumoDepoisAsync(
                dataSource, app.UsuarioId, app.App, externalId, resumo, cancellationToken);

            // A imagem que não entrou (repetição ou recusa) não fica no disco.
            if (!usado)
            {
                TreinoAoVivoApp.ExcluirArquivos(armazenamento, salvos);
            }

            return status switch
            {
                TreinoAppStatus.NaoEncontrado => ErrosIntegracao.TreinoNaoEncontrado(),
                TreinoAppStatus.EmAndamento => ErrosIntegracao.TreinoEmAndamento(),
                TreinoAppStatus.Encerrado => ErrosIntegracao.AtividadeEncerrada(),
                TreinoAppStatus.LimiteEvidencias => ErrosIntegracao.LimiteEvidencias(),
                _ when treino is null => ErrosIntegracao.TreinoNaoEncontrado(),
                _ => Results.Json(RespostasIntegracao.BuildTreino(treino), AppJsonContext.Default.TreinoAppResponse),
            };
        }
        catch (EvidenciaInvalidaException)
        {
            TreinoAoVivoApp.ExcluirArquivos(armazenamento, salvos);
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "EVIDENCIA_INVALIDA",
                "Verifique o arquivo enviado.");
        }
        catch
        {
            TreinoAoVivoApp.ExcluirArquivos(armazenamento, salvos);
            throw;
        }
    }
}
