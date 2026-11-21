using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Reacoes;

/// <summary>A foto ou o vídeo já gravado de um comentário (ADR 0083).</summary>
internal sealed record MidiaComentarioSalva(EvidenciaSalva Arquivo, string? CaminhoMiniatura);

/// <summary>
/// Membros ativos do grupo comentam qualquer atividade, a qualquer momento (decisão 0042), com texto,
/// uma foto ou um vídeo, ou os dois (ADR 0083).
/// </summary>
internal static class Comentarios
{
    public static async Task<IResult> ListarAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (!await IsMembroAsync(connection, null, id, usuarioId, cancellationToken))
        {
            return OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada);
        }

        var comentarios = new List<ComentarioResponse>();
        await using var lista = new NpgsqlCommand("""
            SELECT c.id, c.usuario_id, u.nome_exibicao, c.texto, c.created_at,
                c.caminho_relativo IS NOT NULL, c.caminho_miniatura IS NOT NULL, c.duracao_ms
            FROM comentarios c
            JOIN usuarios u ON u.id = c.usuario_id
            WHERE c.atividade_id = $1
            ORDER BY c.created_at, c.id
            """, connection);
        lista.Parameters.AddWithValue(id);
        await using var reader = await lista.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var comentarioId = reader.GetGuid(0);
            comentarios.Add(new ComentarioResponse(
                comentarioId,
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                TimestampLeitura.FromReader(reader, 4),
                reader.GetBoolean(5)
                    ? BuildMidia(id, comentarioId, reader.GetBoolean(6), reader.IsDBNull(7) ? null : reader.GetInt32(7))
                    : null));
        }

        return Results.Json(new ComentariosResponse(comentarios), AppJsonContext.Default.ComentariosResponse);
    }

    public static async Task<IResult> CriarAsync(
        Guid id,
        ComentarioRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!ValidacaoRevisao.IsValidTexto(request.Texto))
        {
            return OperacaoReacao.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var criado = await InserirAsync(
            dataSource,
            id,
            usuarioId,
            ValidacaoRevisao.NormalizeJustificativa(request.Texto!),
            null,
            cancellationToken);
        return criado is null
            ? OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada)
            : Results.Json(criado, AppJsonContext.Default.ComentarioResponse, statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Grava o comentário e avisa o autor e quem já comentou. Nulo quando a atividade não existe ou quem
    /// comenta não é membro ativo do grupo; aí quem chama apaga os arquivos da mídia.
    /// </summary>
    public static async Task<ComentarioResponse?> InserirAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        string? texto,
        MidiaComentarioSalva? midia,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await IsMembroAsync(connection, transaction, atividadeId, usuarioId, cancellationToken))
        {
            return null;
        }

        Guid comentarioId;
        DateTimeOffset createdAt;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO comentarios (
                atividade_id, usuario_id, texto, caminho_relativo, tipo_mime, tamanho_bytes, caminho_miniatura, duracao_ms)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            RETURNING id, created_at
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(atividadeId);
            insert.Parameters.AddWithValue(usuarioId);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)texto ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)midia?.Arquivo.CaminhoRelativo ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)midia?.Arquivo.TipoMime ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)midia?.Arquivo.TamanhoBytes ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)midia?.CaminhoMiniatura ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)midia?.Arquivo.DuracaoMs ?? DBNull.Value);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            comentarioId = reader.GetGuid(0);
            createdAt = TimestampLeitura.FromReader(reader, 1);
        }

        await NotificacoesAtividade.NotificarComentarioAsync(connection, transaction, atividadeId, usuarioId, cancellationToken);

        string nome;
        await using (var autor = new NpgsqlCommand(
            "SELECT nome_exibicao FROM usuarios WHERE id = $1",
            connection,
            transaction))
        {
            autor.Parameters.AddWithValue(usuarioId);
            nome = (string)(await autor.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);
        return new ComentarioResponse(
            comentarioId,
            usuarioId,
            nome,
            texto,
            createdAt,
            midia is null
                ? null
                : BuildMidia(atividadeId, comentarioId, midia.CaminhoMiniatura is not null, midia.Arquivo.DuracaoMs));
    }

    /// <summary>A atividade existe e o usuário é membro ativo do grupo dela.</summary>
    public static async Task<bool> IsMembroAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid atividadeId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var acesso = new NpgsqlCommand("""
            SELECT 1
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            """, connection, transaction);
        acesso.Parameters.AddWithValue(atividadeId);
        acesso.Parameters.AddWithValue(usuarioId);
        return await acesso.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static ComentarioMidiaResponse BuildMidia(Guid atividadeId, Guid comentarioId, bool temMiniatura, int? duracaoMs)
    {
        var url = $"/api/atividades/{atividadeId}/comentarios/{comentarioId}/midia";
        return new ComentarioMidiaResponse(
            url,
            temMiniatura ? url + "/miniatura" : null,
            duracaoMs is null ? "Foto" : "Video",
            duracaoMs);
    }
}
