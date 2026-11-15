using System.Globalization;
using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O app cria ou troca o rascunho de um treino (<c>PUT</c> pelo identificador do treino no app). Multipart com
/// <c>tipo</c>, <c>duracaoSegundos</c>, <c>startedAt</c>, <c>endedAt</c>, a imagem do resumo (<c>arquivo</c>,
/// só foto) e a <c>miniatura</c> JPEG. Repetir com os mesmos dados não cria outro rascunho; depois de
/// postado, o rascunho não muda mais.
/// </summary>
internal static class SalvarRascunho
{
    private sealed record Campos(string Tipo, int DuracaoSegundos, DateTimeOffset StartedAt, DateTimeOffset EndedAt);

    public static async Task<IResult> HandleAsync(
        string externalId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        if (!AutorizacaoIntegracao.IsValidExternalId(externalId) || !context.Request.HasFormContentType)
        {
            return ErrosIntegracao.InvalidData();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var agora = DateTimeOffset.UtcNow;
        if (LerCampos(form, agora) is not { } campos)
        {
            return ErrosIntegracao.InvalidData();
        }

        if (campos.StartedAt + AutorizacaoIntegracao.ValidadeRascunho <= agora)
        {
            return ErrosIntegracao.AtividadeMuitoAntiga();
        }

        var arquivo = form.Files.GetFile("arquivo");
        var miniatura = form.Files.GetFile("miniatura");
        if (arquivo is null || arquivo.Length == 0 || miniatura is null || miniatura.Length == 0)
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        var salvos = new List<string>();
        try
        {
            EvidenciaSalva imagem;
            await using (var stream = arquivo.OpenReadStream())
            {
                imagem = await armazenamento.SalvarAsync(
                    stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytes, cancellationToken);
            }

            salvos.Add(imagem.CaminhoRelativo);

            EvidenciaSalva salvaMiniatura;
            await using (var stream = miniatura.OpenReadStream())
            {
                salvaMiniatura = await armazenamento.SalvarAsync(
                    stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytesMiniatura, cancellationToken);
            }

            salvos.Add(salvaMiniatura.CaminhoRelativo);
            if (salvaMiniatura.TipoMime != ValidacaoMidiaEvidencia.TipoMimeMiniatura)
            {
                throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
            }

            var (resposta, antigos) = await PersistirAsync(
                dataSource, app, externalId, campos, imagem, salvaMiniatura.CaminhoRelativo, cancellationToken);

            if (resposta is null)
            {
                ExcluirTodos(armazenamento, salvos);
                return ErrosIntegracao.RascunhoJaPostado();
            }

            // Os arquivos substituídos e os de rascunhos vencidos só saem depois do commit.
            ExcluirTodos(armazenamento, antigos);
            return Results.Json(resposta, AppJsonContext.Default.RascunhoSituacaoResponse);
        }
        catch (EvidenciaInvalidaException)
        {
            ExcluirTodos(armazenamento, salvos);
            return ApiProblemEvidencia();
        }
    }

    private static IResult ApiProblemEvidencia() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "EVIDENCIA_INVALIDA",
        "Verifique o arquivo enviado.");

    private static void ExcluirTodos(ArmazenamentoEvidencias armazenamento, IEnumerable<string> caminhos)
    {
        foreach (var caminho in caminhos)
        {
            armazenamento.ExcluirSeExistir(caminho);
        }
    }

    private static Campos? LerCampos(IFormCollection form, DateTimeOffset agora)
    {
        string? tipo = form["tipo"];
        if (!TipoAtividade.IsValid(tipo)
            || !int.TryParse(form["duracaoSegundos"], NumberStyles.None, CultureInfo.InvariantCulture, out var duracao)
            || duracao is < 60 or > 24 * 60 * 60
            || !TryParseInstante(form["startedAt"], out var startedAt)
            || !TryParseInstante(form["endedAt"], out var endedAt)
            || endedAt < startedAt
            || endedAt > agora + TimeSpan.FromHours(1))
        {
            return null;
        }

        return new Campos(tipo!, duracao, startedAt, endedAt);
    }

