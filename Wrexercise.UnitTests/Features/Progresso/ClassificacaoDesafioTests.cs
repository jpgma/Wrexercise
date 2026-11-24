using Wrexercise.Features.Progresso;

namespace Wrexercise.UnitTests.Features.Progresso;

public class ClassificacaoDesafioTests
{
    private static readonly DateOnly Inicio = new(2026, 3, 2);
    private static readonly DateOnly Fim = Inicio.AddDays(27);
    private const int Meta = 4;

    private static ParticipanteDesafio P(Guid id, string nome, DateOnly? entrada = null) =>
        new(id, nome, entrada ?? Inicio.AddDays(-1));

    private static IReadOnlyList<EstatisticasParticipante> Calcular(
        IReadOnlyList<ParticipanteDesafio> participantes,
        Dictionary<(Guid, int), (int, int)> matriz,
        DateOnly hoje,
        Dictionary<Guid, TotaisVerificados>? totais = null) =>
        ClassificacaoDesafio.CalcularRanking(participantes, matriz, totais ?? [], Inicio, Fim, hoje, Meta);

    [Fact]
    public void CalcularRanking_PriorizaSemanasCumpridasDepoisSessoesDepoisSequencia()
    {
        var ana = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (4, 0),
            [(bob, 0)] = (8, 0),
            [(bob, 1)] = (0, 0),
        };

        var ranking = Calcular([P(ana, "Ana"), P(bob, "Bob")], matriz, Inicio.AddDays(14));

