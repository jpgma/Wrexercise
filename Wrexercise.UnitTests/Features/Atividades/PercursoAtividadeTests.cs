using Wrexercise.Features.Atividades;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.UnitTests.Features.Atividades;

public class PercursoAtividadeTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 21, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = Inicio.AddHours(3);

    /// <summary>Um grau de latitude tem uns 111,2 km: metros para o norte viram graus.</summary>
    private const double MetrosPorGrau = 6_371_000 * Math.PI / 180;

    private const double LatBase = -23.55;
    private const double LonBase = -46.63;

    /// <summary>Pontos em linha reta para o norte, a <paramref name="metrosPorPasso"/> a cada <paramref name="segundos"/>.</summary>
    private static List<PontoPercurso> Reta(
        int passos,
        double metrosPorPasso,
        double segundos = 5,
        int seqInicial = 0,
        double metrosIniciais = 0,
        DateTimeOffset? de = null,
        int segmento = 0,
        double? altitude = null)
    {
        var comeco = de ?? Inicio;
        return Enumerable.Range(0, passos)
            .Select(i => new PontoPercurso(
                seqInicial + i,
                comeco.AddSeconds(i * segundos),
                LatBase + (metrosIniciais + i * metrosPorPasso) / MetrosPorGrau,
                LonBase,
                5,
                altitude,
                segmento))
            .ToList();
    }

    [Fact]
    public void Haversine_SaoPauloAoRio()
    {
        var metros = PercursoAtividade.Haversine(-23.5505, -46.6333, -22.9068, -43.1729);
        Assert.InRange(metros, 355_000, 362_000);
    }

    [Fact]
    public void Haversine_CemMetrosParaONorte()
    {
        var metros = PercursoAtividade.Haversine(LatBase, LonBase, LatBase + 100 / MetrosPorGrau, LonBase);
        Assert.InRange(metros, 99.9, 100.1);
    }

    /// <summary>
    /// O algoritmo v1 (ADR 0085) desconta das cordas o ruído que a precisão anuncia: pontos sintéticos perfeitos,
    /// sem ruído nenhum, ficam até uns 6% abaixo da reta. Os percursos de referência, com ruído de verdade, ficam
    /// perto da verdade (<see cref="MetricasPercursoTests"/>).
    /// </summary>
    private const double DescontoPontosPerfeitos = 0.94;

    [Fact]
    public void Calcular_SomaAReta()
    {
        // 1 km em 200 passos de 5 m (a cada 5 s: 1 m/s, uma caminhada).
        var calculo = PercursoAtividade.Calcular(Reta(201, 5), "Caminhada", Inicio, Fim);
        Assert.InRange(calculo.DistanciaMetros, 1000 * DescontoPontosPerfeitos, 1005);
        Assert.Single(calculo.Segmentos);
    }

    [Fact]
    public void Filtrar_IgnoraPontoImpreciso()
    {
        var pontos = Reta(10, 5);
        pontos[5] = pontos[5] with { Precisao = 80, Latitude = pontos[5].Latitude + 0.01 };
        var calculo = PercursoAtividade.Calcular(pontos, "Caminhada", Inicio, Fim);
        Assert.InRange(calculo.DistanciaMetros, 44, 46);
    }

    [Fact]
    public void Filtrar_DescartaSaltoIsolado()
    {
        // 200 m em 5 s numa caminhada: erro do GPS. O ponto seguinte volta para a reta.
        var pontos = Reta(10, 5);
        pontos[5] = pontos[5] with { Latitude = pontos[5].Latitude + 200 / MetrosPorGrau };
        var calculo = PercursoAtividade.Calcular(pontos, "Caminhada", Inicio, Fim);
        Assert.InRange(calculo.DistanciaMetros, 44, 46);
        Assert.Single(calculo.Segmentos);
    }

    [Fact]
    public void Filtrar_TresPontosCoerentesDepoisDoSaltoViramTrechoNovo()
    {
        var antes = Reta(10, 5);
        var depois = Reta(10, 5, seqInicial: 10, metrosIniciais: 2000, de: Inicio.AddSeconds(50));
        var calculo = PercursoAtividade.Calcular([.. antes, .. depois], "Caminhada", Inicio, Fim);

        Assert.Equal(2, calculo.Segmentos.Count);
        // O salto de 2 km não conta: no máximo os dois trechos de 45 m.
        Assert.InRange(calculo.DistanciaMetros, 40, 95);
    }

    [Fact]
    public void Lacuna_AteDoisMinutosComVelocidadePlausivelSomaAReta()
    {
        var antes = Reta(10, 5);
        // Um túnel (ou a tela apagada) por 2 min; a pessoa andou 300 m nesse tempo.
        var depois = Reta(10, 5, seqInicial: 10, metrosIniciais: 345, de: Inicio.AddSeconds(45 + 120));
        var calculo = PercursoAtividade.Calcular([.. antes, .. depois], "Corrida", Inicio, Fim);

        // O desenho continua com dois trechos (a lacuna tracejada); a distância soma a reta de 300 m.
        Assert.Equal(2, calculo.Segmentos.Count);
        Assert.InRange(calculo.DistanciaMetros, 300, 395);
    }

    [Fact]
    public void Lacuna_MaisDeDoisMinutosNaoContaDistancia()
    {
        var antes = Reta(10, 5);
        var depois = Reta(10, 5, seqInicial: 10, metrosIniciais: 345, de: Inicio.AddSeconds(45 + 180));
        var calculo = PercursoAtividade.Calcular([.. antes, .. depois], "Corrida", Inicio, Fim);

        Assert.Equal(2, calculo.Segmentos.Count);
        Assert.InRange(calculo.DistanciaMetros, 0, 95);
    }

    [Fact]
    public void Filtrar_OutroSegmentoDoAparelhoComecaTrechoNovo()
    {
        var antes = Reta(10, 5);
        var depois = Reta(10, 5, seqInicial: 10, metrosIniciais: 50, de: Inicio.AddSeconds(50), segmento: 1);
        Assert.Equal(2, PercursoAtividade.Filtrar([.. antes, .. depois], "Caminhada", Inicio, Fim).Count);
    }

    [Fact]
    public void Distancia_ParadoComTremorDoGpsFicaPertoDeZero()
    {
        // 5 minutos parado, com o GPS tremendo até 3 m para os lados.
        var pontos = Enumerable.Range(0, 100)
            .Select(i => new PontoPercurso(
                i,
                Inicio.AddSeconds(i * 3),
                LatBase + (i % 3 - 1) * 3 / MetrosPorGrau,
                LonBase,
                8,
                null,
                0))
            .ToList();
        var calculo = PercursoAtividade.Calcular(pontos, "Caminhada", Inicio, Fim);
        Assert.InRange(calculo.DistanciaMetros, 0, 6);
    }

    [Fact]
    public void Calcular_CortaNoFimInformado()
    {
        // 30 min de pontos, mas a pessoa terminou dizendo 10 min.
        var pontos = Reta(361, 5);
        var completo = PercursoAtividade.Calcular(pontos, "Caminhada", Inicio, Fim);
        var cortado = PercursoAtividade.Calcular(pontos, "Caminhada", Inicio, Inicio.AddMinutes(10));

        Assert.InRange(completo.DistanciaMetros, 1800 * DescontoPontosPerfeitos, 1805);
        Assert.InRange(cortado.DistanciaMetros, 600 * DescontoPontosPerfeitos, 605);
    }

    [Fact]
    public void Parciais_CorridaDe5Km3TemSeisParciais()
    {
        // 5,3 km a 10 m a cada 3 s (3,33 m/s).
        var pontos = Reta(531, 10, segundos: 3);
        var parciais = PercursoAtividade.Calcular(pontos, "Corrida", Inicio, Fim).Parciais;

        Assert.Equal(6, parciais.Count);
        Assert.All(parciais.Take(5), p => Assert.Equal(1000, p.Metros));
        Assert.All(parciais.Take(5), p => Assert.InRange(p.Segundos, 299, 300 / DescontoPontosPerfeitos));
        Assert.InRange(parciais[5].Metros, 1, 305);
    }

    [Fact]
    public void Parciais_CiclismoUsaTrechosDe5Km()
    {
        // 12 km a 20 m a cada 3 s.
        var pontos = Reta(601, 20, segundos: 3);
        var parciais = PercursoAtividade.Calcular(pontos, "Ciclismo", Inicio, Fim).Parciais;

        Assert.Equal([5000, 5000], parciais.Take(2).Select(p => p.Metros));
        Assert.InRange(parciais[2].Metros, 2000 - (12_000 * (1 - DescontoPontosPerfeitos)), 2000);
    }

    [Fact]
    public void VelocidadeMaxima_IgnoraUmPicoDeUmPonto()
    {
        // A 1 m/s, um ponto pula 10 m a mais e volta: uma janela de 10 s ou mais dilui o pico.
        var pontos = Reta(60, 5);
        pontos[30] = pontos[30] with { Latitude = pontos[30].Latitude + 10 / MetrosPorGrau };
        var maxima = PercursoAtividade.Calcular(pontos, "Corrida", Inicio, Fim).VelocidadeMaxima;

        Assert.NotNull(maxima);
        Assert.InRange(maxima.Value, 1 * DescontoPontosPerfeitos * 0.8, 2.5);
    }

    [Fact]
    public void GanhoElevacao_RuidoNoPlanoDaZero()
    {
        var pontos = Reta(100, 5).Select((p, i) => p with { Altitude = 800 + (i % 2 == 0 ? 2 : -2) }).ToList();
        Assert.Equal(0, PercursoAtividade.Calcular(pontos, "Corrida", Inicio, Fim).GanhoElevacaoMetros);
    }

    [Fact]
    public void GanhoElevacao_SomaASubida()
    {
        var pontos = Reta(100, 5).Select((p, i) => p with { Altitude = 800 + i * 0.5 }).ToList();
        Assert.InRange(
            PercursoAtividade.Calcular(pontos, "Corrida", Inicio, Fim).GanhoElevacaoMetros!.Value,
            45,
            50);
    }

    [Fact]
    public void GanhoElevacao_SemAltitudeEhNulo()
    {
        Assert.Null(PercursoAtividade.Calcular(Reta(20, 5), "Corrida", Inicio, Fim).GanhoElevacaoMetros);
    }

    [Fact]
    public void Aparar_TiraOs200MetrosDoComecoEDoFim()
    {
        var segmentos = PercursoAtividade.Filtrar(Reta(201, 5), "Caminhada", Inicio, Fim);
        var aparado = PercursoAtividade.Aparar(segmentos);

        var pontos = aparado.SelectMany(s => s.Pontos).ToList();
        var primeiro = segmentos[0].Pontos[0];
        var ultimo = segmentos[^1].Pontos[^1];
        Assert.All(pontos, p => Assert.True(PercursoAtividade.Haversine(primeiro, p) > 200));
        Assert.All(pontos, p => Assert.True(PercursoAtividade.Haversine(ultimo, p) > 200));
        Assert.InRange(PercursoAtividade.Haversine(pontos[0], pontos[^1]), 590, 600);
    }

    [Fact]
    public void Aparar_PercursoCurtoFicaVazio()
    {
        // Uma volta no quarteirão: tudo fica a menos de 200 m do começo ou do fim.
        var segmentos = PercursoAtividade.Filtrar(Reta(60, 5), "Caminhada", Inicio, Fim);
        Assert.Empty(PercursoAtividade.Aparar(segmentos));
    }

    [Fact]
    public void Simplificar_CabeNoMaximoEMantemAsPontas()
    {
        // Um zigue-zague de 3.000 pontos.
        var pontos = Enumerable.Range(0, 3000)
            .Select(i => new PontoPercurso(
                i,
                Inicio.AddSeconds(i * 3),
                LatBase + i * 5 / MetrosPorGrau,
                LonBase + (i % 20 < 10 ? i % 10 : 10 - i % 10) * 4 / MetrosPorGrau,
                5,
                null,
                0))
            .ToList();
        var segmentos = PercursoAtividade.Filtrar(pontos, "Ciclismo", Inicio, Fim);
        var simples = PercursoAtividade.Simplificar(segmentos);

        Assert.True(simples.Sum(s => s.Pontos.Count) <= PercursoAtividade.MaxPontosSimplificados);
        Assert.Equal(segmentos[0].Pontos[0], simples[0].Pontos[0]);
        Assert.Equal(segmentos[^1].Pontos[^1], simples[^1].Pontos[^1]);
    }

    [Theory]
    [InlineData("Corrida", true)]
    [InlineData("Caminhada", true)]
    [InlineData("Ciclismo", true)]
    [InlineData("Natacao", false)]
    [InlineData(null, false)]
    public void IsTipoComPercurso(string? tipo, bool esperado) =>
        Assert.Equal(esperado, PercursoAtividade.IsTipoComPercurso(tipo));

    [Fact]
    public void IsPontoValido_RecusaZeroZeroEPrecisaoRuim()
    {
        var ponto = Reta(1, 0)[0];
        Assert.True(PercursoAtividade.IsPontoValido(ponto, Inicio, Inicio.AddMinutes(1)));
        Assert.False(PercursoAtividade.IsPontoValido(ponto with { Latitude = 0, Longitude = 0 }, Inicio, Inicio));
        Assert.False(PercursoAtividade.IsPontoValido(ponto with { Precisao = 31 }, Inicio, Inicio));
        Assert.False(PercursoAtividade.IsPontoValido(ponto with { Em = Inicio.AddMinutes(-5) }, Inicio, Inicio));
        Assert.False(PercursoAtividade.IsPontoValido(ponto with { Em = Inicio.AddMinutes(5) }, Inicio, Inicio));
    }

    [Fact]
    public void Recorde_PrimeiroTreinoNaoTemRecorde() =>
        Assert.Null(RecordesPercurso.Calcular("Corrida", 5000, 1500, new MelhoresAnteriores(0, null, null)));

    [Fact]
    public void Recorde_AbaixoDoMinimoNaoTemRecorde()
    {
        Assert.Null(RecordesPercurso.Calcular("Corrida", 800, 240, new MelhoresAnteriores(3, 500, 2)));
        Assert.Null(RecordesPercurso.Calcular("Ciclismo", 4000, 600, new MelhoresAnteriores(3, 3000, 5)));
    }

    [Fact]
    public void Recorde_DistanciaVemAntesDoRitmo() =>
        Assert.Equal(
            TipoRecorde.Distancia,
            RecordesPercurso.Calcular("Corrida", 6000, 1500, new MelhoresAnteriores(2, 5000, 3)));

    [Fact]
    public void Recorde_RitmoMelhor() =>
        Assert.Equal(
            TipoRecorde.Ritmo,
            RecordesPercurso.Calcular("Corrida", 3000, 900, new MelhoresAnteriores(2, 5000, 3)));

    [Fact]
    public void Recorde_EmpateNaoEhRecorde() =>
        Assert.Null(RecordesPercurso.Calcular("Corrida", 5000, 1500, new MelhoresAnteriores(2, 5000, 5000.0 / 1500)));

    [Fact]
    public void Parceria_MesmoCaminhoPor20Minutos()
    {
        var meus = PercursoAtividade.Filtrar(Reta(241, 5), "Caminhada", Inicio, Fim);
        var deles = PercursoAtividade.Filtrar(
            Reta(241, 5).Select(p => p with { Longitude = p.Longitude + 10 / MetrosPorGrau }).ToList(),
            "Caminhada",
            Inicio,
            Fim);
        Assert.True(ParceriaPercurso.TreinaramJuntos(meus, deles));
    }

    [Fact]
    public void Parceria_A80MetrosNaoEhJunto()
    {
        var meus = PercursoAtividade.Filtrar(Reta(241, 5), "Caminhada", Inicio, Fim);
        var deles = PercursoAtividade.Filtrar(
            Reta(241, 5).Select(p => p with { Longitude = p.Longitude + 80 / MetrosPorGrau }).ToList(),
            "Caminhada",
            Inicio,
            Fim);
        Assert.False(ParceriaPercurso.TreinaramJuntos(meus, deles));
    }

    [Fact]
    public void Parceria_MenosDe15MinutosJuntosNaoConta()
    {
        // 14 min: 168 pontos a cada 5 s.
        var meus = PercursoAtividade.Filtrar(Reta(169, 5), "Caminhada", Inicio, Fim);
        var deles = PercursoAtividade.Filtrar(Reta(169, 5), "Caminhada", Inicio, Fim);
        Assert.False(ParceriaPercurso.TreinaramJuntos(meus, deles));
    }

    [Fact]
    public void Mapa_PermiteOsTilesDoPercursoComUmDeMargem()
    {
        var bbox = new BboxPercurso(LatBase, LonBase, LatBase + 0.01, LonBase + 0.01);
        var centro = TileMapa.De(LatBase + 0.005, LonBase + 0.005, 15);
        var noroeste = TileMapa.De(LatBase + 0.01, LonBase, 15);

        Assert.True(MapaPercurso.IsTilePermitido(bbox, centro));
        Assert.True(MapaPercurso.IsTilePermitido(bbox, noroeste with { X = noroeste.X - 1, Y = noroeste.Y - 1 }));
        Assert.False(MapaPercurso.IsTilePermitido(bbox, noroeste with { X = noroeste.X - 2 }));
    }

    [Fact]
    public void Mapa_RecusaZoomForaDosLimitesETilesDemais()
    {
        var pequeno = new BboxPercurso(LatBase, LonBase, LatBase + 0.001, LonBase + 0.001);
        Assert.False(MapaPercurso.IsTilePermitido(pequeno, TileMapa.De(LatBase, LonBase, 18)));
        Assert.False(MapaPercurso.IsTilePermitido(pequeno, TileMapa.De(LatBase, LonBase, 11)));

        // Uns 33 km de lado: tiles demais no zoom 17, uns 16 no zoom 12.
        var enorme = new BboxPercurso(LatBase, LonBase, LatBase + 0.3, LonBase + 0.3);
        Assert.False(MapaPercurso.IsTilePermitido(enorme, TileMapa.De(LatBase, LonBase, 17)));
        Assert.True(MapaPercurso.IsTilePermitido(enorme, TileMapa.De(LatBase, LonBase, 12)));
    }

    [Fact]
    public void AvisoData_ImagemDoPercursoNoFimNaoTemAviso() =>
        Assert.Null(AvisosEvidencia.CalcularAvisoData(
            Inicio, 1800, Inicio.AddSeconds(1800), OrigemDataCaptura.Percurso, Inicio.AddHours(1)));
}
