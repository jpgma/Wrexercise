namespace Wrexercise.Features.Atividades.Contracts;

/// <summary>
/// Um ponto do GPS. <c>Seq</c> e <c>Segmento</c> vêm do aparelho: o segmento muda a cada reinício da gravação (no
/// navegador) ou a cada pausa (no Movr).
/// </summary>
internal sealed record PontoPercursoRequest(
    int? Seq,
    DateTimeOffset? Em,
    double? Latitude,
    double? Longitude,
    double? Precisao,
    double? Altitude,
    int? Segmento);

/// <summary>Um lote de pontos do Percurso: o mesmo corpo para o navegador (ADR 0084) e para o app (ADR 0085).</summary>
internal sealed record EnviarPontosPercursoRequest(IReadOnlyList<PontoPercursoRequest>? Pontos);

/// <summary><c>UltimoSeq</c>: o maior <c>seq</c> guardado, para o aparelho continuar daí depois de recarregar.</summary>
internal sealed record PontosPercursoResponse(int UltimoSeq, int DistanciaMetros, int TotalPontos);

/// <summary>Os limites de um lote, os mesmos para o navegador e para o app.</summary>
internal static class LotePercurso
{
    public const int MaxPontos = PercursoAtividade.MaxPontosPorLote;
}
