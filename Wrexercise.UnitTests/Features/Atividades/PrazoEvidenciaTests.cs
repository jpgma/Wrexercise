using Wrexercise.Features.Atividades;

namespace Wrexercise.UnitTests.Features.Atividades;

public sealed class PrazoEvidenciaTests
{
    [Theory]
    [InlineData(SituacaoVerificacao.EmAndamento, TipoComprovacao.Fotografia, true, "NoPrazo")]
    [InlineData(SituacaoVerificacao.AguardandoFeedback, TipoComprovacao.Fotografia, true, "NoPrazo")]
    [InlineData(SituacaoVerificacao.Verificada, TipoComprovacao.CapturaDeTela, false, "NoPrazo")]
    [InlineData(SituacaoVerificacao.Verificada, TipoComprovacao.Fotografia, true, "Depois")]
    [InlineData(SituacaoVerificacao.EmRevisao, TipoComprovacao.Fotografia, true, "Depois")]
    [InlineData(SituacaoVerificacao.Contestada, TipoComprovacao.Fotografia, true, "Depois")]
    [InlineData(SituacaoVerificacao.Rejeitada, TipoComprovacao.Fotografia, true, "Depois")]
    [InlineData(SituacaoVerificacao.AguardandoFeedback, TipoComprovacao.RegistroManual, true, "Depois")]
    [InlineData(SituacaoVerificacao.Verificada, TipoComprovacao.RegistroManual, false, "Depois")]
    [InlineData(SituacaoVerificacao.Anulada, TipoComprovacao.Fotografia, false, "Recusada")]
    public void Classificar(string situacao, string tipoComprovacao, bool temRevisores, string esperado) =>
        Assert.Equal(esperado, PrazoEvidencia.Classificar(situacao, tipoComprovacao, temRevisores).ToString());

    [Theory]
    [InlineData(true, SituacaoVerificacao.Rejeitada, true)]
    [InlineData(true, SituacaoVerificacao.Anulada, false)]
    [InlineData(false, SituacaoVerificacao.Verificada, false)]
    public void AutorPodeAnexar(bool autor, string situacao, bool esperado) =>
        Assert.Equal(esperado, PrazoEvidencia.AutorPodeAnexar(autor, situacao));
}
