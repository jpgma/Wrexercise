using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public sealed class PoseAvatarTests
{
    [Theory]
    [InlineData(false, false, PoseAvatar.Parado)]
    [InlineData(false, true, PoseAvatar.Comemorando)]
    [InlineData(true, false, PoseAvatar.Treinando)]
    [InlineData(true, true, PoseAvatar.Treinando)]
    public void Calcular_AplicaAPrioridade(bool pendente, bool meta, string esperado) =>
        Assert.Equal(esperado, PoseAvatar.Calcular(pendente, meta));
}
