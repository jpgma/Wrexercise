using Microsoft.AspNetCore.Http.Features;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>Permite corpo maior só nas rotas de upload: até 25 MB de vídeo (ou 5 MB de foto) mais a miniatura, mantendo 1 MB no restante da API.</summary>
internal sealed class LimiteUploadFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is not null && !feature.IsReadOnly)
        {
            // Folga para os cabeçalhos do multipart: a foto e a miniatura têm limites próprios.
            feature.MaxRequestBodySize = ValidacaoMidiaEvidencia.MaxBytesVideo + ValidacaoMidiaEvidencia.MaxBytesMiniatura + (16 * 1024);
        }

        return await next(context);
    }
}
