using System.Globalization;
using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O treino acabou no app (ADR 0081): multipart com <c>duracaoSegundos</c> (o tempo ativo, sem pausas),
/// <c>endedAt</c> e, opcionalmente, a imagem do resumo (<c>arquivo</c>, só foto) com a <c>miniatura</c> JPEG.
/// O treino continua aberto: a pessoa termina aqui, com a duração sugerida. Repetir não duplica a imagem.
/// </summary>
internal static class InformarFimTreinoApp
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
        if (!int.TryParse(form["duracaoSegundos"], NumberStyles.None, CultureInfo.InvariantCulture, out var duracao)
            || duracao is < 1 or > 24 * 60 * 60
            || !DateTimeOffset.TryParse(
                form["endedAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endedAt))
        {
            return ErrosIntegracao.InvalidData();
        }

        var arquivo = form.Files.GetFile("arquivo");
        var miniatura = form.Files.GetFile("miniatura");
        var temArquivo = arquivo is { Length: > 0 };
        if (temArquivo != miniatura is { Length: > 0 })
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        var salvos = new List<string>();
        try
        {
            ArquivoIntegracao? resumo = null;
            if (temArquivo)
            {
                EvidenciaSalva imagem;
                await using (var stream = arquivo!.OpenReadStream())
                {
                    imagem = await armazenamento.SalvarAsync(
                        stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytes, cancellationToken);
                }

                salvos.Add(imagem.CaminhoRelativo);

                EvidenciaSalva salvaMiniatura;
                await using (var stream = miniatura!.OpenReadStream())
                {
                    salvaMiniatura = await armazenamento.SalvarAsync(
                        stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytesMiniatura, cancellationToken);
                }

                salvos.Add(salvaMiniatura.CaminhoRelativo);
                if (salvaMiniatura.TipoMime != ValidacaoMidiaEvidencia.TipoMimeMiniatura)
                {
                    throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
                }

                resumo = new ArquivoIntegracao(
                    imagem.CaminhoRelativo,
                    imagem.TipoMime,
                    imagem.TamanhoBytes,
                    imagem.Sha256,
                    salvaMiniatura.CaminhoRelativo);
            }

            var (status, treino, usado) = await TreinoAoVivoApp.RegistrarFimAsync(
                dataSource, app.UsuarioId, app.App, externalId, duracao, endedAt, resumo, cancellationToken);

            // A imagem que não entrou (repetição ou limite de evidências) não fica no disco.
            if (!usado)
            {
                TreinoAoVivoApp.ExcluirArquivos(armazenamento, salvos);
            }

            return status switch
            {
                TreinoAppStatus.NaoEncontrado => ErrosIntegracao.TreinoNaoEncontrado(),
                TreinoAppStatus.NaoEmAndamento => ErrosIntegracao.TreinoNaoEmAndamento(),
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
    }
}
