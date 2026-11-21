using Microsoft.Net.Http.Headers;
using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Reacoes;

/// <summary>A foto ou o vídeo de um comentário e a miniatura, só para os membros do grupo (ADR 0083).</summary>
internal static class ObterMidiaComentario
{
    public static Task<IResult> ArquivoAsync(
        Guid id,
        Guid comentarioId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken) =>
        ServirAsync(id, comentarioId, miniatura: false, context, dataSource, armazenamento, cancellationToken);

    public static Task<IResult> MiniaturaAsync(
        Guid id,
        Guid comentarioId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken) =>
        ServirAsync(id, comentarioId, miniatura: true, context, dataSource, armazenamento, cancellationToken);

    private static async Task<IResult> ServirAsync(
        Guid atividadeId,
        Guid comentarioId,
        bool miniatura,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT CASE WHEN $4 THEN c.caminho_miniatura ELSE c.caminho_relativo END, c.tipo_mime
            FROM comentarios c
            JOIN atividades a ON a.id = c.atividade_id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $3 AND m.removed_at IS NULL
            WHERE a.id = $1 AND c.id = $2
            """, connection);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(comentarioId);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(miniatura);

        string caminho;
        string tipoMime;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            {
                return NaoEncontrada();
            }

            caminho = reader.GetString(0);
            tipoMime = miniatura ? ValidacaoMidiaEvidencia.TipoMimeMiniatura : reader.GetString(1);
        }

        string caminhoCompleto;
        try
        {
            caminhoCompleto = armazenamento.ResolverCaminhoCompleto(caminho);
        }
        catch (EvidenciaInvalidaException)
        {
            return NaoEncontrada();
        }

        if (!File.Exists(caminhoCompleto))
        {
            return NaoEncontrada();
        }

        // Comentários não mudam: a mídia pode ficar no navegador, mas nunca em cache compartilhado.
        context.Response.Headers[HeaderNames.CacheControl] = "private, max-age=604800, immutable";
        return Results.File(caminhoCompleto, tipoMime, enableRangeProcessing: !miniatura);
    }

    private static IResult NaoEncontrada() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "MIDIA_NAO_ENCONTRADA",
        "Foto ou vídeo não encontrado.");
}
