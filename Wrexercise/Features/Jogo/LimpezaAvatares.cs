using Npgsql;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// Apaga do disco as folhas que nenhuma pessoa usa mais: as que sobraram de uma falha entre gravar o
/// arquivo e o banco, e as de lado que a migração 022 desligou (ADR 0039, Andamento).
/// </summary>
internal static class LimpezaAvatares
{
    /// <summary>Um envio em andamento grava o arquivo antes da linha; ele nunca é tão antigo.</summary>
    public static readonly TimeSpan IdadeMinima = TimeSpan.FromHours(1);

    /// <returns>Quantos arquivos foram apagados.</returns>
    public static async Task<int> LimparAsync(
        NpgsqlDataSource dataSource,
        ArmazenamentoAvatares armazenamento,
        CancellationToken cancellationToken)
    {
        var candidatos = armazenamento.ListarArquivosAntigos(IdadeMinima);
        if (candidatos.Count == 0)
        {
            return 0;
        }

        var usados = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = dataSource.CreateCommand(
            "SELECT arquivo FROM avatares WHERE arquivo = ANY($1)"))
        {
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, candidatos.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                usados.Add(reader.GetString(0));
            }
        }

        var apagados = 0;
        foreach (var nome in candidatos)
        {
            if (!usados.Contains(nome))
            {
                armazenamento.ExcluirSeExistir(nome);
                apagados++;
            }
        }

        return apagados;
    }
}
