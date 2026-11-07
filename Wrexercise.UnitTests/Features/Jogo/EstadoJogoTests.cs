using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class EstadoJogoTests
{
    private static readonly Guid Eu = Guid.NewGuid();
    private static readonly Guid Colega = Guid.NewGuid();
    private static readonly DateTimeOffset Envio = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);

    private static CartaComprada Carta(string codigo) => new(Guid.NewGuid(), codigo, 30, Envio);

    private static JogadaCarregada Jogada(
        Guid remetente,
        Guid destinatario,
        Guid cartaId,
        string situacao,
        Guid? cartaRespostaId = null,
        Guid? origemId = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            remetente,
            destinatario,
            cartaId,
            CodigoCarta.Variedade,
            30,
            cartaRespostaId,
            null,
            origemId,
            new JogadaEstado(CodigoCarta.Variedade, Envio, Envio.AddHours(48), null, null, null, null),
            null,
            null,
            situacao);

    private static EstadoJogo Estado(IReadOnlyList<CartaComprada> cartas, IReadOnlyList<JogadaCarregada> jogadas) =>
        new(Eu, new EnergiaGanha(0, 0, 0, 0, 0), cartas, jogadas);

    [Fact]
    public void Resposta_UsadaEGasta_MesmoSemConcluir()
    {
        var provocacao = Guid.NewGuid();
        var resposta = Carta(CodigoCarta.Aposta);
        var estado = Estado(
            [resposta],
            [Jogada(Colega, Eu, provocacao, SituacaoJogada.Expirada, cartaRespostaId: resposta.Id)]);

        Assert.True(estado.IsGasta(resposta.Id));
        Assert.Empty(estado.Mao);
    }

    [Fact]
    public void Resposta_NaoUsada_FicaNaMao()
    {
        var resposta = Carta(CodigoCarta.PrazoExtra);
        var estado = Estado([resposta], []);

        Assert.False(estado.IsGasta(resposta.Id));
        Assert.Single(estado.Mao);
    }

    [Fact]
    public void DesafioDuplo_ConcluidoNaoGastaAProvocacaoDeQuemEnviou()
    {
        // Eu enviei; o colega devolveu com DESAFIO DUPLO, e eu cumpri a devolvida.
        var minha = Carta(CodigoCarta.Variedade);
        var original = Jogada(Eu, Colega, minha.Id, SituacaoJogada.Expirada);
        var devolvida = Jogada(Colega, Eu, minha.Id, SituacaoJogada.Concluida, origemId: original.Id);

        var estado = Estado([minha], [devolvida, original]);

        Assert.False(estado.IsGasta(minha.Id));
        Assert.False(estado.IsEmJogo(minha.Id));
        Assert.Single(estado.Mao);
    }

    [Fact]
    public void DesafioDuplo_AbertaNaoDeixaACartaEmJogo()
    {
        var minha = Carta(CodigoCarta.Variedade);
        var original = Jogada(Eu, Colega, minha.Id, SituacaoJogada.Recusada);
        var devolvida = Jogada(Colega, Eu, minha.Id, SituacaoJogada.Aceita, origemId: original.Id);

        Assert.False(Estado([minha], [devolvida, original]).IsEmJogo(minha.Id));
    }

    [Fact]
    public void Provocacao_ConcluidaEGasta()
    {
        var minha = Carta(CodigoCarta.Variedade);
        var estado = Estado([minha], [Jogada(Eu, Colega, minha.Id, SituacaoJogada.Concluida)]);

        Assert.True(estado.IsGasta(minha.Id));
    }
}