    private static bool TryParseInstante(string? valor, out DateTimeOffset instante) =>
        DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out instante);

    /// <returns>A situação, ou <c>null</c> quando o rascunho já foi postado; e os arquivos que podem sair.</returns>
    private static async Task<(RascunhoSituacaoResponse? Resposta, List<string> Antigos)> PersistirAsync(
        NpgsqlDataSource dataSource,
        AppAtual app,
        string externalId,
        Campos campos,
        EvidenciaSalva imagem,
        string caminhoMiniatura,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var antigos = new List<string>();

        // Faxina sem serviço periódico: cada envio leva os rascunhos vencidos e não postados de quem envia.
        await using (var vencidos = new NpgsqlCommand("""
            DELETE FROM rascunhos_atividade
            WHERE usuario_id = $1 AND atividade_id IS NULL AND expires_at <= now() AND NOT (app = $2 AND external_id = $3)
            RETURNING caminho_relativo, caminho_miniatura
            """, connection, transaction))
        {
            vencidos.Parameters.AddWithValue(app.UsuarioId);
            vencidos.Parameters.AddWithValue(app.App);
            vencidos.Parameters.AddWithValue(externalId);
            await using var reader = await vencidos.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                antigos.Add(reader.GetString(0));
                if (!reader.IsDBNull(1))
                {
                    antigos.Add(reader.GetString(1));
                }
            }
        }

        await using (var existente = new NpgsqlCommand("""
            SELECT atividade_id, caminho_relativo, caminho_miniatura
            FROM rascunhos_atividade
            WHERE usuario_id = $1 AND app = $2 AND external_id = $3
            FOR UPDATE
            """, connection, transaction))
        {
            existente.Parameters.AddWithValue(app.UsuarioId);
            existente.Parameters.AddWithValue(app.App);
            existente.Parameters.AddWithValue(externalId);
            await using var reader = await existente.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    await reader.DisposeAsync();
                    await transaction.CommitAsync(cancellationToken);
                    return (null, antigos);
                }

                antigos.Add(reader.GetString(1));
                if (!reader.IsDBNull(2))
                {
                    antigos.Add(reader.GetString(2));
                }
            }
        }

        var expiresAt = campos.StartedAt.ToUniversalTime() + AutorizacaoIntegracao.ValidadeRascunho;
        Guid id;
        await using (var upsert = new NpgsqlCommand("""
            INSERT INTO rascunhos_atividade (
                usuario_id, app, external_id, tipo, duracao_segundos, started_at, ended_at,
                caminho_relativo, tipo_mime, tamanho_bytes, sha256, caminho_miniatura, expires_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)
            ON CONFLICT (usuario_id, app, external_id) DO UPDATE SET
                tipo = excluded.tipo,
                duracao_segundos = excluded.duracao_segundos,
                started_at = excluded.started_at,
                ended_at = excluded.ended_at,
                caminho_relativo = excluded.caminho_relativo,
                tipo_mime = excluded.tipo_mime,
                tamanho_bytes = excluded.tamanho_bytes,
                sha256 = excluded.sha256,
                caminho_miniatura = excluded.caminho_miniatura,
                expires_at = excluded.expires_at,
                updated_at = now()
            RETURNING id
            """, connection, transaction))
        {
            upsert.Parameters.AddWithValue(app.UsuarioId);
            upsert.Parameters.AddWithValue(app.App);
            upsert.Parameters.AddWithValue(externalId);
            upsert.Parameters.AddWithValue(campos.Tipo);
            upsert.Parameters.AddWithValue(campos.DuracaoSegundos);
            upsert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, campos.StartedAt.ToUniversalTime());
            upsert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, campos.EndedAt.ToUniversalTime());
            upsert.Parameters.AddWithValue(imagem.CaminhoRelativo);
            upsert.Parameters.AddWithValue(imagem.TipoMime);
            upsert.Parameters.AddWithValue(imagem.TamanhoBytes);
            upsert.Parameters.AddWithValue(imagem.Sha256);
            upsert.Parameters.AddWithValue(caminhoMiniatura);
            upsert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, expiresAt);
            id = (Guid)(await upsert.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);
        return (RespostasIntegracao.BuildSituacao(id, null, expiresAt, DateTimeOffset.UtcNow), antigos);
    }
}
