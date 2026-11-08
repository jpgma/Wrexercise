using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Jogo;

/// <summary>Entrega a folha só para a própria pessoa e para quem divide um grupo com ela.</summary>
internal static class ObterFolhaAvatar
{
    public static async Task<IResult> HandleAsync(
        string arquivo,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoAvatares armazenamento,
        CancellationToken cancellationToken)
    {
        // A mesma resposta para um arquivo inexistente e para um que a pessoa não pode ver.
        if (!ArmazenamentoAvatares.IsNomeValido(arquivo))
        {
            return ErrosJogo.AvatarNaoEncontrado();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var command = dataSource.CreateCommand("""
            SELECT 1
            FROM avatares a
            WHERE a.arquivo = $1
              AND (a.usuario_id = $2 OR EXISTS (
                  SELECT 1
                  FROM membros_grupo eu
                  JOIN membros_grupo dono ON dono.grupo_id = eu.grupo_id
                      AND dono.usuario_id = a.usuario_id AND dono.removed_at IS NULL
                  WHERE eu.usuario_id = $2 AND eu.removed_at IS NULL))
            """);
        command.Parameters.AddWithValue(arquivo);
        command.Parameters.AddWithValue(usuarioId);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            return ErrosJogo.AvatarNaoEncontrado();
        }

        var caminho = armazenamento.ResolverCaminho(arquivo);
        if (caminho is null || !File.Exists(caminho))
        {
            return ErrosJogo.AvatarNaoEncontrado();
        }

        // O nome muda a cada envio, então o navegador pode guardar a folha para sempre.
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return Results.File(caminho, "image/png");
    }
}
