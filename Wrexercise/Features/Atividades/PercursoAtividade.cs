using Wrexercise.Features.Atividades.Contracts;

namespace Wrexercise.Features.Atividades;

/// <summary>Um ponto do Percurso como o navegador o mandou (ADR 0084).</summary>
internal readonly record struct PontoPercurso(
    int Seq,
    DateTimeOffset Em,
    double Latitude,
    double Longitude,
    double Precisao,
    double? Altitude,
    int Segmento);

/// <summary>Um trecho contínuo do percurso: entre dois trechos houve uma lacuna, e a distância não conta.</summary>
internal sealed record SegmentoPercurso(IReadOnlyList<PontoPercurso> Pontos);

internal readonly record struct BboxPercurso(double LatMin, double LonMin, double LatMax, double LonMax);

/// <summary>Uma parcial: o trecho de <c>Metros</c> (1 km, ou 5 km no Ciclismo; a última pode ser menor).</summary>
internal sealed record ParcialPercurso(int Metros, int Segundos);

/// <summary>O que o servidor calcula de um percurso, sempre a partir dos pontos crus.</summary>
internal sealed record CalculoPercurso(
    IReadOnlyList<SegmentoPercurso> Segmentos,
    int DistanciaMetros,
    double? VelocidadeMaxima,
    int? GanhoElevacaoMetros);

/// <summary>
/// Regras puras do Percurso (ADR 0084). Os pontos vêm do navegador e podem ser forjados: a distância aparece para o
/// grupo, mas nunca conta para o progresso, a Energia ou a classificação. A cópia em TypeScript para a estimativa ao
/// vivo fica em <c>webapp/src/features/percurso/calculo.ts</c>, com as mesmas constantes.
/// </summary>
internal static class PercursoAtividade
{
    /// <summary>Acima disso, o ponto é ruído: o GPS ainda não firmou ou está dentro de um prédio.</summary>
    public const double PrecisaoMaximaMetros = 30;

    /// <summary>O começo e o fim que o grupo não vê, para não mostrar onde a pessoa mora.</summary>
    public const double AparoMetros = 200;

    /// <summary>Mais tempo que isso sem ponto vira lacuna: a tela apagou, o app saiu da frente ou o sinal caiu.</summary>
    public static readonly TimeSpan Lacuna = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Parado, o GPS treme alguns metros. Só conta o deslocamento a partir disso, ou a partir da precisão média dos
    /// dois pontos, se for maior: um GPS de 8 metros que oscila de um lado a outro não soma distância.
    /// </summary>
    public const double DeslocamentoMinimoMetros = 5;

    /// <summary>O deslocamento mínimo entre dois pontos para contar distância.</summary>
    public static double LimiarDeslocamento(PontoPercurso a, PontoPercurso b) =>
        Math.Max(DeslocamentoMinimoMetros, (a.Precisao + b.Precisao) / 2);

    /// <summary>Quantos pontos seguidos e coerentes entre si, depois de um salto, viram um trecho novo.</summary>
    public const int PontosParaRelocar = 3;

    /// <summary>A janela mínima da velocidade máxima: nunca entre dois pontos soltos.</summary>
    public static readonly TimeSpan JanelaVelocidadeMaxima = TimeSpan.FromSeconds(10);

    public const int MaxPontosPorAtividade = 20_000;
    public const int MaxPontosPorLote = 500;
    public const int MaxPontosSimplificados = 600;

    private const double RaioTerraMetros = 6_371_000;

    private static readonly HashSet<string> Tipos = [TipoAtividade.Corrida, TipoAtividade.Caminhada, TipoAtividade.Ciclismo];

    public static bool IsTipoComPercurso(string? tipo) => tipo is not null && Tipos.Contains(tipo);

    /// <summary>A velocidade acima da qual um salto entre dois pontos é erro do GPS, em m/s.</summary>
    public static double VelocidadeLimite(string tipo) => tipo switch
    {
        TipoAtividade.Caminhada => 6,
        TipoAtividade.Corrida => 10,
        _ => 25,
    };

