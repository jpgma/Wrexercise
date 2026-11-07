using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class CatalogoCartasTests
{
    [Theory]
    [InlineData(CodigoCarta.Mais10Min, new[] { CodigoCarta.Variedade, CodigoCarta.FimDeSemana })]
    [InlineData(CodigoCarta.Variedade, new[] { CodigoCarta.Mais10Min, CodigoCarta.FimDeSemana })]
    [InlineData(CodigoCarta.FimDeSemana, new[] { CodigoCarta.Mais10Min, CodigoCarta.Variedade })]
    [InlineData(CodigoCarta.Mais20Min, new[] { CodigoCarta.AntesDas8 })]
    [InlineData(CodigoCarta.AntesDas8, new[] { CodigoCarta.Mais20Min })]
    public void MudancaDePlanos_TrocaDentroDaMesmaFaixa(string codigo, string[] esperados) =>
        Assert.Equal(esperados, CatalogoCartas.GetEquivalentes(codigo));

    [Fact]
    public void MudancaDePlanos_EquivalentesSaoProvocacoesComPrecoProximo()
    {
        foreach (var carta in CatalogoCartas.Todas.Where(c => c.Tipo == TipoCarta.Provocacao))
        {
            foreach (var equivalente in CatalogoCartas.GetEquivalentes(carta.Codigo))
            {
                var outra = CatalogoCartas.Find(equivalente)!;
                Assert.Equal(TipoCarta.Provocacao, outra.Tipo);
                Assert.InRange(Math.Abs(outra.Preco - carta.Preco), 0, 5);
                Assert.Contains(carta.Codigo, CatalogoCartas.GetEquivalentes(equivalente));
            }
        }
    }

    [Fact]
    public void MudancaDePlanos_RespostasNaoTemEquivalentes() =>
        Assert.Empty(CatalogoCartas.GetEquivalentes(CodigoCarta.Aposta));
}
