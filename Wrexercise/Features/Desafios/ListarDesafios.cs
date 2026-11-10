using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Desafios;

internal static class ListarDesafios
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        if (await GrupoAcesso.GetPapelAsync(dataSource, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        var desafios = await ListByGrupoAsync(dataSource, grupoId, usuarioId, cancellationToken);
        return Results.Ok(new DesafiosResponse(desafios));
    }

    /// <summary>Os que ainda valem vêm primeiro; dentro de cada faixa, os de início mais recente.</summary>
    private static async Task<IReadOnlyList<DesafioResponse>> ListByGrupoAsync(
        NpgsqlDataSource dataSource,
        Guid grupoId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ConsultasDesafio.SelectDesafio + """

            WHERE d.grupo_id = $2
            ORDER BY
                d.cancelled_at IS NOT NULL,
                d.data_fim < (now() AT TIME ZONE g.fuso_horario)::date,
                d.data_inicio DESC,
                d.id
            """);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(grupoId);

        var desafios = new List<DesafioResponse>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                desafios.Add(ConsultasDesafio.ReadDesafio(reader));
            }
        }

        var metas = await ConsultasDesafio.LoadMetasPorTipoAsync(
            dataSource,
            [.. desafios.Select(d => d.Id)],
            cancellationToken);
        return [.. desafios.Select(d => ConsultasDesafio.WithMetasPorTipo(d, metas))];
    }
}
