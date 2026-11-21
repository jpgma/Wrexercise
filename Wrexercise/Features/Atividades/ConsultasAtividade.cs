using Npgsql;

namespace Wrexercise.Features.Atividades;

internal static class ConsultasAtividade
{
    public const string SelectResumo = """
        SELECT a.id, a.grupo_id, g.nome, a.usuario_id, u.nome_exibicao, a.tipo,
            a.duracao_segundos, a.started_at, a.tipo_comprovacao, a.situacao_verificacao,
            (SELECT count(*)::int FROM evidencias_atividade e WHERE e.atividade_id = a.id),
            a.submitted_at, a.descricao,
            (SELECT count(*)::int FROM reacoes_atividade r WHERE r.atividade_id = a.id AND r.tipo = 'Kudos'),
            (SELECT count(*)::int FROM reacoes_atividade r WHERE r.atividade_id = a.id AND r.tipo = 'Suspeito'),
            (SELECT count(*)::int FROM reacoes_atividade r WHERE r.atividade_id = a.id AND r.tipo = 'Rejeitar'),
            (SELECT e.id FROM evidencias_atividade e
             WHERE e.atividade_id = a.id AND e.ordem = 1 AND e.caminho_miniatura IS NOT NULL),
            (SELECT count(*)::int FROM comentarios c WHERE c.atividade_id = a.id),
            ARRAY(SELECT DISTINCT av.tipo FROM avisos_atividade av WHERE av.atividade_id = a.id ORDER BY av.tipo),
            (SELECT e.duracao_ms FROM evidencias_atividade e WHERE e.atividade_id = a.id AND e.ordem = 1)
        FROM atividades a
        JOIN grupos g ON g.id = a.grupo_id
        JOIN usuarios u ON u.id = a.usuario_id
        JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $1 AND m.removed_at IS NULL
        """;

    /// <summary>Fila de avaliação de alguém no grupo: <c>$1</c> é o usuário e <c>$2</c>, o grupo.</summary>
    public const string FiltroParaAvaliar = """
        a.grupo_id = $2
          AND a.situacao_verificacao IN ('AguardandoFeedback', 'EmRevisao')
          AND a.submitted_at + interval '48 hours' > now()
          AND EXISTS (
              SELECT 1 FROM revisores_atividade v
              WHERE v.atividade_id = a.id AND v.usuario_id = $1)
          AND NOT EXISTS (
              SELECT 1 FROM reacoes_atividade r
              WHERE r.atividade_id = a.id AND r.usuario_id = $1)
        """;

