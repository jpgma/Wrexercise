using Wrexercise.Features.Usuarios;

namespace Wrexercise.UnitTests.Features.Usuarios;

public class ConquistasNovasTests
{
    private static string[] Codigos(EstatisticasConquistas estatisticas) =>
        ConquistasUsuario.Calcular(estatisticas).Select(c => c.Codigo).ToArray();

    private static EstatisticasConquistas Com(
        Dictionary<string, int>? porTipo = null,
        int noturnos = 0,
        int fimDeSemana = 0,
        int desafios = 0) =>
        new(0, 0, 0, noturnos, fimDeSemana, desafios, porTipo);

    [Theory]
    [InlineData("Musculacao", "RatoDeAcademia")]
    [InlineData("Corrida", "Corredor")]
    [InlineData("Caminhada", "Andarilho")]
    [InlineData("Ciclismo", "Ciclista")]
    [InlineData("Natacao", "Peixe")]
    [InlineData("Esporte", "Boleiro")]
    [InlineData("Yoga", "Zen")]
    [InlineData("Calistenia", "PesoDoCorpo")]
    public void ConquistaDeTipo_ExigeDezTreinosDoTipo(string tipo, string codigo)
    {
        Assert.Empty(Codigos(Com(new() { [tipo] = 9 })));
        Assert.Equal([codigo], Codigos(Com(new() { [tipo] = 10 })));
    }

    [Fact]
    public void Outro_NaoTemConquistaDeTipo() => Assert.Empty(Codigos(Com(new() { ["Outro"] = 50 })));

    [Fact]
    public void Ecletico_ContaCincoTiposSemOutro()
    {
        var quatroEOutro = new Dictionary<string, int>
        {
            ["Musculacao"] = 1,
            ["Corrida"] = 1,
            ["Caminhada"] = 1,
            ["Ciclismo"] = 1,
            ["Outro"] = 3,
        };
        Assert.DoesNotContain("Ecletico", Codigos(Com(quatroEOutro)));

        quatroEOutro["Yoga"] = 1;
        Assert.Contains("Ecletico", Codigos(Com(quatroEOutro)));
    }

    [Fact]
    public void Habitos_ExigemDezTreinos()
    {
        Assert.Empty(Codigos(Com(noturnos: 9, fimDeSemana: 9)));
        Assert.Equal(["Noturno", "FimDeSemana"], Codigos(Com(noturnos: 10, fimDeSemana: 10)));
    }

    [Fact]
    public void DesafioCompleto_ExigeUmDesafioConcluido()
    {
        Assert.Empty(Codigos(Com(desafios: 0)));
        Assert.Equal(["DesafioCompleto"], Codigos(Com(desafios: 1)));
    }
}
