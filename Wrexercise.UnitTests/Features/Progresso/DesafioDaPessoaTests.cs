using Wrexercise.Features.Progresso.Contracts;

namespace Wrexercise.UnitTests.Features.Progresso;

public class DesafioDaPessoaTests
{
    // 2026-11-02 é uma segunda-feira.
    private static readonly DateOnly Segunda = new(2026, 11, 2);

    private static DesafioDaPessoa Desafio(int[] cumpridas, bool encerrado = true, DateOnly? entrada = null) =>
        new(Segunda, Segunda.AddDays(4 * 7 - 1), entrada ?? Segunda.AddDays(-30), encerrado, cumpridas);

    [Fact]
    public void IsConcluido_ExigeDesafioEncerradoComTodasAsSemanasCumpridas()
    {
        Assert.True(Desafio([0, 1, 2, 3]).IsConcluido());
        Assert.False(Desafio([0, 1, 3]).IsConcluido());
        Assert.False(Desafio([0, 1, 2, 3], encerrado: false).IsConcluido());
    }

    [Fact]
    public void IsConcluido_IgnoraSemanasAnterioresAEntrada()
    {
        var entrouNaTerceira = Segunda.AddDays(14);

        Assert.True(Desafio([2, 3], entrada: entrouNaTerceira).IsConcluido());
        Assert.False(Desafio([2], entrada: entrouNaTerceira).IsConcluido());
    }

    [Fact]
    public void IsConcluido_SemSemanasQueContavam_NaoConta() =>
        Assert.False(Desafio([], entrada: Segunda.AddDays(60)).IsConcluido());

    [Fact]
    public void GetMelhorSequencia_PegaOMelhorDesafio() =>
        Assert.Equal(3, DesafiosDaPessoa.GetMelhorSequencia([Desafio([0, 2]), Desafio([1, 2, 3])]));
}
