using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class FadigaTests
{
    private static readonly Guid GrupoA = Guid.NewGuid();
    private static readonly Guid GrupoB = Guid.NewGuid();

    /// <summary>Um dia depois do início da FADIGA, ao meio-dia.</summary>
    private static readonly DateTimeOffset Base = Fadiga.Inicio.AddDays(1).AddHours(12);

    private static DateTimeOffset Hora(int horas) => Base.AddHours(horas);

    /// <summary>O dia verificado <paramref name="dia"/> dias depois da base, verificado <paramref name="horas"/> depois dela.</summary>
    private static DiaVerificado Dia(int dia, int horas, bool pago = true, Guid? grupo = null) =>
        new(grupo ?? GrupoA, DateOnly.FromDateTime(Base.DateTime).AddDays(dia), Hora(horas), pago);

    [Fact]
    public void SemGatilho_NaoTemFadiga() =>
        Assert.Equal(new ResultadoFadiga(0, 0), Fadiga.Calcular([], [Dia(0, 0), Dia(1, 24)]));

    [Fact]
    public void Gatilho_DaTresCargas() =>
        Assert.Equal(new ResultadoFadiga(0, Fadiga.Cargas), Fadiga.Calcular([Hora(0)], []));

    [Fact]
    public void TresDiasDepois_GastamAsCargasEOQuartoRendeNormal()
    {
        var dias = new[] { Dia(1, 24), Dia(2, 48), Dia(3, 72), Dia(4, 96) };

        Assert.Equal(new ResultadoFadiga(3, 0), Fadiga.Calcular([Hora(0)], dias));
        Assert.Equal(new ResultadoFadiga(2, 1), Fadiga.Calcular([Hora(0)], dias[..2]));
    }

    [Fact]
    public void DiaVerificadoAntesDoGatilho_NaoTemFadiga() =>
        Assert.Equal(new ResultadoFadiga(1, 2), Fadiga.Calcular([Hora(10)], [Dia(0, 5), Dia(1, 24)]));

    [Fact]
    public void GatilhoNoMesmoInstante_ODiaVemAntes() =>
        Assert.Equal(new ResultadoFadiga(0, 3), Fadiga.Calcular([Hora(5)], [Dia(0, 5)]));

    [Fact]
    public void SegundoGatilhoComFadigaAtiva_NaoAcumulaNemRenova()
    {
        var resultado = Fadiga.Calcular([Hora(0), Hora(30), Hora(50)], [Dia(1, 24), Dia(2, 48), Dia(3, 72), Dia(4, 96)]);

        Assert.Equal(new ResultadoFadiga(3, 0), resultado);
    }

    [Fact]
    public void GatilhoDepoisDeZerar_AplicaDeNovo()
    {
        var resultado = Fadiga.Calcular([Hora(0), Hora(80)], [Dia(1, 24), Dia(2, 48), Dia(3, 72), Dia(4, 96)]);

        Assert.Equal(new ResultadoFadiga(4, 2), resultado);
    }

    [Fact]
    public void GatilhoAntesDoInicio_NaoConta() =>
        Assert.Equal(
            new ResultadoFadiga(0, 0),
            Fadiga.Calcular([Fadiga.Inicio.AddMinutes(-1)], [Dia(0, 0)]));

    [Fact]
    public void DiaAnulado_GastaACargaSemDescontar()
    {
        var resultado = Fadiga.Calcular([Hora(0)], [Dia(1, 24, pago: false), Dia(2, 48)]);

        Assert.Equal(new ResultadoFadiga(1, 1), resultado);
    }

    [Fact]
    public void MesmoDiaEmDoisGrupos_SaoDuasBases() =>
        Assert.Equal(
            new ResultadoFadiga(2, 1),
            Fadiga.Calcular([Hora(0)], [Dia(1, 24), Dia(1, 25, grupo: GrupoB)]));

    [Fact]
    public void Desconto_EMetadeDaBaseArredondadaParaBaixo() =>
        Assert.Equal(CalculoEnergia.PorDia - CalculoEnergia.PorDia / 2, Fadiga.Desconto);

    [Fact]
    public void CalculoEnergia_FadigaSoReduzABase()
    {
        var semFadiga = CalculoEnergia.Calcular([], [], [new(45, ComoDestinatario: true)], rivaisVencidos: 1);
        var comFadiga = CalculoEnergia.Calcular([], [], [new(45, ComoDestinatario: true)], rivaisVencidos: 1, diasComFadiga: 3);

        Assert.Equal(semFadiga with { Fadiga = -15 }, comFadiga);
        Assert.Equal(semFadiga.Total - 15, comFadiga.Total);
    }
}
