using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class CalculoEnergiaTests
{
    // 2026-11-02 é uma segunda-feira.
    private static readonly DateOnly Segunda = new(2026, 11, 2);

    private static AtividadeEnergia Atividade(int dia, int hora, string tipo = "Corrida", bool verificada = true) =>
        new(Segunda.AddDays(dia).ToDateTime(new TimeOnly(hora, 0)), tipo, verificada);

    private static DesafioEnergia Desafio(
        int[] cumpridas,
        bool encerrado = false,
        int semanas = 4,
        DateOnly? inicio = null,
        DateOnly? entrada = null)
    {
        var dataInicio = inicio ?? Segunda;
        return new(dataInicio, dataInicio.AddDays(semanas * 7 - 1), entrada ?? dataInicio.AddDays(-30), encerrado, cumpridas);
    }

    private static EnergiaGanha Calcular(params AtividadeEnergia[] atividades) =>
        CalculoEnergia.Calcular(atividades, []);

    [Fact]
    public void SemNada_NaoGanhaEnergia() => Assert.Equal(0, CalculoEnergia.Calcular([], []).Total);

    [Fact]
    public void Jogadas_DestinatarioGanhaOPrecoERemetenteAMetadeParaBaixo()
    {
        var ganha = CalculoEnergia.Calcular([], [], [new(45, ComoDestinatario: true), new(45, ComoDestinatario: false)]);

        Assert.Equal(45 + 22, ganha.Jogadas);
        Assert.Equal(67, ganha.Total);
    }

    [Fact]
    public void Jogadas_ApostaDobraSoAParteDeQuemRecebeu()
    {
        var ganha = CalculoEnergia.Calcular(
            [],
            [],
            [new(35, ComoDestinatario: true, Aposta: true), new(35, ComoDestinatario: false, Aposta: true)]);

        Assert.Equal(70 + 17, ganha.Jogadas);
    }

    [Fact]
    public void SemanaTipica_RendeSetentaETresDasFontesDaG2()
    {
        // Meta de 3: segunda 07:00 Corrida, quarta Musculação, sábado Corrida, todas verificadas.
        // Missões, Rival e Jogadas completam o alvo de 90 a 110 EN por semana (§5.1).
        var ganha = CalculoEnergia.Calcular(
            [Atividade(0, 7), Atividade(2, 18, "Musculacao"), Atividade(5, 10)],
            [Desafio([0])]);

        Assert.Equal(new EnergiaGanha(30, 9 + 8 + 6, 20, 0, 0), ganha);
        Assert.Equal(73, ganha.Total);
    }

    [Fact]
    public void Base_SoContaAsVerificadas() =>
        Assert.Equal(10, Calcular(Atividade(0, 12), Atividade(1, 12, verificada: false)).Atividades);

    [Fact]
    public void Bonus_TemTetoDeDezPorAtividade() =>
        // Sábado 05:00, primeira da semana: 3 + 3 + 5 + 3 = 14.
        Assert.Equal(10, Calcular(Atividade(5, 5)).Bonus);

    [Fact]
    public void Bonus_PendenteOcupaAPosicaoSemReceberCredito()
    {
        Assert.Equal(3, Calcular(Atividade(0, 12, verificada: false), Atividade(1, 12)).Bonus);

        // Se a pendente for rejeitada, ela sai da lista e a seguinte passa a ser a primeira da semana.
        Assert.Equal(6, Calcular(Atividade(1, 12)).Bonus);
    }

    [Fact]
    public void Bonus_CedoPagaUmaVezPorDia() =>
        // Segunda 12:00 (3 + 3), terça 05:30 (3 + 5), terça 07:00 (nada).
        Assert.Equal(14, Calcular(Atividade(0, 12), Atividade(1, 5), Atividade(1, 7)).Bonus);

    [Fact]
    public void Bonus_FimDeSemanaPagaUmaVezPorDia() =>
        // Sábado 10:00 (3 + 3 + 3), sábado 16:00 (nada), domingo 10:00 (3 + 3).
        Assert.Equal(15, Calcular(Atividade(5, 10), Atividade(5, 16), Atividade(6, 10)).Bonus);

    [Fact]
    public void Bonus_TipoNovoSoAPartirDoSegundoTipo()
    {
        Assert.Equal(6, Calcular(Atividade(0, 12, "Yoga")).Bonus);

        // Segunda Corrida (3 + 3), segunda Corrida (nada), terça Yoga (3 + 5), quarta Yoga (3).
        Assert.Equal(17, Calcular(
            Atividade(0, 12), Atividade(0, 18), Atividade(1, 12, "Yoga"), Atividade(2, 12, "Yoga")).Bonus);
    }

    [Fact]
    public void Bonus_RecomecaNaSemanaSeguinte() =>
        Assert.Equal(12, Calcular(Atividade(0, 12), Atividade(7, 12)).Bonus);

    [Fact]
    public void MesmoTreinoEmDoisGrupos_NaoDobraBonusNemMeta()
    {
        var ganha = CalculoEnergia.Calcular(
            [Atividade(0, 12), Atividade(0, 12)],
            [Desafio([0]), Desafio([0])]);

        Assert.Equal(20, ganha.Atividades);
        Assert.Equal(6, ganha.Bonus);
        Assert.Equal(20, ganha.Metas);
    }

    [Fact]
    public void AtividadeRetroativa_NaoReduzOCreditoDasAnteriores()
    {
        var antes = Calcular(Atividade(1, 12));

        // A segunda-feira é registrada depois da terça: entra no fim da ordem de registro.
        var depois = Calcular(Atividade(1, 12), Atividade(0, 12));

        Assert.Equal(6, antes.Bonus);
        Assert.Equal(9, depois.Bonus);
    }

    [Fact]
    public void Metas_PagamPorSemanaCumprida() =>
        Assert.Equal(60, CalculoEnergia.Calcular([], [Desafio([0, 1, 3])]).Metas);

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 25)]
    [InlineData(8, 75)]
    [InlineData(12, 175)]
    public void Sequencias_PagamCadaMarcoUmaVez(int semanas, int esperado) =>
        Assert.Equal(
            esperado,
            CalculoEnergia.Calcular(
                [],
                [Desafio([.. Enumerable.Range(0, semanas)], semanas: 12), Desafio([.. Enumerable.Range(0, semanas)], semanas: 12)])
                .Sequencias);

    [Fact]
    public void Sequencias_NaoSomamDesafios() =>
        Assert.Equal(
            0,
            CalculoEnergia.Calcular([], [Desafio([0, 1, 2]), Desafio([0, 1, 2], inicio: Segunda.AddDays(28))]).Sequencias);

    [Fact]
    public void DesafioConcluido_ComTodasAsSemanasCumpridas() =>
        Assert.Equal(75, CalculoEnergia.Calcular([], [Desafio([0, 1, 2, 3], encerrado: true)]).Desafios);

    [Fact]
    public void DesafioConcluido_NaoPagaComSemanaFaltando() =>
        Assert.Equal(0, CalculoEnergia.Calcular([], [Desafio([0, 1, 3], encerrado: true)]).Desafios);

    [Fact]
    public void DesafioConcluido_NaoPagaAntesDeEncerrar() =>
        Assert.Equal(0, CalculoEnergia.Calcular([], [Desafio([0, 1, 2, 3])]).Desafios);

    [Fact]
    public void DesafioConcluido_IgnoraSemanasAntesDaEntrada() =>
        // Entrou na terça da segunda semana: contam só as semanas 3 e 4.
        Assert.Equal(
            75,
            CalculoEnergia.Calcular([], [Desafio([2, 3], encerrado: true, entrada: Segunda.AddDays(8))]).Desafios);

    [Fact]
    public void DesafioConcluido_SemSemanaQueConte_NaoPaga() =>
        Assert.Equal(
            0,
            CalculoEnergia.Calcular([], [Desafio([], encerrado: true, entrada: Segunda.AddDays(40))]).Desafios);
}
