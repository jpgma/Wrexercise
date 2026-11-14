namespace Wrexercise.Infrastructure.Files;

internal enum FormatoMidiaEvidencia
{
    Jpeg,
    Png,
    Webp,
    Mp4,
}

/// <summary>
/// Formatos e limites das evidências. O servidor não decodifica a mídia: confere a assinatura do arquivo e,
/// no vídeo, lê as caixas do MP4 (ADR 0058). Redimensionar e converter é trabalho do navegador.
/// </summary>
internal static class ValidacaoMidiaEvidencia
{
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// O vídeo convertido no navegador tem cerca de 9,5 MB por minuto. A folga cobre o original, enviado sem
    /// conversão quando o navegador não tem WebCodecs.
    /// </summary>
    public const int MaxBytesVideo = 25 * 1024 * 1024;

    public const int MaxBytesMiniatura = 100 * 1024;
    public const string TipoMimeMiniatura = "image/jpeg";

    public const int DuracaoMinimaVideoMs = 2_000;

    /// <summary>Um segundo de folga sobre o minuto: o recorte e a conversão arredondam a duração.</summary>
    public const int DuracaoMaximaVideoMs = 61_000;

    public const int MaxLadoVideo = 1920;

    /// <summary>Bytes iniciais que bastam para reconhecer qualquer formato aceito.</summary>
    public const int BytesAssinatura = 12;

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> WebpRiff => "RIFF"u8;
    private static ReadOnlySpan<byte> WebpMagic => "WEBP"u8;
    private static ReadOnlySpan<byte> Mp4Ftyp => "ftyp"u8;

    public static bool TryDetectarFormato(ReadOnlySpan<byte> inicio, out FormatoMidiaEvidencia formato)
    {
        if (inicio.Length >= 3 && inicio.StartsWith(JpegMagic))
        {
            formato = FormatoMidiaEvidencia.Jpeg;
            return true;
        }

        if (inicio.Length >= 8 && inicio.StartsWith(PngMagic))
        {
            formato = FormatoMidiaEvidencia.Png;
            return true;
        }

        if (inicio.Length >= 12
            && inicio.StartsWith(WebpRiff)
            && inicio.Slice(8, 4).SequenceEqual(WebpMagic))
        {
            formato = FormatoMidiaEvidencia.Webp;
            return true;
        }

        // MP4 e MOV começam pela caixa `ftyp`; as marcas são conferidas pelo LeitorMp4.
        if (inicio.Length >= 8 && inicio.Slice(4, 4).SequenceEqual(Mp4Ftyp))
        {
            formato = FormatoMidiaEvidencia.Mp4;
            return true;
        }

        formato = default;
        return false;
    }

    public static bool IsVideo(FormatoMidiaEvidencia formato) => formato == FormatoMidiaEvidencia.Mp4;

    public static string GetTipoMime(FormatoMidiaEvidencia formato) => formato switch
    {
        FormatoMidiaEvidencia.Jpeg => "image/jpeg",
        FormatoMidiaEvidencia.Png => "image/png",
        FormatoMidiaEvidencia.Webp => "image/webp",
        FormatoMidiaEvidencia.Mp4 => "video/mp4",
        _ => throw new ArgumentOutOfRangeException(nameof(formato)),
    };

    public static string GetExtensao(FormatoMidiaEvidencia formato) => formato switch
    {
        FormatoMidiaEvidencia.Jpeg => ".jpg",
        FormatoMidiaEvidencia.Png => ".png",
        FormatoMidiaEvidencia.Webp => ".webp",
        FormatoMidiaEvidencia.Mp4 => ".mp4",
        _ => throw new ArgumentOutOfRangeException(nameof(formato)),
    };

    /// <summary>Código de erro do vídeo fora das regras, ou <c>null</c> quando ele é aceito.</summary>
    public static string? ValidarVideo(InfoVideo video)
    {
        if (video.CodecVideo is not ("avc1" or "avc3")
            || video.CodecAudio is not (null or "mp4a")
            || video.Largura <= 0
            || video.Altura <= 0
            || Math.Max(video.Largura, video.Altura) > MaxLadoVideo)
        {
            return CodigosVideo.FormatoNaoSuportado;
        }

        if (video.DuracaoMs < DuracaoMinimaVideoMs)
        {
            return CodigosVideo.MuitoCurto;
        }

        return video.DuracaoMs > DuracaoMaximaVideoMs ? CodigosVideo.MuitoLongo : null;
    }
}

internal static class CodigosVideo
{
    public const string Invalido = "VIDEO_INVALIDO";
    public const string FormatoNaoSuportado = "VIDEO_FORMATO_NAO_SUPORTADO";
    public const string MuitoCurto = "VIDEO_MUITO_CURTO";
    public const string MuitoLongo = "VIDEO_MUITO_LONGO";
}
