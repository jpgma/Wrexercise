using Wrexercise.Features.Jogo;
using Wrexercise.Features.Jogo.Contracts;

namespace Wrexercise.UnitTests.Features.Jogo;

/// <summary>Decisão 0075: só o próximo treino depois do aceite vale, e a ANTES DAS 8 olha o fim do treino.</summary>
public class ProximoTreinoTests
{
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    // Segunda, 23/11/2026, 15:00 no fuso do grupo: depois de ObjetivoCarta.InicioProximoTreino.
    private static readonly DateTimeOffset Envio = new(2026, 11, 23, 15, 0, 0, Fuso);
    private static readonly DateTimeOffset Aceite = Envio.AddHours(1);
    private static readonly DateTimeOffset Prazo = Envio.AddHours(48);

    private static JogadaEstado Jogada(
        string codigo = CodigoCarta.Variedade,
        int? alvo = null,
        string? fusao = null,
        DateTimeOffset? envio = null,
        DateTimeOffset? prazo = null) =>
        new(codigo, envio ?? Envio, prazo ?? Prazo, "Aceita", (envio ?? Envio).AddHours(1), alvo, "Corrida", fusao);

    /// <param name="registro">Horas depois do aceite em que o treino foi registrado: define a ordem.</param>
    private static AtividadeJogada Atividade(
        string tipo = "Musculacao",
        int dia = 24,
        int hora = 12,
        int minuto = 0,
        int duracao = 3600,
        bool verificada = true,
        int registro = 1) =>
        new(new DateTime(2026, 11, dia, hora, minuto, 0), tipo, duracao, verificada, Aceite.AddHours(registro));

    private static DateTimeOffset Em(int dia, int hora) => new(2026, 11, dia, hora, 0, 0, Fuso);

    [Fact]
    public void ProximoTreinoCumpre_Concluida() =>
        Assert.Equal(
            SituacaoJogada.Concluida,
            SituacaoJogada.Calcular(Jogada(), [Atividade()], Em(24, 13)));

    [Fact]
    public void ProximoTreinoNaoCumpre_FalhaNoRegistro_MesmoComOutroQueCumpre()
    {
        var (situacao, falhouEm) = SituacaoJogada.Avaliar(
            Jogada(),
            [Atividade(tipo: "Corrida", registro: 1), Atividade(tipo: "Musculacao", registro: 2)],
            Em(24, 13));

        Assert.Equal(SituacaoJogada.Falhou, situacao);
        Assert.Equal(Aceite.AddHours(1), falhouEm);
    }

    [Fact]
    public void ProximoTreinoPendenteQueNaoCumpre_JaFalha() =>
        Assert.Equal(
            SituacaoJogada.Falhou,
            SituacaoJogada.Calcular(Jogada(), [Atividade(tipo: "Corrida", verificada: false)], Em(24, 13)));

    [Fact]
    public void ProximoTreino_EOPrimeiroRegistrado_NaoOPrimeiroAComecar() =>
        // O de terça foi registrado depois do de quarta: a vez é a do primeiro registro.
        Assert.Equal(
            SituacaoJogada.Concluida,
            SituacaoJogada.Calcular(
                Jogada(),
                [Atividade(dia: 25, registro: 1), Atividade(tipo: "Corrida", dia: 24, registro: 2)],
                Em(25, 13)));

    [Fact]
    public void SemTreino_AindaValendo_EExpiraNoPrazo()
    {
        Assert.Equal(SituacaoJogada.Aceita, SituacaoJogada.Calcular(Jogada(), [], Em(24, 13)));
        Assert.Equal(SituacaoJogada.Expirada, SituacaoJogada.Calcular(Jogada(), [], Prazo.AddMinutes(1)));
    }

    [Theory]
    [InlineData(7, 0, 3600, true)] // termina às 8:00 em ponto
    [InlineData(6, 30, 1800, true)]
    [InlineData(7, 30, 3600, false)] // começa antes, mas termina 8:30
    [InlineData(23, 30, 3600, false)] // vira a meia-noite: não é treino da manhã
    public void AntesDas8_OlhaOFimDoTreino(int hora, int minuto, int duracao, bool cumpre) =>
        Assert.Equal(
            cumpre,
            ObjetivoCarta.Cumpre(
                Jogada(CodigoCarta.AntesDas8),
                Atividade(hora: hora, minuto: minuto, duracao: duracao)));

