using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public sealed class ResultadoRivalTests
{
    // 2026-11-02 é uma segunda-feira.
    private static readonly DateOnly Segunda = new(2026, 11, 2);
    private static readonly DateTimeOffset Cedo = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Tarde = Cedo.AddHours(5);

    private static string Calcular(DateTimeOffset? escolhedor, DateTimeOffset? rival, int diasDepoisDaSegunda = 2) =>
        ResultadoRival.Calcular(escolhedor, rival, Segunda, Segunda.AddDays(diasDepoisDaSegunda));

    [Fact]
    public void SoQuemEscolheuCumpriu_Venceu() =>
        Assert.Equal(ResultadoRival.Venceu, Calcular(Cedo, null));

    [Fact]
    public void QuemEscolheuCumpriuPrimeiro_Venceu() =>
        Assert.Equal(ResultadoRival.Venceu, Calcular(Cedo, Tarde));

    [Fact]
    public void RivalCumpriuPrimeiro_Perdeu()
    {
        Assert.Equal(ResultadoRival.Perdeu, Calcular(Tarde, Cedo));
        Assert.Equal(ResultadoRival.Perdeu, Calcular(null, Cedo));
    }

    [Fact]
    public void MesmoHorario_SemVencedor() =>
        Assert.Equal(ResultadoRival.SemVencedor, Calcular(Cedo, Cedo));

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    // Domingo mais os dois dias da janela de verificação.
    [InlineData(8)]
    public void NinguemCumpriu_EmAndamentoAteOFimDaJanela(int dias) =>
        Assert.Equal(ResultadoRival.EmAndamento, Calcular(null, null, dias));

    [Fact]
    public void NinguemCumpriuDepoisDaJanela_SemVencedor() =>
        Assert.Equal(ResultadoRival.SemVencedor, Calcular(null, null, 9));

    [Fact]
    public void VitoriaNaoDependeDoFimDaSemana() =>
        Assert.Equal(ResultadoRival.Venceu, Calcular(Cedo, null, 1));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(2, 2, true)]
    [InlineData(3, 0, false)]
    [InlineData(0, 3, false)]
    public void PodeEscolher_SoAntesDaMetaDosDois(int escolhedor, int rival, bool esperado) =>
        Assert.Equal(esperado, ResultadoRival.PodeEscolher(escolhedor, rival, sessoesPorSemana: 3));
}
