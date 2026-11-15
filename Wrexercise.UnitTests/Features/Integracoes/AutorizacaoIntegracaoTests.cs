using Wrexercise.Features.Integracoes;

namespace Wrexercise.UnitTests.Features.Integracoes;

public class AutorizacaoIntegracaoTests
{
    // RFC 7636, apêndice B.
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Fact]
    public void Pkce_ConfereOExemploDaRfc() =>
        Assert.True(AutorizacaoIntegracao.VerificarPkce(Verifier, Challenge));

    [Fact]
    public void Pkce_RecusaOutroVerificador() =>
        Assert.False(AutorizacaoIntegracao.VerificarPkce(Verifier[..^1] + "Y", Challenge));

    [Theory]
    [InlineData("movr://integracoes/wrexercise", true)]
    [InlineData("http://127.0.0.1:53124/wx-callback", true)]
    [InlineData("http://127.0.0.1:65535/wx-callback", true)]
    [InlineData("http://127.0.0.1:65536/wx-callback", false)]
    [InlineData("http://127.0.0.1:080/wx-callback", false)]
    [InlineData("http://127.0.0.1:/wx-callback", false)]
    [InlineData("http://localhost:53124/wx-callback", false)]
    [InlineData("http://127.0.0.1:53124/wx-callback?x=1", false)]
    [InlineData("movr://integracoes/wrexercise/", false)]
    [InlineData("https://wx.example/callback", false)]
    [InlineData("", false)]
    public void RedirectUri_SoDaListaFechada(string redirectUri, bool esperado) =>
        Assert.Equal(esperado, AutorizacaoIntegracao.IsRedirectUriPermitida(AppIntegracao.Movr, redirectUri));

    [Fact]
    public void RedirectUri_RecusaAppDesconhecido() =>
        Assert.False(AutorizacaoIntegracao.IsRedirectUriPermitida("Outro", "movr://integracoes/wrexercise"));

    [Theory]
    [InlineData(Challenge, true)]
    [InlineData("curto", false)]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM", false)]
    public void CodeChallenge_Base64UrlCom43Caracteres(string challenge, bool esperado) =>
        Assert.Equal(esperado, AutorizacaoIntegracao.IsValidCodeChallenge(challenge));

    [Theory]
    [InlineData("01JD5K6QZ4", true)]
    [InlineData("treino_1-a", true)]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("com espaço", false)]
    public void ExternalId_SoCaracteresSeguros(string externalId, bool esperado) =>
        Assert.Equal(esperado, AutorizacaoIntegracao.IsValidExternalId(externalId));

    [Fact]
    public void Situacao_PostadaVenceAExpiracao()
    {
        var agora = DateTimeOffset.UtcNow;
        Assert.Equal(SituacaoRascunho.Postada, SituacaoRascunho.Calcular(Guid.NewGuid(), agora.AddHours(-1), agora));
        Assert.Equal(SituacaoRascunho.Expirada, SituacaoRascunho.Calcular(null, agora.AddHours(-1), agora));
        Assert.Equal(SituacaoRascunho.Pendente, SituacaoRascunho.Calcular(null, agora.AddHours(1), agora));
    }
}
