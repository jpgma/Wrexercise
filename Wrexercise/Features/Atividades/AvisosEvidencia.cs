using System.Globalization;
using System.Numerics;
using Wrexercise.Features.Atividades.Contracts;

namespace Wrexercise.Features.Atividades;

internal static class OrigemDataCaptura
{
    public const string Exif = "Exif";
    public const string Arquivo = "Arquivo";

    /// <summary>Capturada pela câmera do próprio aplicativo, com a data do momento da captura (ADR 0058).</summary>
    public const string Camera = "Camera";

    /// <summary>
    /// Captura de tela com a data e a hora no nome do arquivo, como <c>Screenshot_20261114-083012.png</c> (ADR 0060).
    /// O nome não muda ao copiar o arquivo; ao contrário da data do arquivo, vale nos dois sentidos.
    /// </summary>
    public const string Print = "Print";
}

/// <summary>
/// Regras puras dos avisos sobre a evidência. Os dados vêm do navegador e podem ser forjados:
/// servem para avisar o grupo, não para provar nada.
/// </summary>
internal static class AvisosEvidencia
{
    public static readonly TimeSpan MargemAntesDoInicio = TimeSpan.FromHours(2);
    public static readonly TimeSpan MargemDepoisDoFim = TimeSpan.FromHours(12);
    public static readonly TimeSpan AtrasoParaRegistroTardio = TimeSpan.FromHours(12);
    public static readonly TimeSpan MaximoNoPassado = TimeSpan.FromHours(48);
    public static readonly TimeSpan ToleranciaFuturo = TimeSpan.FromHours(1);
    public const int DistanciaMaximaFotoParecida = 6;
    public const int DistanciaMaximaVideoParado = 2;

    /// <summary>
    /// Bits ligados que um <c>dhash</c> precisa ter, de cada lado, para servir à comparação. O dHash só liga um bit
    /// quando um ponto é mais claro que o vizinho da direita: áreas de cor lisa, como o fundo de um print de app,
    /// dão bits desligados, e dois prints diferentes ficavam a menos de 6 bits um do outro.
    /// </summary>
    public const int MinimoBitsDhashInformativo = 12;

    public static (DateTimeOffset Inicio, DateTimeOffset Fim) JanelaFoto(DateTimeOffset startedAt, int duracaoSegundos) =>
        (startedAt - MargemAntesDoInicio, startedAt.AddSeconds(duracaoSegundos) + MargemDepoisDoFim);

    /// <summary>
    /// Aviso sobre a data da foto, ou <c>null</c> quando ela combina com a atividade.
    /// A data do EXIF vale nos dois sentidos. A data do arquivo só vale se for anterior à janela:
    /// copiar ou receber o arquivo muda essa data para depois, nunca para antes da foto.
    /// </summary>
    public static string? CalcularAvisoData(
        DateTimeOffset startedAt,
        int duracaoSegundos,
        DateTimeOffset? capturadaEm,
        string? origem,
        DateTimeOffset agora)
    {
        if (capturadaEm is not { } instante || origem is null)
        {
            return TipoAviso.FotoSemData;
        }

        var (inicio, fim) = JanelaFoto(startedAt, duracaoSegundos);
        if (instante < inicio || instante > agora + ToleranciaFuturo)
        {
            return TipoAviso.FotoForaDoPeriodo;
        }

        return origem switch
        {
            OrigemDataCaptura.Exif or OrigemDataCaptura.Camera or OrigemDataCaptura.Print when instante > fim
                => TipoAviso.FotoForaDoPeriodo,
            OrigemDataCaptura.Exif or OrigemDataCaptura.Camera or OrigemDataCaptura.Print => null,
            // A data do arquivo dentro ou depois da janela não diz nada sobre quando a foto foi tirada.
            _ => TipoAviso.FotoSemData,
        };
    }

    public static bool IsRegistroTardio(DateTimeOffset startedAt, int duracaoSegundos, DateTimeOffset submittedAt) =>
        submittedAt - startedAt.AddSeconds(duracaoSegundos) > AtrasoParaRegistroTardio;

    public static bool IsDentroDoLimiteNoPassado(DateTimeOffset startedAt, DateTimeOffset agora) =>
        startedAt >= agora - MaximoNoPassado;

    public static int DistanciaHamming(long a, long b) => BitOperations.PopCount((ulong)(a ^ b));

    /// <summary>
    /// O hash diz algo sobre a imagem: nem quase todos os bits desligados (cor lisa), nem quase todos ligados.
    /// Sem isso, <see cref="TipoAviso.FotoParecida"/> não compara a foto.
    /// </summary>
    public static bool IsDhashInformativo(long dhash) =>
        BitOperations.PopCount((ulong)dhash) is >= MinimoBitsDhashInformativo and <= 64 - MinimoBitsDhashInformativo;

    /// <summary>
    /// Os quadros a 10%, 50% e 90% do vídeo quase iguais: pode ser uma foto disfarçada de vídeo (ADR 0058).
    /// Sem os três hashes, não há como dizer.
    /// </summary>
    public static bool IsVideoParado(long? inicio, long? meio, long? fim) =>
        inicio is { } a && meio is { } b && fim is { } c
        && DistanciaHamming(a, b) <= DistanciaMaximaVideoParado
        && DistanciaHamming(b, c) <= DistanciaMaximaVideoParado;

    /// <summary>Lê o SHA-256 em hexadecimal (64 caracteres) e normaliza para maiúsculas, como o servidor grava.</summary>
    public static bool TryParseSha256(string? valor, out string normalizado)
    {
        normalizado = string.Empty;
        if (valor is not { Length: 64 } || !valor.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        normalizado = valor.ToUpperInvariant();
        return true;
    }

    /// <summary>Lê o dHash de 64 bits em hexadecimal (16 caracteres).</summary>
    public static bool TryParseDhash(string? valor, out long dhash)
    {
        dhash = 0;
        if (valor is not { Length: 16 }
            || !ulong.TryParse(valor, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var bits))
        {
            return false;
        }

        dhash = unchecked((long)bits);
        return true;
    }

    /// <summary>Lê latitude e longitude em graus decimais; as duas ou nenhuma.</summary>
    public static bool TryParseCoordenadas(string? latitude, string? longitude, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;
        return double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
            && lat is >= -90 and <= 90
            && lon is >= -180 and <= 180
            && !(lat == 0 && lon == 0);
    }

    public static bool TryParseOrigem(string? valor, out string origem)
    {
        origem = valor ?? string.Empty;
        return valor is OrigemDataCaptura.Exif
            or OrigemDataCaptura.Arquivo
            or OrigemDataCaptura.Camera
            or OrigemDataCaptura.Print;
    }
}
