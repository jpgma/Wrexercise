using Wrexercise.Features.Jogo;
using Wrexercise.Features.Jogo.Contracts;

namespace Wrexercise.UnitTests.Features.Jogo.Contracts;

public class JogadasConcluidasTests
{
    private static readonly DateTimeOffset Envio = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);

    private static readonly JogadaEstado Jogada = new(
        CodigoCarta.Variedade, Envio, Envio.AddHours(48), "Aceita", Envio.AddHours(1), null, "Corrida");

    private static (Guid, AtividadeJogada) Verificada(Guid id, string tipo) =>
        (id, new AtividadeJogada(new DateTime(2026, 11, 3, 12, 0, 0), tipo, 3600, true));

    [Fact]
    public void PrimeiraQueCumpre_Conclui()
    {
        var id = Guid.NewGuid();
        Assert.True(JogadasConcluidas.ConcluiAgora(Jogada, id, [Verificada(id, "Yoga"), Verificada(Guid.NewGuid(), "Corrida")]));
    }

    [Fact]
    public void JaConcluidaPorOutra_NaoAvisaDeNovo()
    {
        var id = Guid.NewGuid();
        Assert.False(JogadasConcluidas.ConcluiAgora(Jogada, id, [Verificada(id, "Yoga"), Verificada(Guid.NewGuid(), "Ciclismo")]));
    }

    [Fact]
    public void AtividadeQueNaoCumpre_NaoConclui()
    {
        var id = Guid.NewGuid();
        Assert.False(JogadasConcluidas.ConcluiAgora(Jogada, id, [Verificada(id, "Corrida")]));
    }
}
