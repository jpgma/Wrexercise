using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wrexercise.Infrastructure.Push;

namespace Wrexercise.UnitTests.Infrastructure.Push;

public class WebPushCriptografiaTests
{
    // RFC 8291, seção 5 e apêndice A.
    private const string ChavePrivadaServidor = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string ChavePublicaServidor = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string ChavePublicaAparelho = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string Segredo = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";

    private const string MensagemEsperada =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml"
        + "mlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPT"
        + "pK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Fact]
    public void Cifrar_ReproduzOExemploDaRfc8291()
    {
        var parametros = WebPushCriptografia.ParametrosPublicos(Base64Url.DecodeFromChars(ChavePublicaServidor));
        parametros.D = Base64Url.DecodeFromChars(ChavePrivadaServidor);
        using var servidor = ECDiffieHellman.Create(parametros);

        var mensagem = WebPushCriptografia.Cifrar(
            "When I grow up, I want to be a watermelon"u8,
            Base64Url.DecodeFromChars(ChavePublicaAparelho),
            Base64Url.DecodeFromChars(Segredo),
            servidor,
            Base64Url.DecodeFromChars(Salt));

        Assert.Equal(MensagemEsperada, Base64Url.EncodeToString(mensagem));
    }

    [Fact]
    public void Cifrar_UsaChaveESaltNovosACadaMensagem()
    {
        var aparelho = Base64Url.DecodeFromChars(ChavePublicaAparelho);
        var segredo = Base64Url.DecodeFromChars(Segredo);

        var primeira = WebPushCriptografia.Cifrar("oi"u8, aparelho, segredo);
        var segunda = WebPushCriptografia.Cifrar("oi"u8, aparelho, segredo);

        Assert.NotEqual(primeira, segunda);
        Assert.Equal(16 + 4 + 1 + 65 + 2 + 1 + 16, primeira.Length);
    }

    [Fact]
    public void CriarJwt_AssinaComAChaveVapid()
    {
        var (publica, privada) = WebPushCriptografia.GerarChaves();
        using var chave = WebPushCriptografia.ImportarChaveVapid(publica, privada);
        var expira = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        var jwt = WebPushCriptografia.CriarJwt(chave, "https://fcm.googleapis.com", "https://wx.exemplo.com", expira);

        var partes = jwt.Split('.');
        Assert.Equal(3, partes.Length);
        using var verificador = ECDsa.Create(WebPushCriptografia.ParametrosPublicos(Base64Url.DecodeFromChars(publica)));
        Assert.True(verificador.VerifyData(
            Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}"),
            Base64Url.DecodeFromChars(partes[2]),
            HashAlgorithmName.SHA256));

        using var cabecalho = JsonDocument.Parse(Base64Url.DecodeFromChars(partes[0]));
        Assert.Equal("ES256", cabecalho.RootElement.GetProperty("alg").GetString());
        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(partes[1]));
        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(1_800_000_000, claims.RootElement.GetProperty("exp").GetInt64());
        Assert.Equal("https://wx.exemplo.com", claims.RootElement.GetProperty("sub").GetString());
    }

    [Fact]
    public void IsInscricaoValida_AceitaChaveDaCurvaESegredoDe16Bytes()
    {
        Assert.True(WebPushCriptografia.IsInscricaoValida(ChavePublicaAparelho, Segredo));
    }

    [Theory]
    [InlineData("não é base64", Segredo)]
    [InlineData(ChavePublicaAparelho, "curto")]
    [InlineData(Segredo, Segredo)]
    // Ponto fora da curva: a última coordenada alterada.
    [InlineData("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw0", Segredo)]
    public void IsInscricaoValida_RecusaChavesInvalidas(string p256dh, string auth)
    {
        Assert.False(WebPushCriptografia.IsInscricaoValida(p256dh, auth));
    }

    [Fact]
    public void ImportarChaveVapid_RecusaChaveMalFormada()
    {
        Assert.Throws<InvalidOperationException>(() => WebPushCriptografia.ImportarChaveVapid("abc", "def"));
    }
}