    /// <summary>Quem tem a fila de avaliação vazia pode registrar uma atividade no grupo.</summary>
    public static async Task<bool> ExistemParaAvaliarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid grupoId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT EXISTS (SELECT 1 FROM atividades a WHERE {FiltroParaAvaliar})
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(grupoId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static AtividadeResumoResponse ReadResumo(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetInt32(10),
            reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.GetInt32(13),
            reader.GetInt32(14),
            reader.GetInt32(15),
            reader.IsDBNull(16) ? null : BuildMiniaturaUrl(reader.GetGuid(0), reader.GetGuid(16)),
            reader.GetInt32(17),
            reader.GetFieldValue<string[]>(18),
            reader.IsDBNull(19) ? null : reader.GetInt32(19));

    public static async Task InserirAvisoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        string tipo,
        Guid? evidenciaId,
        Guid? atividadeRelacionadaId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO avisos_atividade (atividade_id, tipo, evidencia_id, atividade_relacionada_id)
            VALUES ($1, $2, $3, $4)
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(tipo);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Uuid, (object?)evidenciaId ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Uuid, (object?)atividadeRelacionadaId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static string BuildEvidenciaUrl(Guid atividadeId, Guid evidenciaId) =>
        $"/api/atividades/{atividadeId:D}/evidencias/{evidenciaId:D}";

    public static string BuildMiniaturaUrl(Guid atividadeId, Guid evidenciaId) =>
        $"{BuildEvidenciaUrl(atividadeId, evidenciaId)}/miniatura";

    public static async Task<AtividadeResponse?> GetDetalheAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var atividade = new NpgsqlCommand("""
            SELECT a.id, a.grupo_id, g.nome, a.usuario_id, u.nome_exibicao, a.tipo,
                a.duracao_segundos, a.started_at, a.descricao, a.tipo_comprovacao,
                a.situacao_verificacao, a.submitted_at, m.papel, a.latitude, a.longitude
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            JOIN usuarios u ON u.id = a.usuario_id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            """, connection);
        atividade.Parameters.AddWithValue(atividadeId);
        atividade.Parameters.AddWithValue(usuarioId);

        Guid id;
        Guid grupoId;
        string grupoNome;
        Guid autorId;
        string nomeExibicao;
        string tipo;
        int duracao;
        DateTimeOffset startedAt;
        string? descricao;
        string tipoComprovacao;
        string situacao;
        DateTimeOffset submittedAt;
        string papel;
        double? latitude;
        double? longitude;

        await using (var reader = await atividade.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetGuid(0);
            grupoId = reader.GetGuid(1);
            grupoNome = reader.GetString(2);
            autorId = reader.GetGuid(3);
            nomeExibicao = reader.GetString(4);
            tipo = reader.GetString(5);
            duracao = reader.GetInt32(6);
            startedAt = reader.GetFieldValue<DateTimeOffset>(7);
            descricao = reader.IsDBNull(8) ? null : reader.GetString(8);
            tipoComprovacao = reader.GetString(9);
            situacao = reader.GetString(10);
            submittedAt = reader.GetFieldValue<DateTimeOffset>(11);
            papel = reader.GetString(12);
            latitude = reader.IsDBNull(13) ? null : reader.GetDouble(13);
            longitude = reader.IsDBNull(14) ? null : reader.GetDouble(14);
        }

        var evidencias = new List<EvidenciaResponse>();
        await using (var lista = new NpgsqlCommand(
            """
            SELECT id, ordem, origem_data_captura IS NOT DISTINCT FROM 'Camera', duracao_ms,
                print, latitude, longitude, capturada_em, origem_data_captura, adicionada_depois
            FROM evidencias_atividade WHERE atividade_id = $1 ORDER BY ordem
            """,
            connection))
        {
            lista.Parameters.AddWithValue(atividadeId);
            await using var reader = await lista.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var evidenciaId = reader.GetGuid(0);
                evidencias.Add(new EvidenciaResponse(
                    evidenciaId,
                    reader.GetInt16(1),
                    BuildEvidenciaUrl(id, evidenciaId),
                    reader.GetBoolean(2),
                    reader.IsDBNull(3) ? TipoEvidencia.Foto : TipoEvidencia.Video,
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.GetBoolean(4),
                    reader.IsDBNull(5) ? null : reader.GetDouble(5),
                    reader.IsDBNull(6) ? null : reader.GetDouble(6),
                    reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.GetBoolean(9)));
            }
        }

        // A atividade da foto parecida só aparece para quem também a pode ver.
        var avisos = new List<AvisoResponse>();
        await using (var lista = new NpgsqlCommand("""
            SELECT av.tipo, av.evidencia_id,
                CASE WHEN EXISTS (
                    SELECT 1 FROM atividades r
                    JOIN membros_grupo m ON m.grupo_id = r.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
                    WHERE r.id = av.atividade_relacionada_id)
                THEN av.atividade_relacionada_id END
            FROM avisos_atividade av
            WHERE av.atividade_id = $1
            ORDER BY av.created_at, av.id
            """, connection))
        {
            lista.Parameters.AddWithValue(atividadeId);
            lista.Parameters.AddWithValue(usuarioId);
            await using var reader = await lista.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                avisos.Add(new AvisoResponse(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetGuid(2)));
            }
        }

        var correcoes = new List<CorrecaoDuracaoResponse>();
        await using (var lista = new NpgsqlCommand("""
            SELECT u.nome_exibicao, c.duracao_anterior_segundos, c.duracao_nova_segundos, c.justificativa, c.created_at
            FROM correcoes_duracao c
            JOIN usuarios u ON u.id = c.responsavel_id
            WHERE c.atividade_id = $1
            ORDER BY c.created_at, c.id
            """, connection))
        {
            lista.Parameters.AddWithValue(atividadeId);
            await using var reader = await lista.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                correcoes.Add(new CorrecaoDuracaoResponse(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetFieldValue<DateTimeOffset>(4)));
            }
        }

        return new AtividadeResponse(
            id,
            grupoId,
            grupoNome,
            autorId,
            nomeExibicao,
            tipo,
            duracao,
            startedAt,
            descricao,
            tipoComprovacao,
            situacao,
            submittedAt,
            autorId == usuarioId,
            evidencias,
            avisos,
            ExclusaoAtividade.PermitidaAte(autorId == usuarioId, situacao, submittedAt, DateTimeOffset.UtcNow),
            CorrecaoDuracao.Permitida(autorId == usuarioId, papel, situacao),
            correcoes,
            latitude,
            longitude,
            PrazoEvidencia.AutorPodeAnexar(autorId == usuarioId, situacao));
    }
}
