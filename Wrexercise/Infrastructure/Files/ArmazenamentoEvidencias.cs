using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Wrexercise.Infrastructure.Files;

internal sealed class ArmazenamentoEvidencias(IOptions<EvidenciasOptions> options)
{
    private const int BufferSize = 81920;
    private readonly string _diretorioRaiz = options.Value.Diretorio;

    /// <summary>
    /// Grava a mídia enviada e confere o formato pelo conteúdo. <paramref name="maxBytesImagem"/> vale para
    /// imagens; vídeos (só quando <paramref name="aceitaVideo"/>) têm o limite próprio e passam pelo
    /// <see cref="LeitorMp4"/>. Qualquer recusa apaga o arquivo temporário.
    /// </summary>
    public async Task<EvidenciaSalva> SalvarAsync(
        Stream origem,
        bool aceitaVideo,
        int maxBytesImagem,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_diretorioRaiz);

        var agora = DateTimeOffset.UtcNow;
        var subpasta = Path.Combine(agora.Year.ToString("D4"), agora.Month.ToString("D2"));
        var diretorioDestino = Path.Combine(_diretorioRaiz, subpasta);
        Directory.CreateDirectory(diretorioDestino);

        var nomeArquivo = $"{Guid.NewGuid():N}";
        var caminhoTemporario = Path.Combine(diretorioDestino, nomeArquivo + ".tmp");

        try
        {
            var (formato, sha256) = await GravarAsync(
                origem, caminhoTemporario, aceitaVideo, maxBytesImagem, cancellationToken);

            int? duracaoMs = null;
            if (ValidacaoMidiaEvidencia.IsVideo(formato))
            {
                await using var leitura = File.OpenRead(caminhoTemporario);
                var video = LeitorMp4.Ler(leitura);
                if (ValidacaoMidiaEvidencia.ValidarVideo(video) is { } erro)
                {
                    throw new EvidenciaInvalidaException("Vídeo fora das regras.", erro);
                }

                duracaoMs = video.DuracaoMs;
            }

            var extensao = ValidacaoMidiaEvidencia.GetExtensao(formato);
            var caminhoFinal = Path.Combine(diretorioDestino, nomeArquivo + extensao);
            File.Move(caminhoTemporario, caminhoFinal);

            var caminhoRelativo = Path.Combine(subpasta, nomeArquivo + extensao).Replace('\\', '/');
            return new EvidenciaSalva(
                caminhoRelativo,
                ValidacaoMidiaEvidencia.GetTipoMime(formato),
                (int)new FileInfo(caminhoFinal).Length,
                sha256,
                duracaoMs);
        }
        catch
        {
            // Sem isto, cada envio recusado deixava um .tmp órfão, e um vídeo pesa até 25 MB.
            File.Delete(caminhoTemporario);
            throw;
        }
    }

    private static async Task<(FormatoMidiaEvidencia Formato, string Sha256)> GravarAsync(
        Stream origem,
        string caminhoTemporario,
        bool aceitaVideo,
        int maxBytesImagem,
        CancellationToken cancellationToken)
    {
        await using var destino = new FileStream(
            caminhoTemporario,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous);

        var buffer = new byte[BufferSize];
        var header = new byte[ValidacaoMidiaEvidencia.BytesAssinatura];
        var headerLength = 0;
        FormatoMidiaEvidencia? formato = null;
        long total = 0;
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        while (true)
        {
            var lidos = await origem.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (lidos == 0)
            {
                break;
            }

            if (headerLength < header.Length)
            {
                var copiar = Math.Min(lidos, header.Length - headerLength);
                buffer.AsSpan(0, copiar).CopyTo(header.AsSpan(headerLength));
                headerLength += copiar;
                if (headerLength == header.Length)
                {
                    formato = DetectarFormato(header, aceitaVideo);
                }
            }

            total += lidos;
            var limite = formato is { } f && ValidacaoMidiaEvidencia.IsVideo(f)
                ? ValidacaoMidiaEvidencia.MaxBytesVideo
                : maxBytesImagem;
            if (total > limite)
            {
                throw new EvidenciaInvalidaException("Arquivo excede o limite permitido.");
            }

            hasher.AppendData(buffer, 0, lidos);
            await destino.WriteAsync(buffer.AsMemory(0, lidos), cancellationToken);
        }

        if (total == 0)
        {
            throw new EvidenciaInvalidaException("Arquivo vazio.");
        }

        // Arquivo menor que a assinatura completa: ainda pode ser um JPEG ou PNG minúsculo.
        formato ??= DetectarFormato(header.AsSpan(0, headerLength), aceitaVideo);
        return (formato.Value, Convert.ToHexString(hasher.GetHashAndReset()));
    }

    private static FormatoMidiaEvidencia DetectarFormato(ReadOnlySpan<byte> header, bool aceitaVideo)
    {
        if (!ValidacaoMidiaEvidencia.TryDetectarFormato(header, out var formato)
            || (ValidacaoMidiaEvidencia.IsVideo(formato) && !aceitaVideo))
        {
            throw new EvidenciaInvalidaException("Formato de arquivo não suportado.");
        }

        return formato;
    }

    public string ResolverCaminhoCompleto(string caminhoRelativo)
    {
        var caminhoCompleto = Path.Combine(_diretorioRaiz, caminhoRelativo.Replace('/', Path.DirectorySeparatorChar));
        var raizNormalizado = Path.GetFullPath(_diretorioRaiz);
        var completoNormalizado = Path.GetFullPath(caminhoCompleto);
        if (!completoNormalizado.StartsWith(raizNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            throw new EvidenciaInvalidaException("Caminho inválido.");
        }

        return completoNormalizado;
    }

    public void ExcluirSeExistir(string caminhoRelativo)
    {
        var caminhoCompleto = Path.Combine(_diretorioRaiz, caminhoRelativo.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(caminhoCompleto))
        {
            File.Delete(caminhoCompleto);
        }
    }

}

internal sealed record EvidenciaSalva(
    string CaminhoRelativo,
    string TipoMime,
    int TamanhoBytes,
    string Sha256,
    int? DuracaoMs);

/// <summary><c>Codigo</c>: o código de erro próprio (ex.: <c>VIDEO_MUITO_LONGO</c>); sem ele, <c>EVIDENCIA_INVALIDA</c>.</summary>
internal sealed class EvidenciaInvalidaException(string message, string? codigo = null) : Exception(message)
{
    public string? Codigo { get; } = codigo;
}
