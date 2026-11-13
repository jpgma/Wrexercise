using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;

namespace Wrexercise.UnitTests.Features.Atividades;

public sealed class ValidacaoAtividadeTests
{
    [Theory]
    [InlineData(TipoAtividade.Musculacao, true)]
    [InlineData("musculacao", false)]
    public void TipoAtividade_RespeitaOContrato(string valor, bool valido) =>
        Assert.Equal(valido, TipoAtividade.IsValid(valor));

    [Theory]
    [InlineData(TipoComprovacao.Fotografia, true)]
    [InlineData("Foto", false)]
    public void TipoComprovacao_RespeitaOContrato(string valor, bool valido) =>
        Assert.Equal(valido, TipoComprovacao.IsValid(valor));

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(86400, true)]
    [InlineData(86401, false)]
    public void Duracao_RespeitaLimites(int segundos, bool valido) =>
        Assert.Equal(valido, ValidacaoAtividade.IsValidDuracao(segundos));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Minhas", true)]
    [InlineData("Pendentes", true)]
    [InlineData("Verificadas", true)]
    [InlineData("EmRevisao", true)]
    [InlineData("minhas", false)]
    [InlineData("' OR 1=1 --", false)]
    public void Filtro_SoAceitaValoresConhecidos(string? filtro, bool valido) =>
        Assert.Equal(valido, ValidacaoAtividade.TryGetFiltroSql(filtro, out _));

    [Fact]
    public void StartedAt_NaoAceitaHorarioDistanteNoFuturo()
    {
        Assert.True(ValidacaoAtividade.IsValidStartedAt(DateTimeOffset.UtcNow));
        Assert.False(ValidacaoAtividade.IsValidStartedAt(DateTimeOffset.UtcNow.AddHours(2)));
    }

    [Fact]
    public void Paginacao_CursorRedondo()
    {
        var started = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var id = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var cursor = PaginacaoAtividade.EncodeCursor(started, id);
        Assert.True(PaginacaoAtividade.TryDecodeCursor(cursor, out var decodedStarted, out var decodedId));
        Assert.Equal(started, decodedStarted);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData(SituacaoVerificacao.AguardandoFeedback, true)]
    [InlineData(SituacaoVerificacao.Verificada, false)]
    [InlineData(SituacaoVerificacao.Rejeitada, false)]
    public void SituacaoPendente_SegueOContrato(string situacao, bool pendente) =>
        Assert.Equal(pendente, SituacaoVerificacao.ContaComoPendente(situacao));

    [Theory]
    [InlineData(true, SituacaoVerificacao.AguardandoFeedback, 14, true)]
    [InlineData(true, SituacaoVerificacao.AguardandoFeedback, 15, false)]
    [InlineData(true, SituacaoVerificacao.EmRevisao, 1, false)]
    [InlineData(true, SituacaoVerificacao.Verificada, 1, false)]
    [InlineData(false, SituacaoVerificacao.AguardandoFeedback, 1, false)]
    public void Exclusao_SoOAutorNos15MinutosSemContestacao(bool autor, string situacao, int minutos, bool permitida)
    {
        var registrada = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

        var ate = ExclusaoAtividade.PermitidaAte(autor, situacao, registrada, registrada.AddMinutes(minutos));

        Assert.Equal(permitida ? registrada.AddMinutes(15) : null, ate);
    }
}
