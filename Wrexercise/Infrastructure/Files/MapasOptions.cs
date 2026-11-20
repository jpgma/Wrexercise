namespace Wrexercise.Infrastructure.Files;

/// <summary>O cache dos tiles do mini-mapa (ADR 0080). A pasta pode ser apagada: ela se refaz sozinha.</summary>
internal sealed class MapasOptions
{
    public const string Section = "Mapas";

    public string Diretorio { get; set; } = "/dados/mapas";

    /// <summary>O servidor de tiles, no formato <c>{z}/{x}/{y}.png</c>.</summary>
    public string Servidor { get; set; } = "https://tile.openstreetmap.org";

    /// <summary>A política de uso do OpenStreetMap exige um User-Agent que identifique o aplicativo.</summary>
    public string UserAgent { get; set; } = "Wrexercise/1.0 (aplicativo privado de um pequeno grupo)";
}
