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
}
