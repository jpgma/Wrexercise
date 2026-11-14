using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;

namespace Wrexercise.UnitTests.Features.Atividades;

public class AvisosEvidenciaTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
    private const int UmaHora = 3600;
    private static readonly DateTimeOffset Agora = Inicio.AddDays(1);

    private static string? Aviso(DateTimeOffset? capturadaEm, string? origem) =>
        AvisosEvidencia.CalcularAvisoData(Inicio, UmaHora, capturadaEm, origem, Agora);

    [Fact]
    public void JanelaFoto_DuasHorasAntesAteDozeDepoisDoFim()
    {
        var (inicio, fim) = AvisosEvidencia.JanelaFoto(Inicio, UmaHora);
        Assert.Equal(Inicio.AddHours(-2), inicio);
        Assert.Equal(Inicio.AddHours(13), fim);
    }

    [Theory]
    [InlineData(-120, null)]
    [InlineData(-121, TipoAviso.FotoForaDoPeriodo)]
    [InlineData(30, null)]
    [InlineData(13 * 60, null)]
    [InlineData((13 * 60) + 1, TipoAviso.FotoForaDoPeriodo)]
    public void Exif_ValeNosDoisSentidos(int minutosDoInicio, string? esperado) =>
        Assert.Equal(esperado, Aviso(Inicio.AddMinutes(minutosDoInicio), OrigemDataCaptura.Exif));

    [Theory]
    [InlineData(30, null)]
    [InlineData(-121, TipoAviso.FotoForaDoPeriodo)]
    [InlineData((13 * 60) + 1, TipoAviso.FotoForaDoPeriodo)]
    public void CameraDoApp_ValeComoExif(int minutosDoInicio, string? esperado) =>
        Assert.Equal(esperado, Aviso(Inicio.AddMinutes(minutosDoInicio), OrigemDataCaptura.Camera));

    [Theory]
    [InlineData(30, null)]
    [InlineData(13 * 60, null)]
    [InlineData(-121, TipoAviso.FotoForaDoPeriodo)]
    [InlineData((13 * 60) + 1, TipoAviso.FotoForaDoPeriodo)]
    public void PrintComDataNoNome_ValeComoExif(int minutosDoInicio, string? esperado) =>
        Assert.Equal(esperado, Aviso(Inicio.AddMinutes(minutosDoInicio), OrigemDataCaptura.Print));

    [Theory]
    [InlineData("Exif", true)]
    [InlineData("Print", true)]
    [InlineData("Arquivo", true)]
    [InlineData("Camera", true)]
    [InlineData("camera", false)]
    [InlineData("", false)]
    public void TryParseOrigem_AceitaSoAsOrigensConhecidas(string valor, bool esperado) =>
        Assert.Equal(esperado, AvisosEvidencia.TryParseOrigem(valor, out _));

    [Theory]
    [InlineData(0x00L, 0x03L, 0x00L, true)]
    [InlineData(0x00L, 0x07L, 0x00L, false)]
    [InlineData(0x00L, 0x00L, 0xF0L, false)]
    public void VideoParado_QuadrosQuaseIguais(long inicio, long meio, long fim, bool esperado) =>
        Assert.Equal(esperado, AvisosEvidencia.IsVideoParado(inicio, meio, fim));

    [Fact]
    public void VideoParado_SemOsTresHashes_NaoAvisa() =>
        Assert.False(AvisosEvidencia.IsVideoParado(null, 0, 0));

    [Fact]
    public void SemData_FotoSemData() =>
        Assert.Equal(TipoAviso.FotoSemData, Aviso(null, null));

    [Fact]
    public void DataDoArquivoAntesDaJanela_ForaDoPeriodo() =>
        Assert.Equal(TipoAviso.FotoForaDoPeriodo, Aviso(Inicio.AddDays(-3), OrigemDataCaptura.Arquivo));

    [Theory]
    [InlineData(30)]
    [InlineData(20 * 60)]
    public void DataDoArquivoDentroOuDepois_SoSemData(int minutosDoInicio) =>
        Assert.Equal(TipoAviso.FotoSemData, Aviso(Inicio.AddMinutes(minutosDoInicio), OrigemDataCaptura.Arquivo));

    [Fact]
    public void DataNoFuturo_ForaDoPeriodo() =>
        Assert.Equal(TipoAviso.FotoForaDoPeriodo, Aviso(Agora.AddHours(2), OrigemDataCaptura.Exif));

    [Theory]
    [InlineData(12, false)]
    [InlineData(13, true)]
    public void RegistroTardio_DozeHorasDepoisDoFim(int horasDepoisDoFim, bool esperado) =>
        Assert.Equal(
            esperado,
            AvisosEvidencia.IsRegistroTardio(Inicio, UmaHora, Inicio.AddHours(1 + horasDepoisDoFim)));

    [Fact]
    public void LimiteNoPassado_48Horas()
    {
        Assert.True(AvisosEvidencia.IsDentroDoLimiteNoPassado(Agora.AddHours(-48), Agora));
        Assert.False(AvisosEvidencia.IsDentroDoLimiteNoPassado(Agora.AddHours(-48).AddMinutes(-1), Agora));
    }

    [Fact]
    public void DistanciaHamming_ContaBitsDiferentes()
    {
        Assert.Equal(0, AvisosEvidencia.DistanciaHamming(-1, -1));
        Assert.Equal(64, AvisosEvidencia.DistanciaHamming(0, -1));
        Assert.Equal(2, AvisosEvidencia.DistanciaHamming(0b1010, 0b0000));
    }

    [Theory]
    // Hashes reais de prints de app com fundo liso: diferentes entre si, mas a 4 bits um do outro.
    [InlineData("0080002001000001", false)]
    [InlineData("00000000a0000000", false)]
    [InlineData("0000000023010000", false)]
    [InlineData("0000000080a00100", false)]
    [InlineData("0000000000000000", false)]
    [InlineData("ffffffffffffffff", false)]
    [InlineData("0000000000000fff", true)]
    [InlineData("fffffffffffff000", true)]
    [InlineData("fffffffffffff800", false)]
    // Foto real da câmera do app.
    [InlineData("0a0306ebd4cc330e", true)]
    public void DhashInformativo_IgnoraImagensDeCorLisa(string hex, bool esperado)
    {
        Assert.True(AvisosEvidencia.TryParseDhash(hex, out var dhash));
        Assert.Equal(esperado, AvisosEvidencia.IsDhashInformativo(dhash));
    }

    [Fact]
    public void TryParseDhash_AceitaHexDe16()
    {
        Assert.True(AvisosEvidencia.TryParseDhash("ffffffffffffffff", out var dhash));
        Assert.Equal(-1L, dhash);
        Assert.False(AvisosEvidencia.TryParseDhash("fff", out _));
        Assert.False(AvisosEvidencia.TryParseDhash("zzzzzzzzzzzzzzzz", out _));
    }

    [Fact]
    public void TryParseSha256_NormalizaMaiusculas()
    {
        Assert.True(AvisosEvidencia.TryParseSha256(new string('a', 64), out var sha));
        Assert.Equal(new string('A', 64), sha);
        Assert.False(AvisosEvidencia.TryParseSha256(new string('g', 64), out _));
        Assert.False(AvisosEvidencia.TryParseSha256("abc", out _));
    }

    [Theory]
    [InlineData("-23.5505", "-46.6333", true)]
    [InlineData("91", "0", false)]
    [InlineData("0", "0", false)]
    [InlineData("-23.5", null, false)]
    [InlineData("abc", "1", false)]
    public void TryParseCoordenadas_ValidaFaixas(string? lat, string? lon, bool esperado) =>
        Assert.Equal(esperado, AvisosEvidencia.TryParseCoordenadas(lat, lon, out _, out _));
}
