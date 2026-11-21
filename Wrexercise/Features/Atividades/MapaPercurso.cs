using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// Os tiles do mapa do Percurso (ADR 0084). O servidor só serve os tiles em volta do percurso guardado no banco, no
/// zoom que cabe na tela: a rota nunca vira um proxy aberto do servidor de mapas.
/// </summary>
internal static class MapaPercurso
{
    public const int ZoomMinimo = 12;
    public const int ZoomMaximo = 17;

    /// <summary>Quantos tiles o percurso (sem a margem) pode ocupar num zoom.</summary>
    public const int MaxTiles = 36;

    /// <summary>Os tiles que o percurso ocupa no zoom, de canto a canto.</summary>
    public static (int XMin, int YMin, int XMax, int YMax) Faixa(BboxPercurso bbox, int zoom)
    {
        var noroeste = TileMapa.De(bbox.LatMax, bbox.LonMin, zoom);
        var sudeste = TileMapa.De(bbox.LatMin, bbox.LonMax, zoom);
        return (noroeste.X, noroeste.Y, sudeste.X, sudeste.Y);
    }

    public static int TilesNecessarios(BboxPercurso bbox, int zoom)
    {
        var (xMin, yMin, xMax, yMax) = Faixa(bbox, zoom);
        return (xMax - xMin + 1) * (yMax - yMin + 1);
    }

    /// <summary>O tile toca o percurso, com um tile de margem, num zoom permitido e que não pede tiles demais.</summary>
    public static bool IsTilePermitido(BboxPercurso bbox, TileMapa tile)
    {
        if (tile.Zoom is < ZoomMinimo or > ZoomMaximo || TilesNecessarios(bbox, tile.Zoom) > MaxTiles)
        {
            return false;
        }

        var (xMin, yMin, xMax, yMax) = Faixa(bbox, tile.Zoom);
        return tile.X >= xMin - 1 && tile.X <= xMax + 1 && tile.Y >= yMin - 1 && tile.Y <= yMax + 1;
    }
}
