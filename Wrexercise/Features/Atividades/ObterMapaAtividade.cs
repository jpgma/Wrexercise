using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>Um tile do mini-mapa de onde o treino ao vivo começou (ADR 0080), só quando a pessoa escolheu mostrar.</summary>
internal static class ObterMapaAtividade
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        int dx,
        int dy,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CacheMapas cache,
        CancellationToken cancellationToken)
    {
        if (!MapaLocal.IsDeslocamentoValido(dx, dy))
        {
            return OperacaoAtividade.AtividadeNaoEncontrada();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT a.latitude, a.longitude
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1 AND a.latitude IS NOT NULL
            """, connection);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(usuarioId);

        double latitude;
        double longitude;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return OperacaoAtividade.AtividadeNaoEncontrada();
            }

            latitude = reader.GetDouble(0);
            longitude = reader.GetDouble(1);
        }

        return await MapaLocal.ResponderAsync(
            latitude, longitude, dx, dy, cache, context, OperacaoAtividade.AtividadeNaoEncontrada, cancellationToken);
    }
}
