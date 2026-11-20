using Wrexercise.Features.Progresso;

namespace Wrexercise.UnitTests.Features.Progresso;

public class ClassificacaoDesafioTests
{
    private static readonly DateOnly Inicio = new(2026, 3, 2);
    private static readonly DateOnly Fim = Inicio.AddDays(27);
    private const int Meta = 4;

    [Fact]
    public void CalcularRanking_PriorizaSemanasCumpridasDepoisSessoesDepoisSequencia()
    {
        var ana = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var participantes = new List<(Guid, string)>
        {
            (ana, "Ana"),
            (bob, "Bob"),
        };

        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (4, 0),
            [(bob, 0)] = (8, 0),
            [(bob, 1)] = (0, 0),
        };

        var ranking = ClassificacaoDesafio.CalcularRanking(
            participantes,
            matriz,
            Inicio,
            Fim,
            Inicio.AddDays(14),
            Meta);

        Assert.Equal(["Ana", "Bob"], ranking.Select(r => r.NomeExibicao));
        Assert.Equal(2, ranking[0].SemanasCumpridas);
        Assert.Equal(1, ranking[1].SemanasCumpridas);
    }

    [Fact]
    public void CalcularRanking_SequenciaContaDaSemanaMaisRecenteParaTras()
    {
        var ana = Guid.NewGuid();
        var participantes = new List<(Guid, string)> { (ana, "Ana") };
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (0, 0),
            [(ana, 2)] = (4, 0),
            [(ana, 3)] = (4, 0),
        };

        var ranking = ClassificacaoDesafio.CalcularRanking(
            participantes,
            matriz,
            Inicio,
            Fim,
            Inicio.AddDays(27),
            Meta);

        Assert.Equal(3, Assert.Single(ranking).SemanasCumpridas);
        Assert.Equal(2, Assert.Single(ranking).SequenciaSemanas);
    }

    [Fact]
    public void CalcularRanking_DesempataPorNome()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var participantes = new List<(Guid, string)>
        {
            (b, "Zeca"),
            (a, "Ana"),
        };

        var ranking = ClassificacaoDesafio.CalcularRanking(
            participantes,
            [],
            Inicio,
            Fim,
            Inicio.AddDays(7),
            Meta);

        Assert.Equal(["Ana", "Zeca"], ranking.Select(r => r.NomeExibicao));
    }

    [Fact]
    public void GetProximo_Lider_MostraAVantagemEmTreinos()
    {
        var (ana, ranking) = Ranking(("Ana", 2, 10), ("Bob", 2, 7));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ana, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Lider, "Bob", 3), proximo);
    }

    [Fact]
    public void GetProximo_Lider_ComMaisSemanas_NaoContaTreinos()
    {
        var (ana, ranking) = Ranking(("Ana", 3, 10), ("Bob", 2, 12));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ana, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Lider, "Bob", 0), proximo);
    }

    [Theory]
    [InlineData(9, 7, 3)]
    [InlineData(7, 7, 1)]
    public void GetProximo_MesmasSemanas_ContaOsTreinosParaPassar(int sessoesAcima, int minhas, int esperado)
    {
        var (_, ranking) = Ranking(("Ana", 2, sessoesAcima), ("Bob", 2, minhas));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ranking[1].UsuarioId, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Treinos, "Ana", esperado), proximo);
    }

    [Fact]
    public void GetProximo_UmaSemanaAtras_SemAMetaDaSemana_PedeAMeta()
    {
        var (_, ranking) = Ranking(("Ana", 2, 9), ("Bob", 1, 9));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ranking[1].UsuarioId, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Meta, "Ana", 1), proximo);
    }

    [Fact]
    public void GetProximo_SemanasAtras_ContaAsSemanas()
    {
        var (_, ranking) = Ranking(("Ana", 3, 12), ("Bob", 1, 9));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ranking[1].UsuarioId, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Semanas, "Ana", 2), proximo);
    }

    [Fact]
    public void GetProximo_UmaSemanaAtras_ComAMetaDaSemana_ContaAsSemanas()
    {
        var (_, ranking) = Ranking(("Ana", 2, 9), ("Bob", 1, 9));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ranking[1].UsuarioId, metaDaSemanaCumprida: true);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Semanas, "Ana", 1), proximo);
    }

    [Fact]
    public void GetProximo_SemTreinoVerificado_SozinhoOuForaDoRanking_NaoTemDisputa()
    {
        var (ana, zerados) = Ranking(("Ana", 0, 0), ("Bob", 0, 0));
        var (solo, sozinha) = Ranking(("Ana", 1, 4));
        var (_, outros) = Ranking(("Ana", 1, 4), ("Bob", 0, 1));

        Assert.Null(ClassificacaoDesafio.GetProximo(zerados, ana, metaDaSemanaCumprida: false));
        Assert.Null(ClassificacaoDesafio.GetProximo(sozinha, solo, metaDaSemanaCumprida: false));
        Assert.Null(ClassificacaoDesafio.GetProximo(outros, Guid.NewGuid(), metaDaSemanaCumprida: false));
    }

    /// <returns>O id do primeiro e o ranking, já na ordem dada.</returns>
    private static (Guid Primeiro, IReadOnlyList<EstatisticasParticipante> Ranking) Ranking(
        params (string Nome, int Semanas, int Sessoes)[] linhas)
    {
        var ranking = linhas
            .Select(l => new EstatisticasParticipante(Guid.NewGuid(), l.Nome, l.Semanas, l.Sessoes, 0))
            .ToList();
        return (ranking[0].UsuarioId, ranking);
    }
}
