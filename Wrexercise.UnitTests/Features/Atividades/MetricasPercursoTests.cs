using System.Globalization;
using System.Text.Json.Nodes;
using Wrexercise.Features.Atividades;

namespace Wrexercise.UnitTests.Features.Atividades;

/// <summary>
/// O algoritmo v1 contra os percursos de referência do Movr (ADR 0085). <c>Percursos/*.json</c> são os mesmos arquivos
/// de <c>Movr.Api.Tests/Fixtures/Routes</c>, e <c>Resultados/movr-v1.json</c> é o que o <c>RouteMetrics</c> do Movr
/// calcula para cada um: os dois aplicativos precisam dar o mesmo número.
/// </summary>
public class MetricasPercursoTests
{
    private static readonly string Pasta = Path.Combine(AppContext.BaseDirectory, "Features", "Atividades", "Percursos");

    private static readonly JsonNode Movr =
        JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, "Resultados", "movr-v1.json")))!;

    public static TheoryData<string> Percursos()
    {
        var dados = new TheoryData<string>();
        foreach (var arquivo in Directory.EnumerateFiles(Pasta, "*.json").Order(StringComparer.Ordinal))
        {
            dados.Add(Path.GetFileNameWithoutExtension(arquivo));
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(Percursos))]
    public void DaOMesmoNumeroQueOMovr(string nome)
    {
        var (tipo, pontos, barometricas) = Carregar(nome);
        var esperado = Movr[nome]!;

        var metricas = new MetricasPercurso(tipo);
        for (var i = 0; i < pontos.Count; i++)
        {
            metricas.Adicionar(pontos[i], barometricas[i]);
        }

        Assert.Equal(esperado["distanceMeters"]!.GetValue<double>(), metricas.DistanciaMetros, 0.1);
        Assert.Equal(esperado["movingSeconds"]!.GetValue<double>(), metricas.SegundosEmMovimento, 0.1);

        var maxima = esperado["maxSpeedMps"]?.GetValue<double>();
        if (maxima is null)
        {
            Assert.Null(metricas.VelocidadeMaxima());
        }
        else
        {
            Assert.Equal(maxima.Value, metricas.VelocidadeMaxima()!.Value, 0.01);
        }

        var ganho = esperado["elevationGainMeters"]?.GetValue<double>();
        if (ganho is null)
        {
            Assert.Null(metricas.GanhoElevacao);
        }
        else
        {
            Assert.Equal(ganho.Value, metricas.GanhoElevacao!.Value, 0.1);
        }

        var parciais = metricas.Parciais();
        var parciaisMovr = esperado["splits"]!.AsArray();
        Assert.Equal(parciaisMovr.Count, parciais.Count);
        for (var i = 0; i < parciais.Count; i++)
        {
            Assert.InRange(parciais[i].Metros - parciaisMovr[i]!["meters"]!.GetValue<double>(), -1, 1);
            Assert.InRange(parciais[i].Segundos - parciaisMovr[i]!["seconds"]!.GetValue<double>(), -1, 1);
        }
    }

    /// <summary>
    /// O navegador guarda um ponto a cada 3 s (ou 5 m): com um ponto em cada três, a distância continua perto da
    /// verdade do percurso de referência, dentro da mesma tolerância.
    /// </summary>
    [Theory]
    [InlineData("straight-walk")]
    [InlineData("noisy-run")]
    [InlineData("ride")]
    [InlineData("stopped-walk-jitter")]
    public void UmPontoACada3SegundosFicaPertoDaVerdade(string nome)
    {
        var (tipo, pontos, _) = Carregar(nome);
        var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, nome + ".json")))!;
        var verdade = raiz["expected"]!["distanceMeters"]!.GetValue<double>();
        var tolerancia = raiz["expected"]!["distanceTolerance"]!.GetValue<double>();

        var metricas = MetricasPercurso.Calcular(pontos.Where((_, i) => i % 3 == 0), tipo);

        Assert.InRange(metricas.DistanciaMetros, verdade * (1 - tolerancia), verdade * (1 + tolerancia));
    }

    [Fact]
    public void Calcular_UsaOAlgoritmoV1NoResumo()
    {
        var (tipo, pontos, _) = Carregar("noisy-run");
        var inicio = pontos[0].Em;

        var calculo = PercursoAtividade.Calcular(pontos, tipo, inicio, inicio.AddHours(3));

        Assert.Equal((int)Math.Round(Movr["noisy-run"]!["distanceMeters"]!.GetValue<double>()), calculo.DistanciaMetros);
        Assert.Equal(Movr["noisy-run"]!["splits"]!.AsArray().Count, calculo.Parciais.Count);
    }

    private static (string Tipo, List<PontoPercurso> Pontos, List<bool> Barometricas) Carregar(string nome)
    {
        var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, nome + ".json")))!;
        var tipo = raiz["kind"]!.GetValue<string>() switch
        {
            "Run" => "Corrida",
            "Cycling" => "Ciclismo",
            _ => "Caminhada",
        };
        var inicio = DateTimeOffset.Parse(raiz["startedAtUtc"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        var pontos = new List<PontoPercurso>();
        var barometricas = new List<bool>();
        var seq = 0;
        foreach (var p in raiz["points"]!.AsArray())
        {
            pontos.Add(new PontoPercurso(
                seq++,
                inicio.AddMilliseconds(Math.Round(p!["t"]!.GetValue<double>() * 1_000)),
                p["lat"]!.GetValue<double>(),
                p["lon"]!.GetValue<double>(),
                p["acc"]!.GetValue<double>(),
                p["alt"]?.GetValue<double>(),
                p["seg"]!.GetValue<int>()));
            barometricas.Add(p["baro"]?.GetValue<bool>() ?? false);
        }

        return (tipo, pontos, barometricas);
    }
}
