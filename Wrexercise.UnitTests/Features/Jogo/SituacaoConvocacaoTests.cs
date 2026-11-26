using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class SituacaoConvocacaoTests
{
    private static readonly Guid Anfitriao = Guid.NewGuid();
    private static readonly Guid Bia = Guid.NewGuid();
    private static readonly Guid Caio = Guid.NewGuid();

    /// <summary>Depois do início das regras da FADIGA, para que os gatilhos contem.</summary>
    private static readonly DateTimeOffset Criada = Fadiga.InicioRegras2.AddDays(2);

    private static readonly DateTimeOffset Prazo = Criada + SituacaoConvocacao.Duracao;

    private static DateTimeOffset Hora(double horas) => Criada.AddHours(horas);

    private static ParticipanteConvocacao P(
        Guid id, string? decisao = null, double? decididaHoras = null, double? removidaHoras = null) =>
        new(id, id == Anfitriao ? "Ana" : "Bia", decisao,
            decididaHoras is { } d ? Hora(d) : null,
            removidaHoras is { } r ? Hora(r) : null);

    private static AtividadeConvocacao A(
        Guid usuario, string situacao, double? verificadaHoras = null, double? resolvidaHoras = null) =>
        new(usuario, situacao,
            verificadaHoras is { } v ? Hora(v) : null,
            resolvidaHoras is { } r ? Hora(r) : null);

    private static ConvocacaoCarregada Avaliar(
        IReadOnlyList<ParticipanteConvocacao> outros,
        IReadOnlyList<AtividadeConvocacao> atividades,
        DateTimeOffset agora,
        int preco = 150)
    {
        var participantes = new List<ParticipanteConvocacao> { P(Anfitriao, "Aceita", 0) };
        participantes.AddRange(outros);
        return SituacaoConvocacao.Avaliar(
            new ConvocacaoFatos(Guid.NewGuid(), Guid.NewGuid(), Anfitriao, Guid.NewGuid(), CodigoCarta.TodoMundo,
                preco, "Ciclismo", Criada, Prazo, participantes, atividades),
            agora);
    }

    private static string Situacao(ConvocacaoCarregada c, Guid usuario) => c.Get(usuario)!.Situacao;

    [Fact]
    public void Anfitriao_ParticipaDesdeOInicio() =>
        Assert.Equal(SituacaoConvocacao.Participando, Situacao(Avaliar([P(Bia)], [], Hora(1)), Anfitriao));

    [Fact]
    public void SemResposta_FicaPendenteEExpiraNoPrazo()
    {
        Assert.Equal(SituacaoConvocacao.Pendente, Situacao(Avaliar([P(Bia)], [], Hora(1)), Bia));

        var encerrada = Avaliar([P(Bia)], [], Prazo.AddHours(1));
        Assert.Equal(SituacaoConvocacao.Expirada, Situacao(encerrada, Bia));
        Assert.Equal(new GatilhoFadiga(Prazo, MotivoFadiga.Expiracao, ConvocacaoId: encerrada.Id), encerrada.GetGatilho(Bia));
    }

    [Fact]
    public void TreinoVerificado_Conclui()
    {
        var c = Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "Verificada", 5)], Hora(6));

        Assert.Equal(SituacaoConvocacao.Concluida, Situacao(c, Bia));
        Assert.Equal(Hora(5), c.GetCumprida(Bia));
        Assert.Equal(30, c.GetRecompensa(Bia));
        Assert.Null(c.GetGatilho(Bia));
    }

    [Fact]
    public void TreinoAntesDoAceite_ContaQuandoAceitaNoPrazo()
    {
        var c = Avaliar([P(Bia, "Aceita", 20)], [A(Bia, "Verificada", 5)], Hora(21));

        Assert.Equal(SituacaoConvocacao.Concluida, Situacao(c, Bia));
        Assert.Equal(Hora(20), c.GetCumprida(Bia));
    }

    [Fact]
    public void TreinoVerificadoSemAceitar_NaoConclui() =>
        Assert.Equal(SituacaoConvocacao.Pendente, Situacao(Avaliar([P(Bia)], [A(Bia, "Verificada", 5)], Hora(6)), Bia));

    [Fact]
    public void TreinoPendente_EsperaVerificacaoMesmoDepoisDoPrazo()
    {
        var antes = Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "AguardandoFeedback")], Hora(6));
        Assert.Equal(SituacaoConvocacao.AguardandoVerificacao, Situacao(antes, Bia));
        Assert.Equal(0, antes.GetRecompensa(Bia));

        var depois = Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "EmRevisao")], Prazo.AddDays(1));
        Assert.Equal(SituacaoConvocacao.AguardandoVerificacao, Situacao(depois, Bia));
        Assert.Equal(SituacaoConvocacao.AguardandoVerificacao, depois.Situacao);
        Assert.Null(depois.GetGatilho(Bia));
    }

    [Fact]
    public void VerificacaoDepoisDoPrazo_Conclui()
    {
        var c = Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "Verificada", 24 * 8)], Prazo.AddDays(2));

        Assert.Equal(SituacaoConvocacao.Concluida, Situacao(c, Bia));
        Assert.Equal(SituacaoConvocacao.Encerrada, c.Situacao);
    }

    [Fact]
    public void Rejeitada_NaoConcluiMasOutroTreinoAinda()
    {
        Assert.Equal(
            SituacaoConvocacao.Participando,
            Situacao(Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "Rejeitada", resolvidaHoras: 10)], Hora(11)), Bia));

        Assert.Equal(
            SituacaoConvocacao.Concluida,
            Situacao(Avaliar([P(Bia, "Aceita", 1)],
                [A(Bia, "Rejeitada", resolvidaHoras: 10), A(Bia, "Verificada", 30)], Hora(31)), Bia));
    }

    [Fact]
    public void AceitaSemCumprir_FalhaNoPrazo()
    {
        var c = Avaliar([P(Bia, "Aceita", 1)], [], Prazo.AddHours(1));

        Assert.Equal(SituacaoConvocacao.Falhou, Situacao(c, Bia));
        Assert.Equal(new GatilhoFadiga(Prazo, MotivoFadiga.Falha, ConvocacaoId: c.Id), c.GetGatilho(Bia));
    }

    [Fact]
    public void RejeitadaDepoisDoPrazo_FalhaNaRejeicao()
    {
        var c = Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "Rejeitada", resolvidaHoras: 24 * 9)], Prazo.AddDays(3));

        Assert.Equal(SituacaoConvocacao.Falhou, Situacao(c, Bia));
        Assert.Equal(Hora(24 * 9), c.GetGatilho(Bia)!.Em);
    }

    [Fact]
    public void Recusa_DaFadigaDeRecusaNaHora()
    {
        var c = Avaliar([P(Bia, "Recusada", 3)], [], Hora(4));

        Assert.Equal(SituacaoConvocacao.Recusada, Situacao(c, Bia));
        Assert.Equal(new GatilhoFadiga(Hora(3), MotivoFadiga.Recusa, ConvocacaoId: c.Id), c.GetGatilho(Bia));
    }

    [Fact]
    public void Desistir_DaFadigaDeFalha()
    {
        var c = Avaliar([P(Bia, "Desistiu", 3)], [], Hora(4));

        Assert.Equal(SituacaoConvocacao.Desistiu, Situacao(c, Bia));
        Assert.Equal(MotivoFadiga.Falha, c.GetGatilho(Bia)!.Motivo);
    }

    [Fact]
    public void Anfitriao_NaoTemFadigaMesmoSemCumprir() =>
        Assert.Null(Avaliar([P(Bia)], [], Prazo.AddHours(1)).GetGatilho(Anfitriao));

    [Fact]
    public void SaiuDoGrupoAntesDeConcluir_FicaForaSemFadiga()
    {
        var c = Avaliar([P(Bia, "Aceita", 1, removidaHoras: 5)], [A(Bia, "Verificada", 10)], Hora(11));

        Assert.Equal(SituacaoConvocacao.ForaDoGrupo, Situacao(c, Bia));
        Assert.Equal(0, c.GetRecompensa(Bia));
        Assert.Null(c.GetGatilho(Bia));
    }

    [Fact]
    public void SaiuDoGrupoDepoisDeConcluir_MantemARecompensa()
    {
        var c = Avaliar([P(Bia, "Aceita", 1, removidaHoras: 20)], [A(Bia, "Verificada", 10)], Hora(21));

        Assert.Equal(SituacaoConvocacao.Concluida, Situacao(c, Bia));
        Assert.Equal(30, c.GetRecompensa(Bia));
    }

    [Fact]
    public void VariosTreinos_UmaRecompensaSo()
    {
        var c = Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "Verificada", 5), A(Bia, "Verificada", 9)], Hora(10));

        Assert.Equal(30, c.GetRecompensa(Bia));
        Assert.Equal(Hora(5), c.GetCumprida(Bia));
    }

    [Fact]
    public void Anfitriao_GanhaATrintaQuandoCumpre()
    {
        var c = Avaliar([P(Bia)], [A(Anfitriao, "Verificada", 5)], Hora(6));

        Assert.Equal(30, c.GetRecompensa(Anfitriao));
        Assert.Equal(30, c.GetEnergia(Anfitriao));
    }

    [Fact]
    public void Devolucao_ParteDoPrecoPorResultadoFinal()
    {
        // Dois outros convidados: 75 cada.
        Assert.Equal(0, Avaliar([P(Bia), P(Caio)], [], Hora(1)).Devolucao);
        Assert.Equal(75, Avaliar([P(Bia, "Recusada", 1), P(Caio)], [], Hora(2)).Devolucao);
        Assert.Equal(150, Avaliar([P(Bia, "Recusada", 1), P(Caio, "Aceita", 1)], [A(Caio, "Verificada", 4)], Hora(5)).Devolucao);
    }

    [Fact]
    public void Devolucao_ContaTodoResultadoFinal()
    {
        var c = Avaliar(
            [P(Bia, "Aceita", 1), P(Caio, "Desistiu", 2), P(Guid.NewGuid()), P(Guid.NewGuid(), removidaHoras: 3)],
            [],
            Prazo.AddHours(1));

        // Falhou, Desistiu, Expirada e ForaDoGrupo: todos finais, o preço volta inteiro.
        Assert.Equal(150, c.Devolucao);
        Assert.Equal(150, c.GetEnergia(Anfitriao));
        Assert.Equal(0, c.GetEnergia(Bia));
    }

    [Fact]
    public void Devolucao_PendenteNaoConta() =>
        Assert.Equal(0, Avaliar([P(Bia, "Aceita", 1)], [A(Bia, "AguardandoFeedback")], Prazo.AddHours(1)).Devolucao);

    [Theory]
    [InlineData(2, 1, 75)]
    [InlineData(2, 2, 150)]
    [InlineData(9, 1, 16)]
    [InlineData(9, 8, 128)]
    [InlineData(9, 9, 150)]
    [InlineData(10, 9, 135)]
    [InlineData(1, 0, 0)]
    [InlineData(0, 0, 0)]
    public void GetDevolucao_PisoESobraNoUltimo(int outros, int finais, int esperado) =>
        Assert.Equal(esperado, SituacaoConvocacao.GetDevolucao(150, outros, finais));

    [Fact]
    public void Fadiga_ConvocacaoRecusadaDaCargasECumpridaTiraUma()
    {
        var recusada = Avaliar([P(Bia, "Recusada", 1)], [], Hora(2));
        Assert.Equal(Fadiga.Cargas, Fadiga.Calcular([], [], Bia, [recusada]).Restantes);

        var cumprida = Avaliar([P(Bia, "Aceita", 3)], [A(Bia, "Verificada", 5)], Hora(6));
        Assert.Equal(Fadiga.Cargas - 1, Fadiga.Calcular([], [], Bia, [recusada, cumprida]).Restantes);
    }

    [Fact]
    public void Situacao_AtivaAteOPrazo()
    {
        Assert.Equal(SituacaoConvocacao.Ativa, Avaliar([P(Bia)], [], Prazo.AddTicks(-1)).Situacao);
        Assert.Equal(SituacaoConvocacao.Encerrada, Avaliar([P(Bia)], [], Prazo).Situacao);
    }

    [Theory]
    [InlineData(null, "Aceita", false, true)]
    [InlineData(null, "Recusada", false, true)]
    [InlineData(null, "Desistiu", false, false)]
    [InlineData("Aceita", "Desistiu", false, true)]
    [InlineData("Aceita", "Desistiu", true, false)]
    [InlineData("Recusada", "Aceita", false, false)]
    [InlineData("Desistiu", "Aceita", false, false)]
    [InlineData("Aceita", "Aceita", false, false)]
    public void Decisao_RecusarESairSaoFinais(string? atual, string nova, bool anfitriao, bool permitida) =>
        Assert.Equal(permitida, ResponderConvocacao.IsPermitida(atual, nova, anfitriao));
}
