using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class EstadoJogoTests
{
    private static readonly Guid Eu = Guid.NewGuid();
    private static readonly Guid Colega = Guid.NewGuid();
    private static readonly DateTimeOffset Envio = new(2026, 12, 1, 15, 0, 0, TimeSpan.Zero);

    private static CartaComprada Carta(string codigo) => new(Guid.NewGuid(), codigo, 30, Envio);

    private static JogadaCarregada Jogada(
        Guid remetente,
        Guid destinatario,
        Guid cartaId,
        string situacao,
        Guid? cartaRespostaId = null,
        Guid? origemId = null,
        Guid? cartaFusaoId = null,
        DateTimeOffset? envio = null) =>
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
            new JogadaEstado(
                CodigoCarta.Variedade,
                envio ?? Envio,
                (envio ?? Envio).AddHours(48),
                situacao == SituacaoJogada.Recusada ? "Recusada" : null,
                situacao == SituacaoJogada.Recusada ? (envio ?? Envio).AddHours(1) : null,
                null,
                null),
            null,
            null,
            situacao,
            cartaFusaoId,
            cartaFusaoId is null ? 0 : 40);

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
    public void DesafioDuplo_ACartaOriginalJaFoiGastaPelaJogadaOriginal()
    {
        // Eu enviei; o colega devolveu com DESAFIO DUPLO, e eu cumpri a devolvida.
        var minha = Carta(CodigoCarta.Variedade);
        var original = Jogada(Eu, Colega, minha.Id, SituacaoJogada.Expirada);
        var devolvida = Jogada(Colega, Eu, minha.Id, SituacaoJogada.Concluida, origemId: original.Id);

        var estado = Estado([minha], [devolvida, original]);

        Assert.True(estado.IsGasta(minha.Id));
        Assert.Empty(estado.Mao);
    }

    [Fact]
    public void DesafioDuplo_DevolvidaNaoGastaACartaDeQuemNaoEODono()
    {
        // A Jogada devolvida sozinha não gasta nada: a carta é da Jogada original.
        var minha = Carta(CodigoCarta.Variedade);
        var devolvida = Jogada(Colega, Eu, minha.Id, SituacaoJogada.Aceita, origemId: Guid.NewGuid());

        Assert.False(Estado([minha], [devolvida]).IsGasta(minha.Id));
    }

    [Theory]
    [InlineData(SituacaoJogada.Enviada)]
    [InlineData(SituacaoJogada.Aceita)]
    [InlineData(SituacaoJogada.AguardandoVerificacao)]
    [InlineData(SituacaoJogada.Concluida)]
    [InlineData(SituacaoJogada.Recusada)]
    [InlineData(SituacaoJogada.Expirada)]
    public void Provocacao_JogadaEGasta_SejaQualForOResultado(string situacao)
    {
        var minha = Carta(CodigoCarta.Variedade);
        var estado = Estado([minha], [Jogada(Eu, Colega, minha.Id, situacao)]);

        Assert.True(estado.IsGasta(minha.Id));
        Assert.Empty(estado.Mao);
    }

    [Theory]
    [InlineData(SituacaoJogada.Recusada)]
    [InlineData(SituacaoJogada.Expirada)]
    public void Provocacao_RecusadaOuExpiradaAntesDoInicio_ContinuaNaMao(string situacao)
    {
        var minha = Carta(CodigoCarta.Variedade);
        var antes = EstadoJogo.InicioCartaPermanente.AddDays(-7);
        var estado = Estado([minha], [Jogada(Eu, Colega, minha.Id, situacao, envio: antes)]);

        Assert.False(estado.IsGasta(minha.Id));
        Assert.Single(estado.Mao);
    }

    [Fact]
    public void Provocacao_AbertaNoInicio_SegueARegraNova()
    {
        // Enviada antes da mudança, mas o prazo acaba depois: a carta não volta.
        var minha = Carta(CodigoCarta.Variedade);
        var envio = EstadoJogo.InicioCartaPermanente.AddHours(-24);
        var estado = Estado([minha], [Jogada(Eu, Colega, minha.Id, SituacaoJogada.Expirada, envio: envio)]);

        Assert.True(estado.IsGasta(minha.Id));
    }

    [Theory]
    [InlineData(SituacaoJogada.Aceita)]
    [InlineData(SituacaoJogada.Concluida)]
    [InlineData(SituacaoJogada.Recusada)]
    [InlineData(SituacaoJogada.Expirada)]
    public void Fusao_AsDuasCartasSaoGastasNoArremesso(string situacao)
    {
        var primeira = Carta(CodigoCarta.Variedade);
        var segunda = Carta(CodigoCarta.AntesDas8);
        var estado = Estado(
            [primeira, segunda],
            [Jogada(Eu, Colega, primeira.Id, situacao, cartaFusaoId: segunda.Id)]);

        Assert.True(estado.IsGasta(primeira.Id));
        Assert.True(estado.IsGasta(segunda.Id));
        Assert.Empty(estado.Mao);
    }

    [Fact]
    public void Fusao_RecompensaSomaAsDuasCartas()
    {
        var jogada = Jogada(Eu, Colega, Guid.NewGuid(), SituacaoJogada.Concluida, cartaFusaoId: Guid.NewGuid());

        // 30 + 40 = 70: quem cumpre ganha a soma mais 20%; quem enviou recebe a soma de volta, sem o bônus.
        Assert.Equal(84, CalculoEnergia.GetRecompensa(jogada.ToRecompensa(comoDestinatario: true)));
        Assert.Equal(70, CalculoEnergia.GetRecompensa(jogada.ToRecompensa(comoDestinatario: false)));
    }

    // Decisão 0087: a carta recusada, não cumprida ou expirada devolve metade do preço a quem enviou.

    [Theory]
    [InlineData(SituacaoJogada.Recusada, 15)]
    [InlineData(SituacaoJogada.Falhou, 15)]
    [InlineData(SituacaoJogada.Expirada, 15)]
    [InlineData(SituacaoJogada.Enviada, 0)]
    [InlineData(SituacaoJogada.Aceita, 0)]
    [InlineData(SituacaoJogada.AguardandoVerificacao, 0)]
    [InlineData(SituacaoJogada.Concluida, 0)]
    public void Devolucao_MetadeSoQuandoNaoCumprida(string situacao, int devolucao) =>
        Assert.Equal(devolucao, Jogada(Eu, Colega, Guid.NewGuid(), situacao).Devolucao);

    [Fact]
    public void Devolucao_Fusao_MetadeDaSoma_ArredondadaParaBaixo()
    {
        // 30 + 40 = 70 → 35. Com 35 + 40 = 75 → 37.
        Assert.Equal(35, Jogada(Eu, Colega, Guid.NewGuid(), SituacaoJogada.Falhou, cartaFusaoId: Guid.NewGuid()).Devolucao);
        Assert.Equal(37, CalculoEnergia.GetDevolucao(75));
        Assert.Equal(22, CalculoEnergia.GetDevolucao(45));
    }

    [Fact]
    public void Devolucao_DesafioDuplo_NaoDevolve() =>
        Assert.Equal(0, Jogada(Colega, Eu, Guid.NewGuid(), SituacaoJogada.Falhou, origemId: Guid.NewGuid()).Devolucao);

    [Theory]
    [InlineData(SituacaoJogada.Recusada)]
    [InlineData(SituacaoJogada.Expirada)]
    public void Devolucao_CartaQueVoltouPelaRegraAntiga_NaoDevolve(string situacao)
    {
        var antes = EstadoJogo.InicioCartaPermanente.AddDays(-7);

        Assert.Equal(0, Jogada(Eu, Colega, Guid.NewGuid(), situacao, envio: antes).Devolucao);
    }

    [Fact]
    public void Devolucao_EntraNoTotalDaEnergia()
    {
        var ganha = CalculoEnergia.Calcular([], [], devolucoes: 15);

        Assert.Equal(15, ganha.Devolucoes);
        Assert.Equal(15, ganha.Total);
    }
}
