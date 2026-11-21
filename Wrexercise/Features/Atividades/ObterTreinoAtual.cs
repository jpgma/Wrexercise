using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

/// <summary>Uma Torcida recebida no treino ao vivo: quem torceu e quando.</summary>
internal sealed record TorcidaResponse(Guid UsuarioId, string NomeExibicao, DateTimeOffset CriadaEm);

/// <summary>
/// O Percurso que o aparelho está gravando (ADR 0084). <c>UltimoSeq</c>: o maior ponto já guardado, ou −1; depois de
/// recarregar, o aparelho continua do seguinte.
/// </summary>
internal sealed record PercursoAoVivoResponse(int UltimoSeq, int DistanciaMetros);

internal sealed record TreinoAtualResponse(
    Guid AtividadeId,
    Guid GrupoId,
    string GrupoNome,
    string Tipo,
    DateTimeOffset StartedAt,
    int TotalEvidencias,
    IReadOnlyList<TorcidaResponse> Torcidas,
    string? App,
    int? AppDuracaoSegundos,
    DateTimeOffset? AppEndedAt,
    PercursoAoVivoResponse? Percurso);

/// <summary>
/// O treino ao vivo de quem consulta, em qualquer grupo (ADR 0078). Sem treino aberto, 204.
/// O navegador usa a resposta para mostrar o cronômetro em qualquer tela e aparelho. Começado por um app
/// conectado, traz o fim e a duração ativa que o app informou, como sugestão (ADR 0081).
/// </summary>
internal static class ObterTreinoAtual
{
    private const int MaxTorcidas = 50;

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        TreinoAtualResponse treino;
        await using (var command = new NpgsqlCommand("""
            SELECT a.id, a.grupo_id, g.nome, a.tipo, a.started_at,
                (SELECT count(*)::int FROM evidencias_atividade e WHERE e.atividade_id = a.id),
                a.app, a.app_duracao_segundos, a.app_ended_at,
                p.distancia_metros,
                (SELECT coalesce(max(pp.seq), -1) FROM pontos_percurso pp WHERE pp.atividade_id = a.id)
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            LEFT JOIN percursos p ON p.atividade_id = a.id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = a.usuario_id AND m.removed_at IS NULL
            WHERE a.usuario_id = $1 AND a.situacao_verificacao = 'EmAndamento'
            """, connection))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Results.NoContent();
            }

            treino = new TreinoAtualResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetInt32(5),
                [],
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : new PercursoAoVivoResponse(reader.GetInt32(10), reader.GetInt32(9)));
        }

        var torcidas = new List<TorcidaResponse>();
        await using (var command = new NpgsqlCommand("""
            SELECT t.usuario_id, u.nome_exibicao, t.created_at
            FROM torcidas t
            JOIN usuarios u ON u.id = t.usuario_id
            WHERE t.atividade_id = $1
            ORDER BY t.created_at DESC, t.id
            LIMIT $2
            """, connection))
        {
            command.Parameters.AddWithValue(treino.AtividadeId);
            command.Parameters.AddWithValue(MaxTorcidas);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                torcidas.Add(new TorcidaResponse(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetFieldValue<DateTimeOffset>(2)));
            }
        }

        return Results.Json(treino with { Torcidas = torcidas }, AppJsonContext.Default.TreinoAtualResponse);
    }
}
