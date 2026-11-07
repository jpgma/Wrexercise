using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// Registra que a encenação já foi vista: o envio, por quem recebeu; a resposta, por quem enviou
/// (docs/gamificacao.md §3.3).
/// </summary>
internal static class MarcarJogadasVistas
{
    private const int MaxIds = 50;

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        MarcarJogadasVistasRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.Ids is not { Count: > 0 and <= MaxIds } ids)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await GrupoAcesso.GetPapelAsync(connection, null, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var id in ids.Distinct())
        {
            await using var update = new NpgsqlCommand("""
                UPDATE jogadas
                SET vista_em = CASE WHEN destinatario_id = $3 THEN coalesce(vista_em, now()) ELSE vista_em END,
                    resultado_visto_em = CASE
                        WHEN remetente_id = $3 AND resposta IS NOT NULL THEN coalesce(resultado_visto_em, now())
                        ELSE resultado_visto_em
                    END
                WHERE id = $1 AND grupo_id = $2 AND $3 IN (remetente_id, destinatario_id)
                """, connection, transaction);
            update.Parameters.AddWithValue(id);
            update.Parameters.AddWithValue(grupoId);
            update.Parameters.AddWithValue(usuarioId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }
}
