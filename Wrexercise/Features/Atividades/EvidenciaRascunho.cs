using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// ADR 0065: ao postar, a imagem do rascunho de um app conectado vira a primeira evidência. A atividade
/// recebe uma cópia dos arquivos: apagar a atividade (ADR 0054) não leva a imagem do rascunho, que volta
/// a ficar pendente. A data da captura é o fim do treino, com a origem <c>Integracao</c>.
/// </summary>
internal static class EvidenciaRascunho
{
    /// <returns><c>false</c> quando o rascunho não é de quem posta, já virou atividade ou venceu.</returns>
    public static async Task<bool> AnexarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ArmazenamentoEvidencias armazenamento,
        Guid atividadeId,
        Guid usuarioId,
        Guid rascunhoId,
        DateTimeOffset startedAt,
        int duracaoSegundos,
        List<string> copias,
        CancellationToken cancellationToken)
    {
        string caminho;
        string tipoMime;
        int tamanhoBytes;
        string sha256;
        string? caminhoMiniatura;
        DateTimeOffset endedAt;

        // SQL explícito sobre a tabela da fatia Integracoes: só a leitura e o vínculo com a atividade.
        await using (var rascunho = new NpgsqlCommand("""
            SELECT caminho_relativo, tipo_mime, tamanho_bytes, sha256, caminho_miniatura, ended_at
            FROM rascunhos_atividade
            WHERE id = $1 AND usuario_id = $2 AND atividade_id IS NULL AND expires_at > now()
            FOR UPDATE
            """, connection, transaction))
        {
            rascunho.Parameters.AddWithValue(rascunhoId);
            rascunho.Parameters.AddWithValue(usuarioId);
            await using var reader = await rascunho.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            caminho = reader.GetString(0);
            tipoMime = reader.GetString(1);
            tamanhoBytes = reader.GetInt32(2);
            sha256 = reader.GetString(3);
            caminhoMiniatura = reader.IsDBNull(4) ? null : reader.GetString(4);
            endedAt = reader.GetFieldValue<DateTimeOffset>(5);
        }

        var copia = armazenamento.Copiar(caminho);
        copias.Add(copia);
        string? copiaMiniatura = null;
        if (caminhoMiniatura is not null)
        {
            copiaMiniatura = armazenamento.Copiar(caminhoMiniatura);
            copias.Add(copiaMiniatura);
        }

        // O formulário deixa mudar o início e a duração: a data do fim do treino ainda pode cair fora.
        await EvidenciaIntegracao.InserirAsync(
            connection,
            transaction,
            atividadeId,
            new ArquivoIntegracao(copia, tipoMime, tamanhoBytes, sha256, copiaMiniatura),
            1,
            endedAt,
            startedAt,
            duracaoSegundos,
            cancellationToken);

        await using (var vincular = new NpgsqlCommand(
            "UPDATE rascunhos_atividade SET atividade_id = $1, updated_at = now() WHERE id = $2",
            connection,
            transaction))
        {
            vincular.Parameters.AddWithValue(atividadeId);
            vincular.Parameters.AddWithValue(rascunhoId);
            await vincular.ExecuteNonQueryAsync(cancellationToken);
        }

        return true;
    }

    public static void ExcluirCopias(ArmazenamentoEvidencias armazenamento, List<string> copias)
    {
        foreach (var copia in copias)
        {
            armazenamento.ExcluirSeExistir(copia);
        }

        copias.Clear();
    }
}
