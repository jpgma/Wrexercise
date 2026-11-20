using Wrexercise.Features.Reacoes;

namespace Wrexercise.UnitTests.Features.Reacoes;

public class IntervaloTorcidaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PrimeiraTorcida_SempreVale() => Assert.True(IntervaloTorcida.Permitida(null, Agora));

    [Fact]
    public void MenosDe10Minutos_Espera() =>
        Assert.False(IntervaloTorcida.Permitida(Agora.AddMinutes(-9), Agora));

    [Fact]
    public void Com10Minutos_TorceDeNovo() =>
        Assert.True(IntervaloTorcida.Permitida(Agora.AddMinutes(-10), Agora));
}
