using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public sealed class PoseAvatarTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false, false, PoseAvatar.Parado)]
    [InlineData(false, true, false, PoseAvatar.Comemorando)]
    [InlineData(true, false, false, PoseAvatar.Treinando)]
    [InlineData(true, true, false, PoseAvatar.Treinando)]
    [InlineData(false, false, true, PoseAvatar.Dormindo)]
    [InlineData(false, true, true, PoseAvatar.Comemorando)]
    [InlineData(true, false, true, PoseAvatar.Treinando)]
    public void Calcular_AplicaAPrioridade(bool pendente, bool meta, bool dormindo, string esperado) =>
        Assert.Equal(esperado, PoseAvatar.Calcular(pendente, meta, dormindo));

    [Theory]
    [InlineData(47.9, false)]
    [InlineData(48, true)]
    [InlineData(200, true)]
    public void IsDormindo_ComecaEm48Horas(double horas, bool esperado) =>
        Assert.Equal(esperado, PoseAvatar.IsDormindo(Agora.AddHours(-horas), Agora));
}
