using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public sealed class EnfeitesTests
{
    [Fact]
    public void Liberados_EvoluiParaOMaisAlto()
    {
        var liberados = Enfeites.Liberados(["Sequencia4", "Sequencia8", "Treinos10", "Madrugador"]);

        Assert.Equal(["planta", "medalha", "despertador"], liberados.Select(e => e.Codigo));
        Assert.Equal("Sequencia8", liberados[0].Conquista);
        Assert.Equal("Treinos10", liberados[1].Conquista);
    }

    [Fact]
    public void Liberados_SemConquistas_EVazio() => Assert.Empty(Enfeites.Liberados([]));

    [Fact]
    public void Resolver_EnfeitesNovosEntramNasVagas()
    {
        var liberados = Enfeites.Liberados(["Treinos10", "Corredor", "Zen", "Noturno"]);

        var (vitrine, novos) = Enfeites.Resolver(liberados, [], []);

        Assert.Equal(["medalha", "cronometro", "tapete"], vitrine);
        Assert.Equal(["medalha", "cronometro", "tapete", "poste"], novos);
    }

    [Fact]
    public void Resolver_EvolucaoMantemOLugar()
    {
        var liberados = Enfeites.Liberados(["Treinos50", "Corredor"]);

        var (vitrine, novos) = Enfeites.Resolver(liberados, ["cronometro", "medalha"], ["medalha", "cronometro"]);

        Assert.Equal(["cronometro", "medalha"], vitrine);
        Assert.Empty(novos);
    }

    [Fact]
    public void Resolver_EnfeiteRemovidoEJaVisto_NaoVolta()
    {
        var liberados = Enfeites.Liberados(["Treinos10", "Corredor"]);

        var (vitrine, novos) = Enfeites.Resolver(liberados, ["medalha"], ["medalha", "cronometro"]);

        Assert.Equal(["medalha"], vitrine);
        Assert.Empty(novos);
    }

    [Fact]
    public void Resolver_IgnoraCodigoInvalidoERepetido()
    {
        var liberados = Enfeites.Liberados(["Treinos10"]);

        var (vitrine, _) = Enfeites.Resolver(liberados, ["inexistente", "medalha", "medalha", "bola"], ["medalha"]);

        Assert.Equal(["medalha"], vitrine);
    }

    [Fact]
    public void IsVitrineValida_ExigeAteTresLiberadosSemRepetir()
    {
        var liberados = Enfeites.Liberados(["Treinos10", "Corredor", "Zen", "Noturno"]);

        Assert.True(Enfeites.IsVitrineValida(liberados, []));
        Assert.True(Enfeites.IsVitrineValida(liberados, ["poste", "medalha"]));
        Assert.False(Enfeites.IsVitrineValida(liberados, ["poste", "poste"]));
        Assert.False(Enfeites.IsVitrineValida(liberados, ["bola"]));
        Assert.False(Enfeites.IsVitrineValida(liberados, ["medalha", "cronometro", "tapete", "poste"]));
    }
}
