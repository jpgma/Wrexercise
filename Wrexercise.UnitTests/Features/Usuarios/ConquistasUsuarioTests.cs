using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Usuarios;

namespace Wrexercise.UnitTests.Features.Usuarios;

public class ConquistasUsuarioTests
{
    private static string[] Codigos(int treinos, int sequencia, int antesDas7) =>
        ConquistasUsuario
            .Calcular(new EstatisticasConquistas(treinos, sequencia, antesDas7, DiasComTreino: treinos))
            .Select(c => c.Codigo)
            .ToArray();

    [Fact]
    public void Calcular_SemNada_NaoTemConquistas() => Assert.Empty(Codigos(9, 3, 9));

    [Fact]
    public void Medalhas_ContamDiasComTreino_NaoAtividades() =>
        Assert.DoesNotContain(
            "Treinos10",
            ConquistasUsuario
                .Calcular(new EstatisticasConquistas(TotalTreinos: 30, 0, 0, DiasComTreino: 9))
                .Select(c => c.Codigo));

    [Fact]
    public void Calcular_NoLimiteDeCadaMarco() =>
        Assert.Equal(["Sequencia4", "Treinos10", "Madrugador"], Codigos(10, 4, 10));

    [Fact]
    public void Calcular_AcumulaOsMarcosAlcancados() =>
        Assert.Equal(
            ["Sequencia12", "Sequencia8", "Sequencia4", "Treinos100", "Treinos50", "Treinos10"],
            Codigos(100, 12, 0));

    [Fact]
    public void Listar_TrazTodas_ComProgressoLimitadoAoAlvo()
    {
        var todas = ConquistasUsuario.Listar(
            new EstatisticasConquistas(0, 5, 3, TreinosPorTipo: new Dictionary<string, int> { ["Corrida"] = 25 }, DiasComTreino: 23));

        Assert.Equal(22, todas.Count);
        Assert.Equal((4, 4, true), Progresso(todas, "Sequencia4"));
        Assert.Equal((5, 8, false), Progresso(todas, "Sequencia8"));
        Assert.Equal((23, 50, false), Progresso(todas, "Treinos50"));
        Assert.Equal((10, 10, true), Progresso(todas, "Corredor"));
        Assert.Equal((3, 10, false), Progresso(todas, "Madrugador"));
        Assert.Equal((1, 5, false), Progresso(todas, "Ecletico"));
        Assert.Equal((0, 1, false), Progresso(todas, "DesafioCompleto"));
    }

    [Fact]
    public void Visiveis_MostraSoOProximoNivel()
    {
        var codigos = ConquistasUsuario
            .Visiveis(ConquistasUsuario.Listar(new EstatisticasConquistas(0, 5, 0)))
            .Select(c => c.Codigo)
            .ToList();

        Assert.Equal("Sequencia4", codigos[0]);
        Assert.Contains("Sequencia8", codigos);
        Assert.DoesNotContain("Sequencia12", codigos);
        Assert.Contains("Treinos10", codigos);
        Assert.DoesNotContain("Treinos50", codigos);
    }

    [Fact]
    public void Visiveis_BloqueadasDaMaisPertoAMaisLonge()
    {
        var visiveis = ConquistasUsuario.Visiveis(ConquistasUsuario.Listar(
            new EstatisticasConquistas(0, 0, 9, TreinosNoturnos: 2, DiasComTreino: 5)));

        Assert.Equal(["Madrugador", "Treinos10", "Noturno"], visiveis.Select(c => c.Codigo).Take(3));
        Assert.All(visiveis, c => Assert.False(c.Liberada));
    }

    private static (int Atual, int Alvo, bool Liberada) Progresso(IReadOnlyList<ConquistaResponse> todas, string codigo)
    {
        var conquista = todas.Single(c => c.Codigo == codigo);
        return (conquista.Atual, conquista.Alvo, conquista.Liberada);
    }

    [Theory]
    [InlineData(new int[0], 0)]
    [InlineData(new[] { 3 }, 1)]
    [InlineData(new[] { 0, 1, 2 }, 3)]
    [InlineData(new[] { 0, 1, 3, 4, 5, 7 }, 3)]
    [InlineData(new[] { 5, 4, 4, 3 }, 3)]
    public void GetMelhorSequencia_ContaSemanasConsecutivas(int[] indices, int esperado) =>
        Assert.Equal(esperado, CalculoSemanaDesafio.GetMelhorSequencia(indices));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 0)]
    public void GetSequenciaAtual_ParaNaPrimeiraFalha(int indiceRecente, int esperado) =>
        Assert.Equal(esperado, CalculoSemanaDesafio.GetSequenciaAtual([true, true, true, false], indiceRecente));

    [Theory]
    [InlineData(8, "Faltam 2 treinos no fim de semana")]
    [InlineData(9, "Falta 1 treino no fim de semana")]
    [InlineData(10, null)]
    [InlineData(25, null)]
    public void Listar_DizOQueFalta_NoSingularENoPlural(int treinosNoFimDeSemana, string? esperado)
    {
        var conquistas = ConquistasUsuario.Listar(
            new EstatisticasConquistas(0, 0, 0, TreinosFimDeSemana: treinosNoFimDeSemana));

        Assert.Equal(esperado, conquistas.Single(c => c.Codigo == ConquistasUsuario.FimDeSemana).Falta);
    }

    [Fact]
    public void Listar_OQueFaltaUsaAUnidadeDeCadaConquista()
    {
        var falta = ConquistasUsuario.Listar(new EstatisticasConquistas(0, 3, 0, DiasComTreino: 9))
            .ToDictionary(c => c.Codigo, c => c.Falta);

        Assert.Equal("Falta 1 semana seguida com a meta batida", falta["Sequencia4"]);
        Assert.Equal("Falta 1 dia com treino", falta["Treinos10"]);
        Assert.Equal("Falta 1 desafio completo", falta["DesafioCompleto"]);
        Assert.Equal("Faltam 10 treinos de corrida", falta["Corredor"]);
        Assert.Equal("Faltam 10 treinos antes das 7h", falta["Madrugador"]);
        Assert.Equal("Faltam 10 treinos a partir das 20h", falta["Noturno"]);
        Assert.Equal("Faltam 5 tipos de treino diferentes", falta["Ecletico"]);
    }
}
