using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// Registra que a encenação do Rival já foi vista: a provocação, pelo Rival; o resultado, por quem
/// escolheu, e só quando ele é final (docs/gamificacao.md §3.3).
/// </summary>
internal static class MarcarRivaisVistos
{
    private const int MaxIds = 50;

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        MarcarRivaisVistosRequest request,
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

        var pedidos = ids.ToHashSet();
        var rivais = (await Rivais.LoadAsync(connection, null, usuarioId, grupoId, cancellationToken))
            .Where(r => pedidos.Contains(r.Id))
            .ToList();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var rival in rivais)
        {
            var coluna = rival.EscolhedorId == usuarioId
                ? (ResultadoRival.IsFinal(rival.Situacao) ? "resultado_visto_em" : null)
                : "vista_em";
            if (coluna is null)
            {
                continue;
            }

            await using var update = new NpgsqlCommand(
                $"UPDATE rivais SET {coluna} = coalesce({coluna}, now()) WHERE id = $1",
                connection,
                transaction);
            update.Parameters.AddWithValue(rival.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }
}
