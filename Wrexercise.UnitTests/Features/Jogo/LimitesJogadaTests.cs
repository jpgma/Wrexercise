using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class LimitesJogadaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Bia = Guid.NewGuid();

    private static JogadaLimite Jogada(Guid de, Guid para, string situacao = SituacaoJogada.Enviada, int horasAtras = 48) =>
        new(de, para, Agora.AddHours(-horasAtras), situacao);

    private static string? Verificar(params JogadaLimite[] jogadas) =>
        LimitesJogada.Verificar(Ana, Bia, jogadas, Agora);

    [Fact]
    public void SemJogadas_Permite() => Assert.Null(Verificar());

    [Theory]
    [InlineData(SituacaoJogada.Enviada)]
    [InlineData(SituacaoJogada.Aceita)]
    public void JogadaAtivaNoPar_EmQualquerDirecao_Bloqueia(string situacao)
    {
        Assert.Equal(LimitesJogada.ErroPar, Verificar(Jogada(Ana, Bia, situacao)));
        Assert.Equal(LimitesJogada.ErroPar, Verificar(Jogada(Bia, Ana, situacao)));
    }

    [Fact]
    public void AguardandoVerificacao_LiberaAVaga() =>
        Assert.Null(Verificar(Jogada(Ana, Bia, SituacaoJogada.AguardandoVerificacao)));

    [Fact]
    public void MesmaPessoaEm24Horas_Bloqueia() =>
        Assert.Equal(LimitesJogada.ErroRecente, Verificar(Jogada(Ana, Bia, SituacaoJogada.Recusada, horasAtras: 23)));

    [Fact]
    public void DuasEnviadasAtivas_Bloqueia() =>
        Assert.Equal(
            LimitesJogada.ErroEnviadas,
            Verificar(Jogada(Ana, Guid.NewGuid()), Jogada(Ana, Guid.NewGuid(), SituacaoJogada.Aceita)));

    [Fact]
    public void DuasRecebidasAtivas_Bloqueia() =>
        Assert.Equal(
            LimitesJogada.ErroRecebidas,
            Verificar(Jogada(Guid.NewGuid(), Bia), Jogada(Guid.NewGuid(), Bia)));
}
