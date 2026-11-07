using Wrexercise.Features.Autenticacao;

namespace Wrexercise.UnitTests.Features.Autenticacao;

public class ModoInicioTests
{
    [Theory]
    [InlineData("App")]
    [InlineData("Jogo")]
    public void IsValid_AceitaOsDoisModos(string modo)
    {
        Assert.True(ModoInicio.IsValid(modo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jogo")]
    [InlineData("app")]
    [InlineData("Outro")]
    public void IsValid_RecusaQualquerOutroValor(string? modo)
    {
        Assert.False(ModoInicio.IsValid(modo));
    }
}
