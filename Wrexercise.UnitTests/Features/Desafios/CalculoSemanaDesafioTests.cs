using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Progresso;

namespace Wrexercise.UnitTests.Features.Desafios;

public class CalculoSemanaDesafioTests
{
    private static readonly DateOnly Segunda = new(2026, 3, 2);

    [Theory]
    [InlineData(-30, true)] // entrou antes
    [InlineData(-1, true)]
    [InlineData(0, true)] // entrou na própria segunda
    [InlineData(1, false)] // entrou na terça
    [InlineData(6, false)] // entrou no domingo
    public void ParticipaDaSemana_SoSemanasInteiras(int diasDaEntrada, bool esperado) =>
        Assert.Equal(esperado, CalculoSemanaDesafio.ParticipaDaSemana(Segunda, Segunda.AddDays(diasDaEntrada)));

    [Theory]
    [InlineData(0, 7)] // segunda
    [InlineData(3, 4)] // quinta
    [InlineData(6, 1)] // domingo: último dia
    public void CountDiasRestantes_ContaHoje(int diasDesdeSegunda, int esperado)
    {
        var semana = CalculoSemanaDesafio.GetSemana(Segunda, Segunda.AddDays(27), Segunda.AddDays(7 + diasDesdeSegunda))!;

        Assert.Equal(esperado, CalculoSemanaDesafio.CountDiasRestantes(semana, Segunda.AddDays(7 + diasDesdeSegunda)));
    }

    [Fact]
    public void MontarHistorico_MarcaAsSemanasAnterioresAEntrada()
    {
        var usuario = Guid.NewGuid();

        var historico = HistoricoSemanalDesafio.MontarHistorico(
            usuario,
            [],
            Segunda,
            Segunda.AddDays(27),
            4,
            Segunda.AddDays(10));

        Assert.Equal([false, false, true, true], historico.Select(s => s.Participou));
    }
}
