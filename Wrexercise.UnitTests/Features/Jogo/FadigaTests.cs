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

    // --- Decisão 0089 ---

    /// <summary>Meio-dia, um dia depois do início das regras da decisão 0089.</summary>
    private static readonly DateTimeOffset Base2 = Fadiga.InicioRegras2.AddDays(1).AddHours(12);

    private static GatilhoFadiga Gatilho(int horas, MotivoFadiga motivo = MotivoFadiga.Recusa) =>
        new(Base2.AddHours(horas), motivo, Guid.NewGuid());

    private static DiaVerificado Dia2(int dia, int horas) =>
        new(GrupoA, DateOnly.FromDateTime(Base2.DateTime).AddDays(dia), Base2.AddHours(horas));

    [Theory]
    [InlineData("Recusa", Fadiga.Cargas)]
    [InlineData("Expiracao", Fadiga.Cargas)]
    [InlineData("Falha", Fadiga.CargasFalha)]
    public void CargasPorMotivo(string motivo, int cargas)
    {
        var gatilho = Gatilho(0, Enum.Parse<MotivoFadiga>(motivo));
        var resultado = Fadiga.Calcular([gatilho], [], []);

        Assert.Equal(cargas, resultado.Restantes);
        Assert.Equal(gatilho, resultado.Causa);
    }

    [Fact]
    public void RecusaComFadigaAtiva_SomaUmaCargaAteOTeto()
    {
        var recusas = Enumerable.Range(0, 5).Select(h => Gatilho(h)).ToList();

        Assert.Equal(4, Fadiga.Calcular(recusas[..2], [], []).Restantes);
        Assert.Equal(Fadiga.Teto, Fadiga.Calcular(recusas, [], []).Restantes);
        Assert.Equal(recusas[2], Fadiga.Calcular(recusas, [], []).Causa);
    }

    [Theory]
    [InlineData("Falha")]
    [InlineData("Expiracao")]
    public void FalhaOuExpiracaoComFadigaAtiva_NaoMudaNada(string motivo)
    {
        var primeira = Gatilho(0);
        var resultado = Fadiga.Calcular(
            [primeira, Gatilho(30, Enum.Parse<MotivoFadiga>(motivo))], [Dia2(1, 24)], []);

        Assert.Equal(new ResultadoFadiga(1, 2), resultado);
        Assert.Equal(primeira, resultado.Causa);
    }

    [Fact]
    public void CartaCumprida_TiraUmaCarga_ENaoPassaDeZero()
    {
        Assert.Equal(new ResultadoFadiga(0, 2), Fadiga.Calcular([Gatilho(0)], [], [Base2.AddHours(5)]));
        Assert.Equal(
            new ResultadoFadiga(0, 0),
            Fadiga.Calcular([Gatilho(0)], [], [Base2.AddHours(1), Base2.AddHours(2), Base2.AddHours(3), Base2.AddHours(4)]));
    }

    [Fact]
    public void CartaCumpridaSemFadiga_NaoGuardaCredito() =>
        Assert.Equal(Fadiga.Cargas, Fadiga.Calcular([Gatilho(10)], [], [Base2.AddHours(1)]).Restantes);

    [Fact]
    public void MesmoInstante_DiaDepoisRedencaoDepoisGatilho()
    {
        // O treino que cumpre a carta gasta a carga do dia e tira outra pela carta, no mesmo instante.
        var resultado = Fadiga.Calcular([Gatilho(0)], [Dia2(1, 24)], [Base2.AddHours(24)]);

        Assert.Equal(new ResultadoFadiga(1, 1), resultado);
    }

    [Fact]
    public void SemCargas_NaoTemCausa() =>
        Assert.Null(Fadiga.Calcular([Gatilho(0)], [Dia2(1, 24), Dia2(2, 48), Dia2(3, 72)], []).Causa);

    [Fact]
    public void AntesDasRegrasNovas_ValeARegraAntiga()
    {
        var antes = Fadiga.InicioRegras2.AddHours(-2);
        var gatilhos = new[]
        {
            new GatilhoFadiga(antes, MotivoFadiga.Falha),
            new GatilhoFadiga(antes.AddMinutes(30), MotivoFadiga.Recusa),
        };

        // Falha com 3 cargas e a segunda recusa sem somar; a carta cumprida antes também não conta.
        Assert.Equal(Fadiga.Cargas, Fadiga.Calcular(gatilhos, [], [antes.AddMinutes(45)]).Restantes);
    }

    [Fact]
    public void Revanche_SoDepoisDaUltimaCartaRecusada()
    {
        var ana = Guid.NewGuid();
        var bia = Guid.NewGuid();
        var agora = Base2.AddHours(2);
        var recusada = new JogadaLimite(ana, bia, Base2, SituacaoJogada.Recusada, Base2.AddMinutes(5));
        var revanche = new JogadaLimite(ana, bia, Base2.AddHours(1), SituacaoJogada.Expirada);

        Assert.Null(LimitesJogada.Verificar(ana, bia, [recusada], agora));
        Assert.Equal(LimitesJogada.ErroRecente, LimitesJogada.Verificar(ana, bia, [recusada, revanche], agora));
        // A carta de Bia para Ana não dá Revanche a Bia.
        Assert.Null(LimitesJogada.Verificar(bia, ana, [recusada], agora));
        // Recusa antes da decisão 0089: sem Revanche.
        var antiga = recusada with { EnviadaEm = agora.AddHours(-1), RespondidaEm = Fadiga.InicioRegras2.AddMinutes(-1) };
        Assert.Equal(LimitesJogada.ErroRecente, LimitesJogada.Verificar(ana, bia, [antiga], agora));
    }

    [Fact]
    public void Devolucao_RecusaInteiraOutrasMetade()
    {
        Assert.Equal(35, CalculoEnergia.GetDevolucao(35, inteira: true));
        Assert.Equal(17, CalculoEnergia.GetDevolucao(35));
    }

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
