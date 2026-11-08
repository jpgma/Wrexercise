using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// O Rival da semana de quem consulta, neste grupo: o que escolheu, quem o escolheu e os resultados que
/// ainda não viu. Só as duas pessoas de cada par veem o par.
/// </summary>
internal static class ListarRivais
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await GrupoAcesso.GetPapelAsync(connection, null, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        var semana = await Rivais.LoadSemanaAsync(connection, null, grupoId, cancellationToken);
        var rivais = await Rivais.LoadAsync(connection, null, usuarioId, grupoId, cancellationToken);

        var semanaInicio = semana?.Semana.Inicio;
        var visiveis = rivais
            .Where(r => r.SemanaInicio == semanaInicio || IsNova(r, usuarioId))
            .Select(r => ToResponse(r, usuarioId))
            .ToList();

        var jaEscolheu = rivais.Any(r => r.EscolhedorId == usuarioId && r.SemanaInicio == semanaInicio);
        var candidatos = semana is null || jaEscolheu ? [] : semana.GetCandidatos(usuarioId);

        return Results.Json(
            new RivaisResponse(
                semanaInicio,
                candidatos.Count > 0,
                candidatos,
                rivais.Count(r => r.EscolhedorId == usuarioId && r.Situacao == ResultadoRival.Venceu),
                visiveis),
            AppJsonContext.Default.RivaisResponse);
    }

    /// <summary>
    /// O que quem consulta ainda não viu: a provocação, para o Rival; o resultado final, para quem escolheu.
    /// </summary>
    public static bool IsNova(RivalCarregado rival, Guid usuarioId) =>
        rival.EscolhedorId == usuarioId
            ? ResultadoRival.IsFinal(rival.Situacao) && !rival.ResultadoVisto
            : !rival.Vista;

    private static RivalResponse ToResponse(RivalCarregado r, Guid usuarioId) => new(
        r.Id,
        r.EscolhedorId,
        r.EscolhedorNome,
        r.RivalId,
        r.RivalNome,
        r.SemanaInicio,
        r.Situacao,
        r.EscolhedorId == usuarioId,
        IsNova(r, usuarioId));
}
