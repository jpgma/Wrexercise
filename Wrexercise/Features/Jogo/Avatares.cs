using Npgsql;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Jogo;

/// <summary>SQL e regras do avatar importado usados por mais de uma operação (docs/gamificacao.md §4.3).</summary>
internal static class Avatares
{
    public static string GetUrl(string arquivo) => $"/api/avatares/{arquivo}";

    /// <summary>
    /// Troca a folha de quem enviou e devolve o nome da anterior, para o chamador apagar o arquivo depois
    /// que a transação terminar.
    /// </summary>
    public static async Task<string?> TrocarFolhaAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        string? novoArquivo,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var garantir = new NpgsqlCommand(
            "INSERT INTO avatares (usuario_id) VALUES ($1) ON CONFLICT (usuario_id) DO NOTHING",
            connection,
            transaction))
        {
            garantir.Parameters.AddWithValue(usuarioId);
            await garantir.ExecuteNonQueryAsync(cancellationToken);
        }

        string? anterior;
        await using (var ler = new NpgsqlCommand(
            "SELECT arquivo FROM avatares WHERE usuario_id = $1 FOR UPDATE",
            connection,
            transaction))
        {
            ler.Parameters.AddWithValue(usuarioId);
            anterior = await ler.ExecuteScalarAsync(cancellationToken) as string;
        }

        await using (var gravar = new NpgsqlCommand(
            "UPDATE avatares SET arquivo = $2, updated_at = now() WHERE usuario_id = $1",
            connection,
            transaction))
        {
            gravar.Parameters.AddWithValue(usuarioId);
            gravar.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)novoArquivo ?? DBNull.Value);
            await gravar.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return anterior;
    }

    /// <summary>Volta ao avatar padrão e apaga o arquivo importado.</summary>
    public static async Task RestaurarAsync(
        NpgsqlDataSource dataSource,
        ArmazenamentoAvatares armazenamento,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var anterior = await TrocarFolhaAsync(dataSource, usuarioId, null, cancellationToken);
        if (anterior is not null)
        {
            armazenamento.ExcluirSeExistir(anterior);
        }
    }
}
