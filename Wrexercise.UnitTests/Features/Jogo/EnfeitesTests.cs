using Wrexercise.Features.Jogo;
using Wrexercise.Features.Usuarios.Contracts;

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
    public void Bloqueados_OmiteFamiliasComAlgumNivel_ETrazOPrimeiroNivel()
    {
        ProgressoConquista[] progresso =
        [
            new("Sequencia12", "12 semanas seguidas", 5, 12, false),
            new("Sequencia8", "8 semanas seguidas", 5, 8, false),
            new("Sequencia4", "4 semanas seguidas", 4, 4, true),
            new("Treinos100", "100 treinos", 7, 100, false),
            new("Treinos50", "50 treinos", 7, 50, false),
            new("Treinos10", "10 treinos", 7, 10, false),
            new("Corredor", "Corredor", 10, 10, true),
            new("Madrugador", "Madrugador", 3, 10, false),
        ];

        var bloqueados = Enfeites.Bloqueados(progresso);

        Assert.Equal(["medalha", "despertador"], bloqueados.Select(e => e.Codigo));
        Assert.Equal(new EnfeiteBloqueado("medalha", "Treinos10", "Medalha de bronze", "10 treinos", 7, 10), bloqueados[0]);
    }

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
