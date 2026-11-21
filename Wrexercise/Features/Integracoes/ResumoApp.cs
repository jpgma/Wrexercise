using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Integracoes;

/// <summary>A imagem do resumo que o app manda (só foto) e a miniatura JPEG, gravadas para virar evidência.</summary>
internal static class ResumoApp
{
    /// <summary>
    /// Grava a imagem e a miniatura. Cada arquivo gravado entra em <paramref name="salvos"/> logo depois de
    /// gravado, para quem chama apagar se algo der errado. Recusa com <see cref="EvidenciaInvalidaException"/>.
    /// </summary>
    public static async Task<ArquivoIntegracao> SalvarAsync(
        IFormFile arquivo,
        IFormFile miniatura,
        ArmazenamentoEvidencias armazenamento,
        List<string> salvos,
        CancellationToken cancellationToken)
    {
        EvidenciaSalva imagem;
        await using (var stream = arquivo.OpenReadStream())
        {
            imagem = await armazenamento.SalvarAsync(
                stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytes, cancellationToken);
        }

        salvos.Add(imagem.CaminhoRelativo);

        EvidenciaSalva salvaMiniatura;
        await using (var stream = miniatura.OpenReadStream())
        {
            salvaMiniatura = await armazenamento.SalvarAsync(
                stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytesMiniatura, cancellationToken);
        }

        salvos.Add(salvaMiniatura.CaminhoRelativo);
        if (salvaMiniatura.TipoMime != ValidacaoMidiaEvidencia.TipoMimeMiniatura)
        {
            throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
        }

        return new ArquivoIntegracao(
            imagem.CaminhoRelativo,
            imagem.TipoMime,
            imagem.TamanhoBytes,
            imagem.Sha256,
            salvaMiniatura.CaminhoRelativo);
    }
}