        Assert.Equal(["Ana", "Bob"], ranking.Select(r => r.NomeExibicao));
        Assert.Equal(2, ranking[0].SemanasCumpridas);
        Assert.Equal(1, ranking[1].SemanasCumpridas);
        Assert.Equal([1, 2], ranking.Select(r => r.Posicao));
    }

    [Fact]
    public void CalcularRanking_SequenciaContaDaSemanaMaisRecenteParaTras()
    {
        var ana = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (0, 0),
            [(ana, 2)] = (4, 0),
            [(ana, 3)] = (4, 0),
        };

        var ranking = Calcular([P(ana, "Ana")], matriz, Inicio.AddDays(27));

        Assert.Equal(3, Assert.Single(ranking).SemanasCumpridas);
        Assert.Equal(2, Assert.Single(ranking).SequenciaSemanas);
    }

    [Fact]
    public void CalcularRanking_SemanaEmAndamentoSemMeta_NaoZeraASequencia()
    {
        var ana = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (4, 0),
            [(ana, 2)] = (1, 0),
        };

        // Segunda-feira da 3ª semana: a semana ainda está aberta.
        var ranking = Calcular([P(ana, "Ana")], matriz, Inicio.AddDays(14));

        Assert.Equal(2, Assert.Single(ranking).SequenciaSemanas);
    }

    [Fact]
    public void CalcularRanking_SemanaEmAndamentoCumprida_SomaNaSequencia()
    {
        var ana = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (4, 0),
            [(ana, 2)] = (4, 0),
        };

        var ranking = Calcular([P(ana, "Ana")], matriz, Inicio.AddDays(17));

        Assert.Equal(3, Assert.Single(ranking).SequenciaSemanas);
    }

    [Fact]
    public void CalcularRanking_DesafioEncerradoSemMetaNaUltimaSemana_ZeraASequencia()
    {
        var ana = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(ana, 1)] = (4, 0),
            [(ana, 2)] = (4, 0),
            [(ana, 3)] = (1, 0),
        };

        var ranking = Calcular([P(ana, "Ana")], matriz, Fim.AddDays(3));

        Assert.Equal(0, Assert.Single(ranking).SequenciaSemanas);
    }

    [Fact]
    public void CalcularRanking_DepoisDaSequencia_DesempataPeloTempoEDepoisPelasAtividades()
    {
        var ana = Guid.NewGuid();
        var bia = Guid.NewGuid();
        var caio = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(ana, 0)] = (4, 0),
            [(bia, 0)] = (4, 0),
            [(caio, 0)] = (4, 0),
        };
        var totais = new Dictionary<Guid, TotaisVerificados>
        {
            [ana] = new(7200, 4),
            [bia] = new(9000, 4),
            [caio] = new(9000, 6),
        };

        var ranking = Calcular([P(ana, "Ana"), P(bia, "Bia"), P(caio, "Caio")], matriz, Inicio.AddDays(8), totais);

        Assert.Equal(["Caio", "Bia", "Ana"], ranking.Select(r => r.NomeExibicao));
        Assert.Equal([1, 2, 3], ranking.Select(r => r.Posicao));
        Assert.Equal(9000, ranking[0].SegundosVerificados);
        Assert.Equal(6, ranking[0].AtividadesVerificadas);
    }

    [Fact]
    public void CalcularRanking_EmpateEmTudo_DivideAPosicao()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();
        var matriz = new Dictionary<(Guid, int), (int, int)>
        {
            [(a, 0)] = (5, 0),
            [(b, 0)] = (4, 0),
            [(c, 0)] = (4, 0),
            [(d, 0)] = (1, 0),
        };
        var totais = new Dictionary<Guid, TotaisVerificados>
        {
            [a] = new(9000, 5),
            [b] = new(7200, 4),
            [c] = new(7200, 4),
            [d] = new(1800, 1),
        };

        var ranking = Calcular([P(d, "Duda"), P(c, "Zeca"), P(b, "Ana"), P(a, "Bia")], matriz, Inicio.AddDays(8), totais);

        Assert.Equal(["Bia", "Ana", "Zeca", "Duda"], ranking.Select(r => r.NomeExibicao));
        Assert.Equal([1, 2, 2, 4], ranking.Select(r => r.Posicao));
    }

    [Fact]
    public void CalcularRanking_ContaAsSemanasDisputadasDesdeAEntrada()
    {
        var ana = Guid.NewGuid();
        var bia = Guid.NewGuid();

        // Bia entra na terça da 1ª semana: começa a contar na 2ª.
        var ranking = Calcular(
            [P(ana, "Ana"), P(bia, "Bia", Inicio.AddDays(1))],
            [],
            Inicio.AddDays(15));

        Assert.Equal(3, ranking.Single(r => r.UsuarioId == ana).SemanasDisputadas);
        Assert.Equal(2, ranking.Single(r => r.UsuarioId == bia).SemanasDisputadas);
    }

    [Fact]
    public void GetProximo_Lider_MostraAVantagemEmDias()
    {
        var (ana, ranking) = Ranking(("Ana", 2, 10), ("Bob", 2, 7));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ana, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Lider, "Bob", 3), proximo);
    }

    [Fact]
    public void GetProximo_Lider_ComMaisSemanas_NaoContaDias()
    {
        var (ana, ranking) = Ranking(("Ana", 3, 10), ("Bob", 2, 12));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ana, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Lider, "Bob", 0), proximo);
    }

    [Fact]
    public void GetProximo_EmpateNaLideranca_AvisaOsDois()
    {
        var (ana, ranking) = Ranking(("Ana", 2, 9), ("Bob", 2, 9), ("Caio", 1, 4));
        var bob = ranking[1].UsuarioId;

        Assert.Equal(
            new ProximoClassificacao(TipoProximoClassificacao.EmpateLider, "Bob", 0),
            ClassificacaoDesafio.GetProximo(ranking, ana, metaDaSemanaCumprida: false));
        Assert.Equal(
            new ProximoClassificacao(TipoProximoClassificacao.EmpateLider, "Ana", 0),
            ClassificacaoDesafio.GetProximo(ranking, bob, metaDaSemanaCumprida: false));
    }

    [Fact]
    public void GetProximo_PosicaoDividida_PersegueQuemEstaDeFatoAcima()
    {
        var (_, ranking) = Ranking(("Ana", 2, 10), ("Bob", 2, 8), ("Caio", 2, 8));

        var proximo = ClassificacaoDesafio.GetProximo(ranking, ranking[2].UsuarioId, metaDaSemanaCumprida: false);

        Assert.Equal(new ProximoClassificacao(TipoProximoClassificacao.Treinos, "Ana", 3), proximo);
    }

    [Theory]
    [InlineData(9, 7, 3)]
    [InlineData(7, 7, 1)]
    public void GetProximo_MesmasSemanas_ContaOsDiasParaPassar(int sessoesAcima, int minhas, int esperado)
    {
        var (_, ranking) = Ranking(("Ana", 2, sessoesAcima), ("Bob", 2, minhas));
        if (sessoesAcima == minhas)
        {
            // Mesmos dias: Ana está à frente por outro critério, como a sequência.
            ranking = [ranking[0] with { SequenciaSemanas = 2 }, ranking[1] with { Posicao = 2 }];
        }

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

    /// <returns>O id do primeiro e o ranking, já na ordem dada, com as posições divididas no empate.</returns>
    private static (Guid Primeiro, IReadOnlyList<EstatisticasParticipante> Ranking) Ranking(
        params (string Nome, int Semanas, int Sessoes)[] linhas)
    {
        var ranking = new List<EstatisticasParticipante>();
        foreach (var l in linhas)
        {
            var item = new EstatisticasParticipante(Guid.NewGuid(), l.Nome, l.Semanas, l.Sessoes, 0);
            var posicao = ranking.Count > 0 && ClassificacaoDesafio.Empatam(ranking[^1], item)
                ? ranking[^1].Posicao
                : ranking.Count + 1;
            ranking.Add(item with { Posicao = posicao });
        }

        return (ranking[0].UsuarioId, ranking);
    }
}
