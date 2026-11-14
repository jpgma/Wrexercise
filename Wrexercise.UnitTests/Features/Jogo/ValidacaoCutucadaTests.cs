using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class ValidacaoCutucadaTests
{
    [Theory]
    [InlineData("bora treinar", "bora treinar")]
    [InlineData("  bora   treinar  ", "bora treinar")]
    [InlineData("bora\ntreinar\r\nhoje", "bora treinar hoje")]
    [InlineData("bora\u0007 treinar", "bora treinar")]
    [InlineData("💪💪💪", "💪💪💪")]
    public void NormalizeTexto_UmaLinhaSemControle(string texto, string esperado) =>
        Assert.Equal(esperado, ValidacaoCutucada.NormalizeTexto(texto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void NormalizeTexto_VazioEInvalido(string? texto) => Assert.Null(ValidacaoCutucada.NormalizeTexto(texto));

    [Fact]
    public void NormalizeTexto_Limite80Caracteres()
    {
        Assert.NotNull(ValidacaoCutucada.NormalizeTexto(new string('a', 80)));
        Assert.Null(ValidacaoCutucada.NormalizeTexto(new string('a', 81)));
    }

    [Fact]
    public void NormalizeTexto_EmojiContaComoUmCaractere()
    {
        // 80 emojis são 160 unidades UTF-16, mas 80 caracteres para o PostgreSQL.
        Assert.NotNull(ValidacaoCutucada.NormalizeTexto(string.Concat(Enumerable.Repeat("💪", 80))));
    }
}
