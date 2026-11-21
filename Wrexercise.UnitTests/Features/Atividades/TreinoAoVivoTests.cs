using Wrexercise.Features.Atividades;

namespace Wrexercise.UnitTests.Features.Atividades;

public class TreinoAoVivoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Inicio = Agora.AddMinutes(-30);

    [Theory]
    [InlineData(60, true)]
    [InlineData(1800, true)]
    [InlineData(1801, false)]
    [InlineData(59, false)]
    [InlineData(null, false)]
    public void DuracaoFinal_VaiDe60SegundosAteOTempoDecorrido(int? segundos, bool valida) =>
        Assert.Equal(valida, TreinoAoVivo.IsValidDuracaoFinal(segundos, Inicio, Agora));

    [Fact]
    public void DuracaoFinal_NuncaPassaDe24Horas() =>
        Assert.False(TreinoAoVivo.IsValidDuracaoFinal(24 * 3600 + 60, Agora.AddHours(-25), Agora));

    [Theory]
    [InlineData("RegistroManual", 0, true)]
    [InlineData("RegistroManual", 1, false)]
    [InlineData("Fotografia", 0, true)]
    [InlineData("Fotografia", 3, true)]
    [InlineData("CapturaDeTela", 2, true)]
    [InlineData("Outro", 0, false)]
    public void ComprovacaoFinal_ComEvidenciasNaoPodeSerManual(string tipo, int evidencias, bool valida) =>
        Assert.Equal(valida, TreinoAoVivo.IsValidComprovacaoFinal(tipo, evidencias));

    [Fact]
    public void Decorridos_ArredondaParaBaixoENuncaFicaNegativo()
    {
        Assert.Equal(1800, TreinoAoVivo.Decorridos(Inicio, Agora.AddMilliseconds(999)));
        Assert.Equal(0, TreinoAoVivo.Decorridos(Agora.AddMinutes(1), Agora));
    }

    [Fact]
    public void Exclusao_TreinoAoVivoSeCancelaAQualquerMomento()
    {
        var bemDepois = Inicio.AddHours(5);

        Assert.True(ExclusaoAtividade.Permitida(true, SituacaoVerificacao.EmAndamento, Inicio, bemDepois));
        Assert.False(ExclusaoAtividade.Permitida(false, SituacaoVerificacao.EmAndamento, Inicio, bemDepois));
        Assert.False(ExclusaoAtividade.Permitida(true, SituacaoVerificacao.AguardandoFeedback, Inicio, bemDepois));
    }

    [Fact]
    public void Correcao_TreinoAoVivoNaoSeCorrige() =>
        Assert.False(CorrecaoDuracao.SituacaoPermite(SituacaoVerificacao.EmAndamento));

    [Theory]
    [InlineData(1500, 1500)]
    [InlineData(1800, 1800)]
    [InlineData(2400, 1800)]
    [InlineData(0, 1)]
    public void SugestaoApp_FicaEntre1EODecorrido(int informada, int esperada) =>
        Assert.Equal(esperada, TreinoAoVivo.SugestaoApp(informada, Inicio, Agora));

    [Fact]
    public void FimApp_FicaEntreOInicioEAgora()
    {
        Assert.Equal(Inicio, TreinoAoVivo.FimApp(Inicio.AddMinutes(-5), Inicio, Agora));
        Assert.Equal(Agora, TreinoAoVivo.FimApp(Agora.AddMinutes(5), Inicio, Agora));
        Assert.Equal(Agora.AddMinutes(-3), TreinoAoVivo.FimApp(Agora.AddMinutes(-3), Inicio, Agora));
    }
}
