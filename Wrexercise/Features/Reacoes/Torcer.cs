using Npgsql;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Reacoes;

/// <summary>ADR 0078: cada membro torce por quem treina ao vivo no máximo uma vez a cada 10 minutos.</summary>
internal static class IntervaloTorcida
{
    public static readonly TimeSpan Minimo = TimeSpan.FromMinutes(10);

    public static bool Permitida(DateTimeOffset? ultima, DateTimeOffset agora) =>
        ultima is not { } instante || agora - instante >= Minimo;
}

/// <summary>
/// "Torcer" (ADR 0078): um colega torce por quem está treinando agora. Quem treina vê a Torcida no
/// cronômetro; não há push, para não virar ruído.
/// </summary>
internal static class Torcer
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid autorId;
        string situacao;
        DateTimeOffset? ultima;

        // FOR UPDATE: dois toques ao mesmo tempo não passam juntos pelo intervalo.
        await using (var load = new NpgsqlCommand("""
            SELECT a.usuario_id, a.situacao_verificacao,
                (SELECT max(t.created_at) FROM torcidas t WHERE t.atividade_id = a.id AND t.usuario_id = $2)
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            FOR UPDATE OF a
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(id);
            load.Parameters.AddWithValue(usuarioId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return AtividadeNaoEncontrada();
            }

            autorId = reader.GetGuid(0);
            situacao = reader.GetString(1);
            ultima = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
        }

        if (autorId == usuarioId)
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "TORCIDA_PROPRIA",
                "Você não pode torcer pelo seu próprio treino.");
        }

        if (situacao != SituacaoVerificacao.EmAndamento)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "TREINO_NAO_EM_ANDAMENTO",
                "Este treino já terminou.");
        }

        if (!IntervaloTorcida.Permitida(ultima, DateTimeOffset.UtcNow))
        {
            return ApiProblem.Create(
                StatusCodes.Status429TooManyRequests,
                "TORCIDA_MUITO_CEDO",
                "Você acabou de torcer. Daqui a pouco dá de novo.");
        }

        await using (var insert = new NpgsqlCommand(
            "INSERT INTO torcidas (atividade_id, usuario_id) VALUES ($1, $2)", connection, transaction))
        {
            insert.Parameters.AddWithValue(id);
            insert.Parameters.AddWithValue(usuarioId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await NotificacoesTreino.NotificarTorcidaAsync(connection, transaction, id, usuarioId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult AtividadeNaoEncontrada() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "ATIVIDADE_NAO_ENCONTRADA",
        "Atividade não encontrada.");
}
