using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>Um tile do mini-mapa da evidência (ADR 0080), só quando o autor escolheu mostrar o local (ADR 0061).</summary>
internal static class ObterMapaEvidencia
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        Guid evidenciaId,
        int dx,
        int dy,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CacheMapas cache,
        CancellationToken cancellationToken)
    {
        if (!MapaLocal.IsDeslocamentoValido(dx, dy))
        {
            return OperacaoAtividade.EvidenciaNaoEncontrada();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT e.latitude, e.longitude
            FROM evidencias_atividade e
            JOIN atividades a ON a.id = e.atividade_id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $3 AND m.removed_at IS NULL
            WHERE a.id = $1 AND e.id = $2 AND e.latitude IS NOT NULL
            """, connection);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(evidenciaId);
        command.Parameters.AddWithValue(usuarioId);

        double latitude;
        double longitude;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return OperacaoAtividade.EvidenciaNaoEncontrada();
            }

            latitude = reader.GetDouble(0);
            longitude = reader.GetDouble(1);
        }

        return await MapaLocal.ResponderAsync(
            latitude, longitude, dx, dy, cache, context, OperacaoAtividade.EvidenciaNaoEncontrada, cancellationToken);
    }
}