    /// <summary>O tamanho de cada parcial: 1 km, ou 5 km no Ciclismo.</summary>
    public static int MetrosPorParcial(string tipo) => tipo == TipoAtividade.Ciclismo ? 5000 : 1000;

    /// <summary>A distância mínima para um recorde: 1 km, ou 5 km no Ciclismo.</summary>
    public static int MetrosMinimosRecorde(string tipo) => tipo == TipoAtividade.Ciclismo ? 5000 : 1000;

    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RaioTerraMetros * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static double Haversine(PontoPercurso a, PontoPercurso b) =>
        Haversine(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

    /// <summary>Um ponto que o servidor aceita guardar; os demais são descartados em silêncio.</summary>
    public static bool IsPontoValido(PontoPercurso ponto, DateTimeOffset startedAt, DateTimeOffset agora) =>
        ponto.Seq is >= 0 and <= 100_000
        && ponto.Segmento is >= 0 and <= short.MaxValue
        && ponto.Latitude is >= -90 and <= 90
        && ponto.Longitude is >= -180 and <= 180
        && !(ponto.Latitude == 0 && ponto.Longitude == 0)
        && double.IsFinite(ponto.Precisao) && ponto.Precisao is >= 0 and <= PrecisaoMaximaMetros
        && (ponto.Altitude is null || (double.IsFinite(ponto.Altitude.Value) && ponto.Altitude is > -500 and < 9000))
        && ponto.Em >= startedAt.AddSeconds(-60)
        && ponto.Em <= agora.AddSeconds(60);

    /// <summary>Tudo o que o resumo guarda, a partir dos pontos crus e do período do treino.</summary>
    public static CalculoPercurso Calcular(
        IEnumerable<PontoPercurso> pontos,
        string tipo,
        DateTimeOffset inicio,
        DateTimeOffset fim)
    {
        var segmentos = Filtrar(pontos, tipo, inicio, fim);
        return new CalculoPercurso(
            segmentos,
            (int)Math.Round(Distancia(segmentos)),
            VelocidadeMaxima(segmentos),
            GanhoElevacao(segmentos));
    }

    /// <summary>
    /// Os trechos válidos dentro de [<paramref name="inicio"/>, <paramref name="fim"/>]: sem pontos imprecisos, sem o
    /// tempo voltando e sem saltos impossíveis para o tipo. Um trecho novo começa numa lacuna, quando o aparelho
    /// reiniciou a gravação (outro <c>Segmento</c>) ou quando 3 pontos seguidos confirmam um salto.
    /// </summary>
    public static IReadOnlyList<SegmentoPercurso> Filtrar(
        IEnumerable<PontoPercurso> pontos,
        string tipo,
        DateTimeOffset inicio,
        DateTimeOffset fim)
    {
        var limite = VelocidadeLimite(tipo);
        var segmentos = new List<SegmentoPercurso>();
        List<PontoPercurso>? atual = null;
        var rejeitados = new List<PontoPercurso>();

        void Fechar()
        {
            if (atual is { Count: >= 2 })
            {
                segmentos.Add(new SegmentoPercurso(atual));
            }

            atual = null;
        }

        DateTimeOffset? ultimoInstante = null;
        foreach (var ponto in pontos.OrderBy(p => p.Seq))
        {
            if (ponto.Precisao > PrecisaoMaximaMetros || ponto.Em < inicio || ponto.Em > fim
                || (ultimoInstante is { } antes && ponto.Em <= antes))
            {
                continue;
            }

            ultimoInstante = ponto.Em;

            if (atual is null)
            {
                atual = [ponto];
                continue;
            }

            var ultimo = atual[^1];
            if (ponto.Segmento != ultimo.Segmento || ponto.Em - ultimo.Em > Lacuna)
            {
                Fechar();
                rejeitados.Clear();
                atual = [ponto];
                continue;
            }

            if (Velocidade(ultimo, ponto) <= limite)
            {
                rejeitados.Clear();
                atual.Add(ponto);
                continue;
            }

            // Um salto: ou é um erro isolado do GPS, ou a pessoa reapareceu em outro lugar. Três pontos seguidos e
            // coerentes entre si confirmam o lugar novo, sem contar a distância do salto.
            if (rejeitados.Count > 0
                && (Velocidade(rejeitados[^1], ponto) > limite || ponto.Em - rejeitados[^1].Em > Lacuna))
            {
                rejeitados.Clear();
            }

            rejeitados.Add(ponto);
            if (rejeitados.Count >= PontosParaRelocar)
            {
                Fechar();
                atual = [.. rejeitados];
                rejeitados.Clear();
            }
        }

        Fechar();
        return segmentos;
    }

    private static double Velocidade(PontoPercurso a, PontoPercurso b)
    {
        var segundos = (b.Em - a.Em).TotalSeconds;
        return segundos <= 0 ? double.PositiveInfinity : Haversine(a, b) / segundos;
    }

    /// <summary>
    /// Os pontos de cada trecho que contam para a distância: o próximo só conta a 5 metros ou mais do último que
    /// contou (ou à precisão média, se maior), para que o tremor do GPS parado não some quilômetros.
    /// </summary>
    private static List<PontoPercurso> PontosContados(SegmentoPercurso segmento)
    {
        var contados = new List<PontoPercurso> { segmento.Pontos[0] };
        foreach (var ponto in segmento.Pontos.Skip(1))
        {
            if (Haversine(contados[^1], ponto) >= LimiarDeslocamento(contados[^1], ponto))
            {
                contados.Add(ponto);
            }
        }

        // O último ponto marca o fim do trecho no tempo, mesmo parado.
        if (contados[^1] != segmento.Pontos[^1])
        {
            contados.Add(segmento.Pontos[^1]);
        }

        return contados;
    }

    /// <summary>A soma dos trechos, em metros. A distância entre dois trechos nunca conta.</summary>
    public static double Distancia(IReadOnlyList<SegmentoPercurso> segmentos)
    {
        var total = 0.0;
        foreach (var segmento in segmentos)
        {
            var contados = PontosContados(segmento);
            for (var i = 1; i < contados.Count; i++)
            {
                total += Haversine(contados[i - 1], contados[i]);
            }
        }

        return total;
    }

    /// <summary>A maior velocidade média numa janela de 10 segundos ou mais, em m/s; <c>null</c> sem janela.</summary>
    public static double? VelocidadeMaxima(IReadOnlyList<SegmentoPercurso> segmentos)
    {
        double? maxima = null;
        foreach (var segmento in segmentos)
        {
            var contados = PontosContados(segmento);
            var acumulado = new double[contados.Count];
            for (var i = 1; i < contados.Count; i++)
            {
                acumulado[i] = acumulado[i - 1] + Haversine(contados[i - 1], contados[i]);
            }

            var j = 0;
            for (var i = 0; i < contados.Count; i++)
            {
                if (j < i)
                {
                    j = i;
                }

                while (j < contados.Count && contados[j].Em - contados[i].Em < JanelaVelocidadeMaxima)
                {
                    j++;
                }

                if (j >= contados.Count)
                {
                    break;
                }

                var velocidade = (acumulado[j] - acumulado[i]) / (contados[j].Em - contados[i].Em).TotalSeconds;
                if (maxima is null || velocidade > maxima)
                {
                    maxima = velocidade;
                }
            }
        }

        return maxima;
    }

    /// <summary>
    /// A subida acumulada, em metros, aproximada: a altitude do GPS oscila muito. Média móvel de 5 pontos e só
    /// conta uma subida de 3 metros ou mais. <c>null</c> quando menos da metade dos pontos tem altitude.
    /// </summary>
    public static int? GanhoElevacao(IReadOnlyList<SegmentoPercurso> segmentos)
    {
        var total = segmentos.Sum(s => s.Pontos.Count);
        var comAltitude = segmentos.Sum(s => s.Pontos.Count(p => p.Altitude is not null));
        if (total == 0 || comAltitude * 2 < total)
        {
            return null;
        }

        const int janela = 5;
        const double histerese = 3;
        var ganho = 0.0;
        foreach (var segmento in segmentos)
        {
            var altitudes = segmento.Pontos.Where(p => p.Altitude is not null).Select(p => p.Altitude!.Value).ToList();
            if (altitudes.Count < janela)
            {
                continue;
            }

            double? referencia = null;
            for (var i = 0; i + janela <= altitudes.Count; i++)
            {
                var media = altitudes.Skip(i).Take(janela).Average();
                if (referencia is not { } r)
                {
                    referencia = media;
                }
                else if (media - r >= histerese)
                {
                    ganho += media - r;
                    referencia = media;
                }
                else if (r - media >= histerese)
                {
                    referencia = media;
                }
            }
        }

        return (int)Math.Round(ganho);
    }

    /// <summary>
    /// As parciais de 1 km (5 km no Ciclismo), com o tempo em movimento de cada uma: as lacunas não contam. O tempo
    /// da parcial é interpolado no ponto exato da marca. A última fica com o que sobrou, se passar de 50 metros.
    /// </summary>
    public static IReadOnlyList<ParcialPercurso> Parciais(IReadOnlyList<SegmentoPercurso> segmentos, string tipo)
    {
        var tamanho = MetrosPorParcial(tipo);
        var parciais = new List<ParcialPercurso>();
        var metrosNaParcial = 0.0;
        var segundosNaParcial = 0.0;

        foreach (var segmento in segmentos)
        {
            var contados = PontosContados(segmento);
            for (var i = 1; i < contados.Count; i++)
            {
                var metros = Haversine(contados[i - 1], contados[i]);
                var segundos = (contados[i].Em - contados[i - 1].Em).TotalSeconds;
                while (metros > 0 && metrosNaParcial + metros >= tamanho)
                {
                    var falta = tamanho - metrosNaParcial;
                    var fracao = falta / metros;
                    parciais.Add(new ParcialPercurso(tamanho, (int)Math.Round(segundosNaParcial + segundos * fracao)));
                    metros -= falta;
                    segundos -= segundos * fracao;
                    metrosNaParcial = 0;
                    segundosNaParcial = 0;
                }

                metrosNaParcial += metros;
                segundosNaParcial += segundos;
            }
        }

        if (metrosNaParcial >= 50)
        {
            parciais.Add(new ParcialPercurso((int)Math.Round(metrosNaParcial), (int)Math.Round(segundosNaParcial)));
        }

        return parciais;
    }

    /// <summary>
    /// O percurso sem os primeiros e os últimos 200 metros em linha reta, que mostrariam a casa de quem treina.
    /// Ao vivo, o último ponto é onde a pessoa está agora: o grupo vê o trajeto uns 200 metros atrás.
    /// Vazio quando o percurso inteiro fica perto do começo ou do fim.
    /// </summary>
    public static IReadOnlyList<SegmentoPercurso> Aparar(IReadOnlyList<SegmentoPercurso> segmentos)
    {
        var todos = segmentos.SelectMany((s, i) => s.Pontos.Select(p => (Indice: i, Ponto: p))).ToList();
        if (todos.Count == 0)
        {
            return [];
        }

        var primeiro = todos[0].Ponto;
        var ultimo = todos[^1].Ponto;
        var de = todos.FindIndex(t => Haversine(primeiro, t.Ponto) > AparoMetros);
        var ate = todos.FindLastIndex(t => Haversine(ultimo, t.Ponto) > AparoMetros);
        if (de < 0 || ate < 0 || de > ate)
        {
            return [];
        }

        return todos.Skip(de).Take(ate - de + 1)
            .GroupBy(t => t.Indice)
            .Select(g => g.Select(t => t.Ponto).ToList())
            .Where(p => p.Count >= 2)
            .Select(p => new SegmentoPercurso(p))
            .ToList();
    }

    /// <summary>
    /// O desenho com no máximo <paramref name="maximo"/> pontos (Douglas–Peucker), para a resposta e o mapa. A
    /// tolerância começa em 3 metros e dobra até caber. As pontas de cada trecho ficam sempre.
    /// </summary>
    public static IReadOnlyList<SegmentoPercurso> Simplificar(
        IReadOnlyList<SegmentoPercurso> segmentos,
        int maximo = MaxPontosSimplificados)
    {
        var tolerancia = 3.0;
        var resultado = segmentos;
        while (resultado.Sum(s => s.Pontos.Count) > maximo && tolerancia < 100_000)
        {
            resultado = segmentos.Select(s => new SegmentoPercurso(DouglasPeucker(s.Pontos, tolerancia))).ToList();
            tolerancia *= 2;
        }

        return resultado;
    }

    private static List<PontoPercurso> DouglasPeucker(IReadOnlyList<PontoPercurso> pontos, double tolerancia)
    {
        if (pontos.Count <= 2)
        {
            return [.. pontos];
        }

        var manter = new bool[pontos.Count];
        manter[0] = manter[^1] = true;
        var pilha = new Stack<(int De, int Ate)>();
        pilha.Push((0, pontos.Count - 1));
        while (pilha.Count > 0)
        {
            var (de, ate) = pilha.Pop();
            var maior = 0.0;
            var indice = -1;
            for (var i = de + 1; i < ate; i++)
            {
                var d = DistanciaAteReta(pontos[i], pontos[de], pontos[ate]);
                if (d > maior)
                {
                    maior = d;
                    indice = i;
                }
            }

            if (indice >= 0 && maior > tolerancia)
            {
                manter[indice] = true;
                pilha.Push((de, indice));
                pilha.Push((indice, ate));
            }
        }

        return pontos.Where((_, i) => manter[i]).ToList();
    }

    /// <summary>A distância do ponto até a reta entre <paramref name="a"/> e <paramref name="b"/>, em metros (plano local).</summary>
    private static double DistanciaAteReta(PontoPercurso p, PontoPercurso a, PontoPercurso b)
    {
        var cosLat = Math.Cos(a.Latitude * Math.PI / 180);
        const double metrosPorGrau = RaioTerraMetros * Math.PI / 180;
        var ax = 0.0;
        var ay = 0.0;
        var bx = (b.Longitude - a.Longitude) * metrosPorGrau * cosLat;
        var by = (b.Latitude - a.Latitude) * metrosPorGrau;
        var px = (p.Longitude - a.Longitude) * metrosPorGrau * cosLat;
        var py = (p.Latitude - a.Latitude) * metrosPorGrau;
        var dx = bx - ax;
        var dy = by - ay;
        var comprimento = dx * dx + dy * dy;
        if (comprimento == 0)
        {
            return Math.Sqrt(px * px + py * py);
        }

        var t = Math.Clamp((px * dx + py * dy) / comprimento, 0, 1);
        var cx = t * dx - px;
        var cy = t * dy - py;
        return Math.Sqrt(cx * cx + cy * cy);
    }

    public static BboxPercurso? Bbox(IEnumerable<SegmentoPercurso> segmentos)
    {
        var pontos = segmentos.SelectMany(s => s.Pontos).ToList();
        return pontos.Count == 0
            ? null
            : new BboxPercurso(
                pontos.Min(p => p.Latitude),
                pontos.Min(p => p.Longitude),
                pontos.Max(p => p.Latitude),
                pontos.Max(p => p.Longitude));
    }
}
