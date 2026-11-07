using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class PrazoJogadaTests
{
    // 2026-11-02 é uma segunda-feira.
    private static DateTime Em(int dia, int hora, int minuto = 0) =>
        new DateTime(2026, 11, 2, hora, minuto, 0).AddDays(dia);

    [Theory]
    [InlineData(CodigoCarta.Mais10Min)]
    [InlineData(CodigoCarta.Mais20Min)]
    [InlineData(CodigoCarta.Variedade)]
    public void Padrao_QuarentaEOitoHoras(string codigo) =>
        Assert.Equal(Em(2, 15, 30), PrazoJogada.Calcular(codigo, Em(0, 15, 30)));

    [Theory]
    [InlineData(0)] // segunda
    [InlineData(4)] // sexta
    [InlineData(5)] // sábado
    public void FimDeSemana_VaiAteOFimDoProximoDomingo(int dia) =>
        Assert.Equal(Em(7, 0), PrazoJogada.Calcular(CodigoCarta.FimDeSemana, Em(dia, 21)));

    [Fact]
    public void FimDeSemana_EnviadaNoDomingo_VaiAteODomingoSeguinte() =>
        Assert.Equal(Em(14, 0), PrazoJogada.Calcular(CodigoCarta.FimDeSemana, Em(6, 10)));

    [Theory]
    [InlineData(6)]
    [InlineData(23)]
    public void AntesDas8_SempreTemDuasManhas(int hora) =>
        Assert.Equal(Em(2, 8), PrazoJogada.Calcular(CodigoCarta.AntesDas8, Em(0, hora)));

    [Fact]
    public void Mudanca_ParaFimDeSemana_EstendeAteODomingo() =>
        Assert.Equal(
            Em(7, 0),
            PrazoJogada.ComMudanca(CodigoCarta.FimDeSemana, Em(0, 15), Em(2, 15)));

    [Fact]
    public void Mudanca_NuncaEncurtaOPrazo() =>
        // FIM DE SEMANA enviada na sexta vale até domingo; trocada por +10 MIN, não cai para 48 horas.
        Assert.Equal(
            Em(7, 0),
            PrazoJogada.ComMudanca(CodigoCarta.Mais10Min, Em(4, 21), Em(7, 0)));
}
