using Microsoft.Net.Http.Headers;
using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

internal static class ObterMiniatura
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        Guid evidenciaId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT e.caminho_miniatura
            FROM evidencias_atividade e
            JOIN atividades a ON a.id = e.atividade_id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $3 AND m.removed_at IS NULL
            WHERE a.id = $1 AND e.id = $2 AND e.caminho_miniatura IS NOT NULL
            """, connection);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(evidenciaId);
        command.Parameters.AddWithValue(usuarioId);

        if (await command.ExecuteScalarAsync(cancellationToken) is not string caminho)
        {
            return OperacaoAtividade.EvidenciaNaoEncontrada();
        }

        string caminhoCompleto;
        try
        {
            caminhoCompleto = armazenamento.ResolverCaminhoCompleto(caminho);
        }
        catch (EvidenciaInvalidaException)
        {
            return OperacaoAtividade.EvidenciaNaoEncontrada();
        }

        if (!File.Exists(caminhoCompleto))
        {
            return OperacaoAtividade.EvidenciaNaoEncontrada();
        }

        // Uma evidência não pode ser trocada: a miniatura pode ficar no navegador, mas nunca em cache compartilhado.
        context.Response.Headers[HeaderNames.CacheControl] = "private, max-age=604800, immutable";
        return Results.File(caminhoCompleto, ValidacaoMidiaEvidencia.TipoMimeMiniatura);
    }
}
