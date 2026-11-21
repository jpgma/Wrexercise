using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Atividades.Contracts;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// A imagem do resumo de um app conectado vira evidência com a origem <c>Integracao</c> e a data do fim do
/// treino no app: a do rascunho postado (ADR 0065), a do treino ao vivo começado pelo app (ADR 0081) e a do
/// resumo mandado depois que o treino terminou aqui (ADR 0083, com <c>adicionadaDepois</c>). A imagem do Percurso
/// (ADR 0084) entra do mesmo jeito, com a origem <c>Percurso</c>.
/// </summary>
internal static class EvidenciaIntegracao
{
    /// <param name="duracaoPeriodoSegundos">Até onde o período do treino vai, para o aviso de data.</param>
    public static async Task<Guid> InserirAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        ArquivoIntegracao arquivo,
        short ordem,
        DateTimeOffset endedAt,
        DateTimeOffset startedAt,
        int duracaoPeriodoSegundos,
        CancellationToken cancellationToken,
        bool adicionadaDepois = false,
        string origem = OrigemDataCaptura.Integracao)
    {
        Guid evidenciaId;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO evidencias_atividade (
                atividade_id, caminho_relativo, tipo_mime, tamanho_bytes, sha256, ordem, caminho_miniatura,
                capturada_em, origem_data_captura, print, adicionada_depois)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, false, $10)
            RETURNING id
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(atividadeId);
            insert.Parameters.AddWithValue(arquivo.CaminhoRelativo);
            insert.Parameters.AddWithValue(arquivo.TipoMime);
            insert.Parameters.AddWithValue(arquivo.TamanhoBytes);
            insert.Parameters.AddWithValue(arquivo.Sha256);
            insert.Parameters.AddWithValue(ordem);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)arquivo.CaminhoMiniatura ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, endedAt.ToUniversalTime());
            insert.Parameters.AddWithValue(origem);
            insert.Parameters.AddWithValue(adicionadaDepois);
            evidenciaId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        var avisoData = AvisosEvidencia.CalcularAvisoData(
            startedAt, duracaoPeriodoSegundos, endedAt, origem, DateTimeOffset.UtcNow);
        if (avisoData is not null)
        {
            await ConsultasAtividade.InserirAvisoAsync(
                connection, transaction, atividadeId, avisoData, evidenciaId, null, cancellationToken);
        }

        return evidenciaId;
    }
}