    [Fact]
    public void AntesDas8_JogadaAntiga_OlhaOInicio()
    {
        var antiga = Jogada(CodigoCarta.AntesDas8, envio: Em(19, 15), prazo: Em(21, 8));

        Assert.True(ObjetivoCarta.Cumpre(antiga, Atividade(dia: 20, hora: 7, minuto: 30, duracao: 3600)));
    }

    [Fact]
    public void JogadaAntiga_QualquerTreinoNoPrazoConta() =>
        Assert.Equal(
            SituacaoJogada.Concluida,
            SituacaoJogada.Calcular(
                Jogada(envio: Em(19, 15), prazo: Em(21, 15)),
                [Atividade(tipo: "Corrida", dia: 20, registro: 1), Atividade(dia: 20, registro: 2)],
                Em(20, 18)));

    [Fact]
    public void Duracao_SomaODiaDoProximoTreino() =>
        Assert.Equal(
            SituacaoJogada.Concluida,
            SituacaoJogada.Calcular(
                Jogada(CodigoCarta.Mais10Min, alvo: 4200),
                [Atividade(hora: 8, duracao: 2100, registro: 1), Atividade(hora: 18, duracao: 2100, registro: 2)],
                Em(24, 20)));

    [Fact]
    public void Duracao_DiaEmAndamento_AindaValendo() =>
        Assert.Equal(
            SituacaoJogada.Aceita,
            SituacaoJogada.Calcular(
                Jogada(CodigoCarta.Mais10Min, alvo: 4200),
                [Atividade(hora: 8, duracao: 2100)],
                Em(24, 20)));

    [Fact]
    public void Duracao_DiaAcabaSemASoma_FalhaNaMeiaNoite()
    {
        // O treino de quarta não salva: só o dia do próximo treino conta.
        var (situacao, falhouEm) = SituacaoJogada.Avaliar(
            Jogada(CodigoCarta.Mais10Min, alvo: 4200),
            [Atividade(dia: 24, duracao: 2100, registro: 21), Atividade(dia: 25, hora: 7, duracao: 4200, registro: 40)],
            Em(25, 9));

        Assert.Equal(SituacaoJogada.Falhou, situacao);
        Assert.Equal(Em(25, 0), falhouEm);
    }

    [Fact]
    public void Duracao_DiaAcabaDepoisDoPrazo_Expirada() =>
        Assert.Equal(
            SituacaoJogada.Expirada,
            SituacaoJogada.Calcular(
                Jogada(CodigoCarta.Mais10Min, alvo: 4200),
                [Atividade(dia: 25, hora: 10, duracao: 2100)],
                Em(26, 0)));

    [Fact]
    public void Fusao_ProximoTreinoPrecisaCumprirOsDois() =>
        Assert.Equal(
            SituacaoJogada.Falhou,
            SituacaoJogada.Calcular(
                Jogada(CodigoCarta.AntesDas8, fusao: CodigoCarta.Variedade),
                [Atividade(tipo: "Corrida", hora: 6, registro: 1), Atividade(tipo: "Musculacao", hora: 6, dia: 25, registro: 2)],
                Em(25, 9)));

    [Fact]
    public void ConcluiAgora_SoOProximoTreino()
    {
        var primeiro = Guid.NewGuid();
        var segundo = Guid.NewGuid();
        List<(Guid, AtividadeJogada)> candidatas =
        [
            (primeiro, Atividade(tipo: "Corrida", verificada: false, registro: 1)),
            (segundo, Atividade(registro: 2)),
        ];

        Assert.False(JogadasConcluidas.ConcluiAgora(Jogada(), segundo, candidatas));
    }

    [Fact]
    public void ConcluiAgora_ProximoTreinoVerificado()
    {
        var id = Guid.NewGuid();

        Assert.True(JogadasConcluidas.ConcluiAgora(Jogada(), id, [(id, Atividade())]));
    }
}
