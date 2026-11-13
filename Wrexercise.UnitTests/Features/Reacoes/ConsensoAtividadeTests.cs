using Wrexercise.Features.Reacoes;

namespace Wrexercise.UnitTests.Features.Reacoes;

public class ConsensoAtividadeTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(10, 5)]
    public void CalculateRejeicoesNecessarias_SegueFormula(int revisores, int esperado) =>
        Assert.Equal(esperado, ConsensoAtividade.CalculateRejeicoesNecessarias(revisores));

    [Fact]
    public void DeterminarSituacao_SemRevisores_Verificada() =>
        Assert.Equal(
            SituacaoVerificacao.Verificada,
            ConsensoAtividade.DeterminarSituacao(
                0,
                new ContagensReacao(0, 0, 0),
                janelaEncerrada: false,
                SituacaoVerificacao.AguardandoFeedback));

    [Fact]
    public void DeterminarSituacao_TodosKudos_Verificada() =>
        Assert.Equal(
            SituacaoVerificacao.Verificada,
            ConsensoAtividade.DeterminarSituacao(
                2,
                new ContagensReacao(2, 0, 0),
                janelaEncerrada: false,
                SituacaoVerificacao.AguardandoFeedback));

    [Fact]
    public void DeterminarSituacao_Suspeito_EmRevisao() =>
        Assert.Equal(
            SituacaoVerificacao.EmRevisao,
            ConsensoAtividade.DeterminarSituacao(
                3,
                new ContagensReacao(1, 1, 0),
                janelaEncerrada: false,
                SituacaoVerificacao.AguardandoFeedback));

    [Fact]
    public void DeterminarSituacao_RejeicaoAtingeLimite_Rejeitada() =>
        Assert.Equal(
            SituacaoVerificacao.Rejeitada,
            ConsensoAtividade.DeterminarSituacao(
                2,
                new ContagensReacao(0, 0, 2),
                janelaEncerrada: false,
                SituacaoVerificacao.AguardandoFeedback));

    [Fact]
    public void DeterminarSituacao_PrazoEncerradoSemNegativa_Verificada() =>
        Assert.Equal(
            SituacaoVerificacao.Verificada,
            ConsensoAtividade.DeterminarSituacao(
                3,
                new ContagensReacao(1, 0, 0),
                janelaEncerrada: true,
                SituacaoVerificacao.AguardandoFeedback));

    [Fact]
    public void DeterminarSituacao_EmRevisao_ReacoesContinuamAteOLimite() =>
        Assert.Equal(
            SituacaoVerificacao.Rejeitada,
            ConsensoAtividade.DeterminarSituacao(
                4,
                new ContagensReacao(0, 1, 2),
                janelaEncerrada: false,
                SituacaoVerificacao.EmRevisao));

    [Fact]
    public void DeterminarSituacao_EmRevisao_AbaixoDoLimitePermanece() =>
        Assert.Equal(
            SituacaoVerificacao.EmRevisao,
            ConsensoAtividade.DeterminarSituacao(
                4,
                new ContagensReacao(1, 1, 1),
                janelaEncerrada: false,
                SituacaoVerificacao.EmRevisao));

    [Fact]
    public void DeterminarSituacao_RetirarUnicaNegativa_VoltaAAguardar() =>
        Assert.Equal(
            SituacaoVerificacao.AguardandoFeedback,
            ConsensoAtividade.DeterminarSituacao(
                3,
                new ContagensReacao(1, 0, 0),
                janelaEncerrada: false,
                SituacaoVerificacao.EmRevisao));

    [Fact]
    public void DeterminarSituacao_RetirarUnicaNegativaComTodosReagindo_Verificada() =>
        Assert.Equal(
            SituacaoVerificacao.Verificada,
            ConsensoAtividade.DeterminarSituacao(
                2,
                new ContagensReacao(2, 0, 0),
                janelaEncerrada: false,
                SituacaoVerificacao.EmRevisao));

    [Theory]
    [InlineData(SituacaoVerificacao.Verificada)]
    [InlineData(SituacaoVerificacao.Rejeitada)]
    public void DeterminarSituacao_SituacoesFinaisNaoMudam(string situacao) =>
        Assert.Equal(
            situacao,
            ConsensoAtividade.DeterminarSituacao(
                2,
                new ContagensReacao(0, 1, 1),
                janelaEncerrada: false,
                situacao));

    [Fact]
    public void DeterminarSituacao_EmRevisaoComPrazoEncerradoPermanece() =>
        Assert.Equal(
            SituacaoVerificacao.EmRevisao,
            ConsensoAtividade.DeterminarSituacao(
                3,
                new ContagensReacao(0, 0, 0),
                janelaEncerrada: true,
                SituacaoVerificacao.EmRevisao));

    [Fact]
    public void DeterminarSituacao_PrazoEncerradoComAvisoBloqueante_EmRevisao() =>
        Assert.Equal(
            SituacaoVerificacao.EmRevisao,
            ConsensoAtividade.DeterminarSituacao(
                3,
                new ContagensReacao(1, 0, 0),
                janelaEncerrada: true,
                SituacaoVerificacao.AguardandoFeedback,
                temAvisoBloqueante: true));

    [Fact]
    public void DeterminarSituacao_TodosReagiramComAvisoBloqueante_Verificada() =>
        Assert.Equal(
            SituacaoVerificacao.Verificada,
            ConsensoAtividade.DeterminarSituacao(
                2,
                new ContagensReacao(2, 0, 0),
                janelaEncerrada: true,
                SituacaoVerificacao.AguardandoFeedback,
                temAvisoBloqueante: true));

    [Fact]
    public void DeterminarSituacao_SemRevisoresComAvisoBloqueante_Verificada() =>
        Assert.Equal(
            SituacaoVerificacao.Verificada,
            ConsensoAtividade.DeterminarSituacao(
                0,
                new ContagensReacao(0, 0, 0),
                janelaEncerrada: true,
                SituacaoVerificacao.AguardandoFeedback,
                temAvisoBloqueante: true));

    [Fact]
    public void JanelaAberta_Respeita48Horas()
    {
        var submitted = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.True(ConsensoAtividade.JanelaAberta(submitted, submitted.AddHours(47)));
        Assert.False(ConsensoAtividade.JanelaAberta(submitted, submitted.AddHours(48)));
    }
}
