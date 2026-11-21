namespace Wrexercise.Features.Atividades;

/// <summary>
/// "Treinaram juntos" (ADR 0084): dois percursos que andaram lado a lado. Só aparece na atividade e no mapa; a
/// Parceria da Energia continua pela sobreposição de tempo (ADR 0078), com ou sem percurso.
/// </summary>
internal static class ParceriaPercurso
{
    /// <summary>A mesma sobreposição da Parceria (<c>CalculoEnergia.SobreposicaoParceria</c>, na fatia Jogo).</summary>
    public static readonly TimeSpan SobreposicaoMinima = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);
    public const double DistanciaMaximaMetros = 50;
    public const int MinimoAmostras = 20;
    public const double FracaoMinima = 0.6;

    /// <summary>
    /// Compara as duas posições a cada 30 segundos no tempo em comum. Amostras em que algum dos dois estava numa
    /// lacuna não contam. Juntos: 20 amostras válidas ou mais, e 60% delas a até 50 metros.
    /// </summary>
    public static bool TreinaramJuntos(IReadOnlyList<SegmentoPercurso> meus, IReadOnlyList<SegmentoPercurso> deles)
    {
        if (meus.Count == 0 || deles.Count == 0)
        {
            return false;
        }

        var inicio = Max(meus[0].Pontos[0].Em, deles[0].Pontos[0].Em);
        var fim = Min(meus[^1].Pontos[^1].Em, deles[^1].Pontos[^1].Em);
        if (fim - inicio < SobreposicaoMinima)
        {
            return false;
        }

        var validas = 0;
        var perto = 0;
        for (var t = inicio; t <= fim; t += Intervalo)
        {
            if (PosicaoEm(meus, t) is not { } a || PosicaoEm(deles, t) is not { } b)
            {
                continue;
            }

            validas++;
            if (PercursoAtividade.Haversine(a.Lat, a.Lon, b.Lat, b.Lon) <= DistanciaMaximaMetros)
            {
                perto++;
            }
        }

        return validas >= MinimoAmostras && perto >= validas * FracaoMinima;
    }

    /// <summary>A posição no instante <paramref name="t"/>, interpolada entre os dois pontos vizinhos do mesmo trecho.</summary>
    public static (double Lat, double Lon)? PosicaoEm(IReadOnlyList<SegmentoPercurso> segmentos, DateTimeOffset t)
    {
        foreach (var segmento in segmentos)
        {
            var pontos = segmento.Pontos;
            if (t < pontos[0].Em || t > pontos[^1].Em)
            {
                continue;
            }

            for (var i = 1; i < pontos.Count; i++)
            {
                if (t > pontos[i].Em)
                {
                    continue;
                }

                var a = pontos[i - 1];
                var b = pontos[i];
                var total = (b.Em - a.Em).TotalSeconds;
                var f = total <= 0 ? 0 : (t - a.Em).TotalSeconds / total;
                return (a.Latitude + (b.Latitude - a.Latitude) * f, a.Longitude + (b.Longitude - a.Longitude) * f);
            }

            return (pontos[^1].Latitude, pontos[^1].Longitude);
        }

        return null;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
