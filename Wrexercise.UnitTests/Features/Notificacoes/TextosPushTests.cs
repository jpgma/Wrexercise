using Wrexercise.Features.Notificacoes;

namespace Wrexercise.UnitTests.Features.Notificacoes;

public class TextosPushTests
{
    private static readonly Guid GrupoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AtividadeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData("NovaAtividade", "Ana Souza", null, 1, "Ana registrou um treino. Dá uma olhada!")]
    [InlineData("Reacao", "Ana", null, 1, "Ana reagiu ao seu treino")]
    [InlineData("Reacao", "Ana", null, 2, "Ana e mais 1 pessoa reagiram ao seu treino")]
    [InlineData("Reacao", "Ana", null, 4, "Ana e mais 3 pessoas reagiram ao seu treino")]
    [InlineData("Comentario", "Ana", null, 1, "Ana comentou em uma atividade")]
    [InlineData("Comentario", "Ana", null, 3, "3 novos comentários em uma atividade")]
    [InlineData("Disputa", "Bia", null, 1, "O treino de Bia precisa de uma decisão")]
    [InlineData("SituacaoAlterada", null, "Rejeitada", 1, "Seu treino foi rejeitado")]
    [InlineData("SituacaoAlterada", null, "EmRevisao", 1, "Seu treino entrou em revisão")]
    [InlineData("CartaRecebida", "Caio", null, 1, "Caio te mandou uma carta. Topa?")]
    [InlineData("RivalEscolhido", null, null, 1, "Alguém escolheu você como Rival da semana. Quem cumpre a meta primeiro?")]
    public void GetTexto_EspelhaOsTextosDoApp(string tipo, string? ator, string? situacao, int total, string esperado)
    {
        Assert.Equal(esperado, TextosPush.GetTexto(new AvisoPush(tipo, GrupoId, AtividadeId, ator, situacao, total)));
    }

    [Fact]
    public void GetDestino_NovaAtividadeAbreARevisao()
    {
        Assert.Equal(
            $"/grupos/{GrupoId}/revisar/{AtividadeId}",
            TextosPush.GetDestino(new AvisoPush("NovaAtividade", GrupoId, AtividadeId, "Ana", null, 1)));
    }

    [Fact]
    public void GetDestino_ReacaoAbreAAtividade()
    {
        Assert.Equal(
            $"/atividades/{AtividadeId}",
            TextosPush.GetDestino(new AvisoPush("Reacao", GrupoId, AtividadeId, "Ana", null, 1)));
    }

    [Fact]
    public void GetDestino_CartaAbreACenaDoGrupo()
    {
        Assert.Equal(
            $"/grupos/{GrupoId}/inicio",
            TextosPush.GetDestino(new AvisoPush("CartaRecebida", GrupoId, null, "Ana", null, 1)));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://web.push.apple.com/QGx", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x", true)]
    [InlineData("https://wns2-bl2p.notify.windows.com/w/?token=x", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://evilpush.apple.com.exemplo.com/x", false)]
    [InlineData("https://push.apple.com.exemplo.com/x", false)]
    [InlineData("https://127.0.0.1/x", false)]
    [InlineData("não é url", false)]
    public void IsEndpointPermitido_SoServicosDePushConhecidos(string endpoint, bool permitido)
    {
        Assert.Equal(permitido, InscricoesPush.IsEndpointPermitido(endpoint));
    }
}
