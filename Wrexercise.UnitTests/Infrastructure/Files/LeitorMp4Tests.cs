using System.Buffers.Binary;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.UnitTests.Infrastructure.Files;

public class LeitorMp4Tests
{
    private static InfoVideo Ler(byte[] bytes) => LeitorMp4.Ler(new MemoryStream(bytes));

    private static string? CodigoDoErro(byte[] bytes) =>
        Assert.Throws<EvidenciaInvalidaException>(() => Ler(bytes)).Codigo;

    [Fact]
    public void Valido_LeDuracaoDimensoesECodecs()
    {
        var info = Ler(Mp4Teste.Criar(duracaoMs: 42_500, largura: 720, altura: 1280));

        Assert.Equal(new InfoVideo(42_500, 720, 1280, "avc1", "mp4a", null), info);
        Assert.Null(ValidacaoMidiaEvidencia.ValidarVideo(info));
    }

    [Fact]
    public void MoovDepoisDoMdat_TambemLe() =>
        Assert.Equal(10_000, Ler(Mp4Teste.Criar(moovNoFim: true)).DuracaoMs);

    [Fact]
    public void SemAudio_CodecAudioNulo() =>
        Assert.Null(Ler(Mp4Teste.Criar(codecAudio: null)).CodecAudio);

    [Fact]
    public void DataDeCriacao_Desde1904EmUtc()
    {
        var esperado = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
        var segundos = (uint)(esperado - new DateTimeOffset(1904, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds;

        Assert.Equal(esperado, Ler(Mp4Teste.Criar(segundosDesde1904: segundos)).CriadoEm);
    }

    [Fact]
    public void SemFaixaDeVideo_Invalido() =>
        Assert.Equal(CodigosVideo.Invalido, CodigoDoErro(Mp4Teste.Criar(semVideo: true)));

    [Fact]
    public void NaoComecaPorFtyp_Invalido() =>
        Assert.Equal(CodigosVideo.Invalido, CodigoDoErro(Mp4Teste.Caixa("moov", new byte[16])));

    [Fact]
    public void MarcaDesconhecida_FormatoNaoSuportado() =>
        Assert.Equal(CodigosVideo.FormatoNaoSuportado, CodigoDoErro(Mp4Teste.Criar(marca: "xxxx")));

    [Fact]
    public void Truncado_Invalido()
    {
        var bytes = Mp4Teste.Criar();
        Assert.Equal(CodigosVideo.Invalido, CodigoDoErro(bytes[..(bytes.Length - 10)]));
    }

    [Fact]
    public void CaixaMaiorQueOArquivo_Invalido()
    {
        var bytes = Mp4Teste.Criar(moovNoFim: true);
        // A caixa depois do ftyp (o mdat) diz ter 4 GB.
        var ftyp = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(ftyp), uint.MaxValue);

        Assert.Equal(CodigosVideo.Invalido, CodigoDoErro(bytes));
    }

    [Fact]
    public void ArquivoSemMoov_Invalido() =>
        Assert.Equal(CodigosVideo.Invalido, CodigoDoErro(Mp4Teste.Caixa("ftyp", [.. "isom"u8, 0, 0, 0, 0])));

    [Theory]
    [InlineData(1_999, CodigosVideo.MuitoCurto)]
    [InlineData(2_000, null)]
    [InlineData(61_000, null)]
    [InlineData(61_001, CodigosVideo.MuitoLongo)]
    public void ValidarVideo_DuracaoDe2a61Segundos(int duracaoMs, string? esperado) =>
        Assert.Equal(esperado, ValidacaoMidiaEvidencia.ValidarVideo(Ler(Mp4Teste.Criar(duracaoMs: duracaoMs))));

    [Theory]
    [InlineData("hvc1", "mp4a", 1280, 720)]
    [InlineData("avc1", "Opus", 1280, 720)]
    [InlineData("avc1", "mp4a", 3840, 2160)]
    public void ValidarVideo_SoH264AacAte1920(string video, string audio, int largura, int altura) =>
        Assert.Equal(
            CodigosVideo.FormatoNaoSuportado,
            ValidacaoMidiaEvidencia.ValidarVideo(
                Ler(Mp4Teste.Criar(codecVideo: video, codecAudio: audio, largura: largura, altura: altura))));

    [Fact]
    public void TryDetectarFormato_ReconheceMp4PeloFtyp()
    {
        Assert.True(ValidacaoMidiaEvidencia.TryDetectarFormato(Mp4Teste.Criar().AsSpan(0, 12), out var formato));
        Assert.Equal(FormatoMidiaEvidencia.Mp4, formato);
        Assert.Equal("video/mp4", ValidacaoMidiaEvidencia.GetTipoMime(formato));
    }
}
