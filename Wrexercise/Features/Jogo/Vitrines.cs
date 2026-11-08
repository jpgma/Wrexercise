using Npgsql;
using Wrexercise.Features.Usuarios.Contracts;

namespace Wrexercise.Features.Jogo;

/// <summary>SQL da vitrine de Enfeites, usado por mais de uma operação (docs/gamificacao.md §9.2).</summary>
internal static class Vitrines
{
    public static async Task<(string[] Vitrine, string[] Vistos)> LoadGuardadaAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT vitrine, enfeites_vistos FROM avatares WHERE usuario_id = $1",
            connection);
        command.Parameters.AddWithValue(usuarioId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetFieldValue<string[]>(0), reader.GetFieldValue<string[]>(1))
            : ([], []);
    }

    public sealed record EstadoVitrine(
        IReadOnlyList<EnfeiteLiberado> Liberados,
        IReadOnlyList<string> Vitrine,
        IReadOnlyList<string> Novos);

    /// <summary>Os Enfeites liberados, a vitrine em vigor e o que a pessoa ainda não viu.</summary>
    public static async Task<EstadoVitrine> LoadEstadoAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var conquistas = await ConquistasDoUsuario.LoadCodigosAsync(connection, usuarioId, cancellationToken);
        var liberados = Enfeites.Liberados(conquistas);
        var (guardada, vistos) = await LoadGuardadaAsync(connection, usuarioId, cancellationToken);
        var (vitrine, novos) = Enfeites.Resolver(liberados, guardada, vistos);
        return new EstadoVitrine(liberados, vitrine, novos);
    }

    /// <summary>Guarda a vitrine e marca todos os Enfeites liberados como vistos.</summary>
    public static async Task SalvarAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        IReadOnlyList<string> vitrine,
        IReadOnlyList<EnfeiteLiberado> liberados,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO avatares (usuario_id, vitrine, enfeites_vistos) VALUES ($1, $2, $3)
            ON CONFLICT (usuario_id)
            DO UPDATE SET vitrine = EXCLUDED.vitrine, enfeites_vistos = EXCLUDED.enfeites_vistos, updated_at = now()
            """, connection);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, vitrine.ToArray());
        command.Parameters.AddWithValue(
            NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
            liberados.Select(e => e.Codigo).ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static EnfeiteResponse ToResponse(EnfeiteLiberado enfeite) =>
        new(enfeite.Codigo, enfeite.Conquista, enfeite.Nome);

    public static IReadOnlyList<EnfeiteResponse> ToResponses(
        IReadOnlyList<EnfeiteLiberado> liberados,
        IEnumerable<string> codigos) =>
        codigos
            .Select(codigo => liberados.FirstOrDefault(e => e.Codigo == codigo))
            .Where(e => e.Codigo is not null)
            .Select(ToResponse)
            .ToList();
}
