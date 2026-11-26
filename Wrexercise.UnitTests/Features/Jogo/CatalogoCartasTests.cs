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

    [Theory]
    [InlineData(CodigoCarta.AntesDas8, CodigoCarta.Variedade, true)]
    [InlineData(CodigoCarta.Mais10Min, CodigoCarta.Mais20Min, true)]
    [InlineData(CodigoCarta.Variedade, CodigoCarta.Variedade, false)]
    [InlineData(CodigoCarta.Variedade, CodigoCarta.Aposta, false)]
    [InlineData(CodigoCarta.PrazoExtra, CodigoCarta.Aposta, false)]
    public void Fusao_SoDuasProvocacoesDiferentes(string a, string b, bool pode)
    {
        Assert.Equal(pode, CatalogoCartas.PodeFundir(CatalogoCartas.Find(a)!, CatalogoCartas.Find(b)!));
        Assert.Equal(pode, CatalogoCartas.PodeFundir(CatalogoCartas.Find(b)!, CatalogoCartas.Find(a)!));
    }

    [Fact]
    public void TodoMundo_EGlobalECustaCentoECinquenta()
    {
        var carta = CatalogoCartas.Find(CodigoCarta.TodoMundo)!;
        Assert.Equal(TipoCarta.Global, carta.Tipo);
        Assert.Equal(150, carta.Preco);
        Assert.Empty(CatalogoCartas.GetEquivalentes(carta.Codigo));
    }

    [Fact]
    public void TodoMundo_NaoEntraEmFusao()
    {
        var global = CatalogoCartas.Find(CodigoCarta.TodoMundo)!;
        Assert.False(CatalogoCartas.PodeFundir(global, CatalogoCartas.Find(CodigoCarta.Variedade)!));
        Assert.False(CatalogoCartas.PodeFundir(CatalogoCartas.Find(CodigoCarta.Variedade)!, global));
    }
}
