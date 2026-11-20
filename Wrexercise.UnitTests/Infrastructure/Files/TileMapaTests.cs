using Wrexercise.Infrastructure.Files;

namespace Wrexercise.UnitTests.Infrastructure.Files;

public class TileMapaTests
{
    [Theory]
    [InlineData(0.0, 0.0, 1, 1, 1)]
    [InlineData(0.0, 0.0, 16, 32768, 32768)]
    [InlineData(89.0, -180.0, 16, 0, 0)]
    [InlineData(-89.0, 180.0, 16, 65535, 65535)]
    // São Paulo (Praça da Sé).
    [InlineData(-23.5505, -46.6333, 16, 24278, 37181)]
    public void De_CalculaOTileDoPonto(double latitude, double longitude, int zoom, int x, int y)
    {
        Assert.Equal(new TileMapa(zoom, x, y), TileMapa.De(latitude, longitude, zoom));
    }

    [Fact]
    public void De_UsaOZoomPadrao()
    {
        Assert.Equal(TileMapa.ZoomPadrao, TileMapa.De(-23.5505, -46.6333).Zoom);
    }

    [Fact]
    public void Deslocar_DaAVoltaNaLongitudeEParaNoPolo()
    {
        var borda = new TileMapa(16, 0, 0);

        Assert.Equal(new TileMapa(16, 65535, 1), borda.Deslocar(-1, 1));
        Assert.Null(borda.Deslocar(0, -1));
        Assert.Null(new TileMapa(16, 3, 65535).Deslocar(0, 1));
    }
}
