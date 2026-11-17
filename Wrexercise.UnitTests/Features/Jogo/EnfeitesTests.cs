using Wrexercise.Features.Jogo;
using Wrexercise.Features.Usuarios.Contracts;

namespace Wrexercise.UnitTests.Features.Jogo;

public sealed class EnfeitesTests
{
    [Fact]
    public void Liberados_EvoluiParaOMaisAlto()
    {
        var liberados = Enfeites.Liberados(["Sequencia4", "Sequencia8", "Treinos10", "Madrugador"]);

        Assert.Equal(["planta", "kettlebell", "galo"], liberados.Select(e => e.Codigo));
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

        Assert.Equal(["kettlebell", "galo"], bloqueados.Select(e => e.Codigo));
        Assert.Equal(new EnfeiteBloqueado("kettlebell", "Treinos10", "Kettlebell de bronze", "10 treinos", 7, 10), bloqueados[0]);
    }

    [Fact]
    public void Resolver_EnfeitesNovosEntramNasVagas()
    {
        var liberados = Enfeites.Liberados(["Treinos10", "Corredor", "Zen", "Noturno"]);

        var (vitrine, novos) = Enfeites.Resolver(liberados, [], []);

        Assert.Equal(["kettlebell", "largada", "tapete"], vitrine);
        Assert.Equal(["kettlebell", "largada", "tapete", "poste"], novos);
    }

    [Fact]
    public void Resolver_EvolucaoMantemOLugar()
    {
        var liberados = Enfeites.Liberados(["Treinos50", "Corredor"]);

        var (vitrine, novos) = Enfeites.Resolver(liberados, ["largada", "kettlebell"], ["kettlebell", "largada"]);

        Assert.Equal(["largada", "kettlebell"], vitrine);
        Assert.Empty(novos);
    }

    [Fact]
    public void Resolver_EnfeiteRemovidoEJaVisto_NaoVolta()
    {
        var liberados = Enfeites.Liberados(["Treinos10", "Corredor"]);

        var (vitrine, novos) = Enfeites.Resolver(liberados, ["kettlebell"], ["kettlebell", "largada"]);

        Assert.Equal(["kettlebell"], vitrine);
        Assert.Empty(novos);
    }

    [Fact]
    public void Resolver_IgnoraCodigoInvalidoERepetido()
    {
        var liberados = Enfeites.Liberados(["Treinos10"]);

        var (vitrine, _) = Enfeites.Resolver(liberados, ["inexistente", "kettlebell", "kettlebell", "golzinho"], ["kettlebell"]);

        Assert.Equal(["kettlebell"], vitrine);
    }

    [Fact]
    public void IsVitrineValida_ExigeAteTresLiberadosSemRepetir()
    {
        var liberados = Enfeites.Liberados(["Treinos10", "Corredor", "Zen", "Noturno"]);

        Assert.True(Enfeites.IsVitrineValida(liberados, []));
        Assert.True(Enfeites.IsVitrineValida(liberados, ["poste", "kettlebell"]));
        Assert.False(Enfeites.IsVitrineValida(liberados, ["poste", "poste"]));
        Assert.False(Enfeites.IsVitrineValida(liberados, ["golzinho"]));
        Assert.False(Enfeites.IsVitrineValida(liberados, ["kettlebell", "largada", "tapete", "poste"]));
    }

    [Fact]
    public void Bloqueados_LevamOQueFaltaDaConquista()
    {
        ProgressoConquista[] progresso = [new("FimDeSemana", "Fim de semana", 8, 10, false, "Faltam 2 treinos no fim de semana")];

        Assert.Equal("Faltam 2 treinos no fim de semana", Assert.Single(Enfeites.Bloqueados(progresso)).Falta);
    }

    [Fact]
    public void Vistos_GuardamOEnfeiteEONivel() =>
        Assert.Equal(["kettlebell", "Treinos50", "largada", "Corredor"], Enfeites.Vistos(Enfeites.Liberados(["Treinos50", "Corredor"])));

    [Theory]
    // Um Enfeite que a pessoa nunca viu.
    [InlineData(new string[0], new[] { "Treinos50" })]
    // O Enfeite subiu de nível desde a última vez.
    [InlineData(new[] { "kettlebell", "Treinos10" }, new[] { "Treinos50" })]
    // Visto antes de os níveis serem guardados: conta como comemorado.
    [InlineData(new[] { "kettlebell" }, new string[0])]
    [InlineData(new[] { "kettlebell", "Treinos50" }, new string[0])]
    public void ConquistasNovas_SaoOEnfeiteNovoEOQueSubiuDeNivel(string[] vistos, string[] esperadas) =>
        Assert.Equal(
            esperadas,
            Enfeites.ConquistasNovas(Enfeites.Liberados(["Treinos50"]), vistos).Select(e => e.Conquista));
}
