using Wrexercise.Infrastructure.Files;

namespace Wrexercise.UnitTests.Infrastructure.Files;

public class ValidacaoFolhaAvatarTests
{
    [Fact]
    public void FolhaNoFormato_EhValida() => Assert.True(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar()));

    [Theory]
    [InlineData(255, 384)]
    [InlineData(256, 383)]
    [InlineData(512, 768)]
    public void TamanhoDiferente_EhRecusado(int largura, int altura) =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(largura, altura)));

    [Fact]
    public void SemCanalAlfa_EhRecusado() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(tipoCor: 2)));

    [Fact]
    public void ProfundidadeDe16Bits_EhRecusada() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(profundidade: 16)));

    [Fact]
    public void Apng_EhRecusado() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(blocoExtra: "acTL")));

    [Fact]
    public void BlocoQueNaoEhApng_Passa() =>
        Assert.True(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(blocoExtra: "pHYs")));

    [Fact]
    public void MaisDe64Kb_EhRecusado() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(bytesExtras: ValidacaoFolhaAvatar.MaxBytes)));

    [Fact]
    public void Jpeg_EhRecusado() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46]));

    [Fact]
    public void Vazio_EhRecusado() => Assert.False(ValidacaoFolhaAvatar.IsValida([]));

    [Fact]
    public void SemIend_EhRecusado() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(comIend: false)));

    [Fact]
    public void ArquivoTruncado_EhRecusado()
    {
        var png = PngFolha.Criar();

        Assert.False(ValidacaoFolhaAvatar.IsValida(png.AsSpan(0, png.Length - 20)));
    }

    [Fact]
    public void DadosDepoisDoIend_SaoRecusados() =>
        Assert.False(ValidacaoFolhaAvatar.IsValida(PngFolha.Criar(lixoNoFim: 10)));

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef.png", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF.png", false)]
    [InlineData("0123456789abcdef0123456789abcdef.jpg", false)]
    [InlineData("0123456789abcdef0123456789abcde.png", false)]
    [InlineData("../123456789abcdef0123456789abcdef.png", false)]
    [InlineData("0123456789abcdef0123456789abcd/..png", false)]
    public void NomeDeArquivo_SoAceitaOQueOArmazenamentoGera(string nome, bool esperado) =>
        Assert.Equal(esperado, ArmazenamentoAvatares.IsNomeValido(nome));
}
