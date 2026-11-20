using Npgsql;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Jogo.Contracts;

/// <summary>Uma carta recebida que espera resposta, numa ordem de chegada.</summary>
internal sealed record CartaSemResposta(Guid JogadaId, Guid GrupoId, DateTimeOffset EnviadaEm, DateTimeOffset ExpiraEm);

/// <summary>
/// As Provocações recebidas que ainda esperam resposta, em todos os grupos de que a pessoa participa. Enquanto
/// houver uma, a Chegada toma a tela e nenhum treino novo é registrado (decisão 0076).
/// </summary>
internal static class CartasSemResposta
{
    private const string FromSql = """
        FROM jogadas j
        JOIN membros_grupo m ON m.grupo_id = j.grupo_id AND m.usuario_id = j.destinatario_id AND m.removed_at IS NULL
        WHERE j.destinatario_id = $1 AND j.resposta IS NULL AND j.expira_em > now()
        """;

    /// <summary>As cartas sem resposta da pessoa, da mais antiga para a mais nova.</summary>
    public static async Task<List<CartaSemResposta>> ListAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT j.id, j.grupo_id, j.enviada_em, j.expira_em
            {FromSql}
            ORDER BY j.enviada_em, j.id
            """, connection);
        command.Parameters.AddWithValue(usuarioId);

        var cartas = new List<CartaSemResposta>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cartas.Add(new CartaSemResposta(
                reader.GetGuid(0),
                reader.GetGuid(1),
                TimestampLeitura.FromReader(reader, 2),
                TimestampLeitura.FromReader(reader, 3)));
        }

        return cartas;
    }

    /// <summary>Se a pessoa tem alguma carta sem resposta, em qualquer grupo.</summary>
    public static async Task<bool> ExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 {FromSql})", connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static IResult Pendente() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "CARTA_SEM_RESPOSTA",
        "Responda a carta que você recebeu antes de registrar um treino.");
}
