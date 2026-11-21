namespace Wrexercise.Features.Atividades;

internal static class TipoRecorde
{
    /// <summary>A maior distância de quem treina naquele tipo.</summary>
    public const string Distancia = "Distancia";

    /// <summary>O melhor ritmo (ou velocidade, no Ciclismo), em treinos de 1 km ou mais (5 km no Ciclismo).</summary>
    public const string Ritmo = "Ritmo";
}

/// <summary>
/// O melhor de antes, no mesmo tipo, dos treinos com percurso da própria pessoa que não foram rejeitados nem
/// anulados. <c>VelocidadeMaxima</c> conta só os treinos com a distância mínima do recorde.
/// </summary>
internal readonly record struct MelhoresAnteriores(int Total, int? DistanciaMaxima, double? VelocidadeMediaMaxima);

/// <summary>
/// Os recordes pessoais do Percurso (ADR 0084). A pessoa só compete com ela mesma, e o recorde não rende nada: é um
/// selo na atividade, no Feed e na imagem do percurso.
/// </summary>
internal static class RecordesPercurso
{
    /// <summary>O recorde que este treino bateu, ou <c>null</c>. A distância vem antes do ritmo.</summary>
    public static string? Calcular(
        string tipo,
        int distanciaMetros,
        int duracaoSegundos,
        MelhoresAnteriores anteriores)
    {
        // O primeiro treino com percurso seria sempre um recorde: sem comparação, sem selo.
        if (anteriores.Total == 0 || duracaoSegundos <= 0
            || distanciaMetros < PercursoAtividade.MetrosMinimosRecorde(tipo))
        {
            return null;
        }

        if (anteriores.DistanciaMaxima is { } distancia && distanciaMetros > distancia)
        {
            return TipoRecorde.Distancia;
        }

        var velocidade = (double)distanciaMetros / duracaoSegundos;
        return anteriores.VelocidadeMediaMaxima is { } melhor && velocidade > melhor + 1e-9
            ? TipoRecorde.Ritmo
            : null;
    }
}
