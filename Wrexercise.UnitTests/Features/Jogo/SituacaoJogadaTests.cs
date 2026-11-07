using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class SituacaoJogadaTests
{
    private static readonly DateTimeOffset Envio = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Prazo = Envio.AddHours(48);
    private static readonly DateTimeOffset Antes = Envio.AddHours(1);
    private static readonly DateTimeOffset Depois = Prazo.AddHours(1);

    private static JogadaEstado Jogada(
        string codigo = CodigoCarta.Variedade,
        string? resposta = null,
        DateTimeOffset? respondidaEm = null,
        int? alvo = null,
        string? evitar = "Corrida") =>
        new(codigo, Envio, Prazo, resposta, resposta is null ? null : respondidaEm ?? Antes, alvo, evitar);

    // Dias de novembro de 2026: 6 é sexta, 7 é sábado, 8 é domingo e 9 é segunda.
    private static AtividadeJogada Atividade(
        string tipo = "Musculacao",
        bool verificada = true,
        int duracao = 3600,
        int dia = 3,
        int hora = 12) =>
        new(new DateTime(2026, 11, dia, hora, 0, 0), tipo, duracao, verificada);

    [Fact]
    public void SemResposta_AntesDoPrazo_Enviada() =>
        Assert.Equal(SituacaoJogada.Enviada, SituacaoJogada.Calcular(Jogada(), [], Antes));

    [Fact]
    public void SemResposta_DepoisDoPrazo_Expirada() =>
        Assert.Equal(SituacaoJogada.Expirada, SituacaoJogada.Calcular(Jogada(), [], Depois));

    [Fact]
    public void Recusada_ContinuaRecusada() =>
        Assert.Equal(
            SituacaoJogada.Recusada,
            SituacaoJogada.Calcular(Jogada(resposta: SituacaoJogada.RespostaRecusada), [Atividade()], Antes));

    [Fact]
    public void Aceita_SemAtividade_Aceita() =>
        Assert.Equal(SituacaoJogada.Aceita, SituacaoJogada.Calcular(Jogada(resposta: "Aceita"), [], Antes));

    [Fact]
    public void Aceita_SemAtividadeAteOPrazo_Expirada() =>
        Assert.Equal(SituacaoJogada.Expirada, SituacaoJogada.Calcular(Jogada(resposta: "Aceita"), [], Depois));

    [Fact]
    public void Aceita_ComAtividadePendente_AguardaVerificacaoMesmoDepoisDoPrazo() =>
        Assert.Equal(
            SituacaoJogada.AguardandoVerificacao,
            SituacaoJogada.Calcular(Jogada(resposta: "Aceita"), [Atividade(verificada: false)], Depois));

    [Fact]
    public void Aceita_ComAtividadeVerificada_Concluida() =>
        Assert.Equal(
            SituacaoJogada.Concluida,
            SituacaoJogada.Calcular(Jogada(resposta: "Aceita"), [Atividade(verificada: false), Atividade()], Antes));

    [Fact]
    public void Aceita_AtividadeQueNaoCumpre_NaoConta() =>
        Assert.Equal(
            SituacaoJogada.Aceita,
            SituacaoJogada.Calcular(Jogada(resposta: "Aceita"), [Atividade(tipo: "Corrida")], Antes));

    [Fact]
    public void AceitaDepoisDoPrazo_Expirada() =>
        Assert.Equal(
            SituacaoJogada.Expirada,
            SituacaoJogada.Calcular(Jogada(resposta: "Aceita", respondidaEm: Depois), [Atividade()], Depois));

    [Theory]
    [InlineData(SituacaoJogada.Enviada, true, true)]
    [InlineData(SituacaoJogada.Aceita, true, true)]
    [InlineData(SituacaoJogada.AguardandoVerificacao, false, true)]
    [InlineData(SituacaoJogada.Concluida, false, false)]
    [InlineData(SituacaoJogada.Recusada, false, false)]
    [InlineData(SituacaoJogada.Expirada, false, false)]
    public void AtivaEAberta(string situacao, bool ativa, bool aberta)
    {
        Assert.Equal(ativa, SituacaoJogada.IsAtiva(situacao));
        Assert.Equal(aberta, SituacaoJogada.IsAberta(situacao));
    }

    [Theory]
    [InlineData(4199, false)]
    [InlineData(4200, true)]
    public void Duracao_PrecisaDoAlvo(int duracao, bool cumpre) =>
        Assert.Equal(cumpre, ObjetivoCarta.Cumpre(Jogada(CodigoCarta.Mais10Min, alvo: 4200), Atividade(duracao: duracao)));

    [Fact]
    public void Variedade_SemTipoAEvitar_QualquerTipoVale() =>
        Assert.True(ObjetivoCarta.Cumpre(Jogada(evitar: null), Atividade(tipo: "Corrida")));

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void FimDeSemana_NoHorarioLocal(int dia, bool cumpre) =>
        Assert.Equal(cumpre, ObjetivoCarta.Cumpre(Jogada(CodigoCarta.FimDeSemana), Atividade(dia: dia)));

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void AntesDas8_NoHorarioLocal(int hora, bool cumpre) =>
        Assert.Equal(cumpre, ObjetivoCarta.Cumpre(Jogada(CodigoCarta.AntesDas8), Atividade(hora: hora)));
}
