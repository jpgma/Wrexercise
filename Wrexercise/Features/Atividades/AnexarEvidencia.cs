using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

internal static class AnexarEvidencia
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return OperacaoAtividade.InvalidData();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var arquivo = form.Files.GetFile("arquivo");
        if (arquivo is null || arquivo.Length == 0)
        {
            return OperacaoAtividade.ToResult(AtividadeOperacaoStatus.EvidenciaInvalida);
        }

        var miniatura = form.Files.GetFile("miniatura");
        var sinais = LerSinais(form);
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var caminhosSalvos = new List<string>();

        void ExcluirSalvos()
        {
            foreach (var caminho in caminhosSalvos)
            {
                armazenamento.ExcluirSeExistir(caminho);
            }
        }

        try
        {
            await using var stream = arquivo.OpenReadStream();
            var (caminhoRelativo, tipoMime, tamanho, sha256) =
                await armazenamento.SalvarAsync(stream, ValidacaoImagemEvidencia.MaxBytes, cancellationToken);
            caminhosSalvos.Add(caminhoRelativo);

            string? caminhoMiniatura = null;
            if (miniatura is not null)
            {
                await using var streamMiniatura = miniatura.OpenReadStream();
                var salva = await armazenamento.SalvarAsync(
                    streamMiniatura,
                    ValidacaoImagemEvidencia.MaxBytesMiniatura,
                    cancellationToken);
                caminhosSalvos.Add(salva.CaminhoRelativo);
                caminhoMiniatura = salva.CaminhoRelativo;

                // Sempre JPEG: é o que o navegador gera, e a leitura não precisa guardar outro tipo.
                if (salva.TipoMime != ValidacaoImagemEvidencia.TipoMimeMiniatura)
                {
                    throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
                }
            }

            var (status, evidencia) = await PersistirAsync(
                dataSource,
                id,
                usuarioId,
                caminhoRelativo,
                tipoMime,
                tamanho,
                sha256,
                caminhoMiniatura,
                sinais,
                cancellationToken);

            if (status != AtividadeOperacaoStatus.Concluida)
            {
                ExcluirSalvos();
                return OperacaoAtividade.ToResult(status);
            }

            return Results.Json(
                evidencia!,
                AppJsonContext.Default.EvidenciaCriadaResponse,
                statusCode: StatusCodes.Status201Created);
        }
        catch (EvidenciaInvalidaException)
        {
            ExcluirSalvos();
            return OperacaoAtividade.ToResult(AtividadeOperacaoStatus.EvidenciaInvalida);
        }
    }

    /// <summary>
    /// Sinais lidos pelo navegador antes de a compressão apagar o EXIF (ADR 0052). Todos opcionais:
    /// um valor malformado é descartado, e a foto segue como se não o tivesse. A localização é guardada
    /// sem ser devolvida pela API nem registrada em log.
    /// </summary>
    private sealed record SinaisEvidencia(
        DateTimeOffset? CapturadaEm,
        string? Origem,
        string? Sha256Original,
        long? Dhash,
        double? Latitude,
        double? Longitude);

    private static SinaisEvidencia LerSinais(IFormCollection form)
    {
        DateTimeOffset? capturadaEm = null;
        string? origem = null;
        if (AvisosEvidencia.TryParseOrigem(form["origemDataCaptura"], out var origemLida)
            && DateTimeOffset.TryParse(
                form["capturadaEm"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var instante))
        {
            capturadaEm = instante;
            origem = origemLida;
        }

        var temCoordenadas = AvisosEvidencia.TryParseCoordenadas(
            form["latitude"],
            form["longitude"],
            out var latitude,
            out var longitude);

        return new SinaisEvidencia(
            capturadaEm,
            origem,
            AvisosEvidencia.TryParseSha256(form["sha256Original"], out var sha256Original) ? sha256Original : null,
            AvisosEvidencia.TryParseDhash(form["dhash"], out var dhash) ? dhash : null,
            temCoordenadas ? latitude : null,
            temCoordenadas ? longitude : null);
    }

    private static async Task<(AtividadeOperacaoStatus Status, EvidenciaCriadaResponse? Evidencia)> PersistirAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        string caminhoRelativo,
        string tipoMime,
        int tamanho,
        string sha256,
        string? caminhoMiniatura,
        SinaisEvidencia sinais,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        string tipoComprovacao;
        string situacao;
        Guid autorId;
        DateTimeOffset startedAt;
        int duracaoSegundos;
        await using (var atividade = new NpgsqlCommand("""
            SELECT a.usuario_id, a.tipo_comprovacao, a.situacao_verificacao, a.started_at, a.duracao_segundos
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            """, connection, transaction))
        {
            atividade.Parameters.AddWithValue(atividadeId);
            atividade.Parameters.AddWithValue(usuarioId);
            await using var reader = await atividade.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (AtividadeOperacaoStatus.AtividadeNaoEncontrada, null);
            }

            autorId = reader.GetGuid(0);
            tipoComprovacao = reader.GetString(1);
            situacao = reader.GetString(2);
            startedAt = reader.GetFieldValue<DateTimeOffset>(3);
            duracaoSegundos = reader.GetInt32(4);
        }

        if (autorId != usuarioId)
        {
            return (AtividadeOperacaoStatus.AcessoNegado, null);
        }

        if (tipoComprovacao == TipoComprovacao.RegistroManual)
        {
            return (AtividadeOperacaoStatus.ComprovacaoNaoAplicavel, null);
        }

        // Sem revisores, a primeira evidência já verifica a atividade: as seguintes ainda precisam entrar.
        await using (var revisores = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM revisores_atividade WHERE atividade_id = $1)",
            connection,
            transaction))
        {
            revisores.Parameters.AddWithValue(atividadeId);
            var temRevisores = (bool)(await revisores.ExecuteScalarAsync(cancellationToken))!;
            if (situacao != SituacaoVerificacao.AguardandoFeedback
                && (temRevisores || situacao != SituacaoVerificacao.Verificada))
            {
                return (AtividadeOperacaoStatus.AtividadeEncerrada, null);
            }
        }

        int proximaOrdem;
        await using (var contagem = new NpgsqlCommand(
            "SELECT count(*)::int FROM evidencias_atividade WHERE atividade_id = $1",
            connection,
            transaction))
        {
            contagem.Parameters.AddWithValue(atividadeId);
            var total = (int)(await contagem.ExecuteScalarAsync(cancellationToken))!;
            if (total >= ValidacaoAtividade.MaxEvidencias)
            {
                return (AtividadeOperacaoStatus.LimiteEvidencias, null);
            }

            proximaOrdem = total + 1;
        }

        string[] hashes = sinais.Sha256Original is { } original ? [sha256, original] : [sha256];
        if (await ExisteFotoIgualAsync(connection, transaction, usuarioId, hashes, cancellationToken))
        {
            return (AtividadeOperacaoStatus.EvidenciaDuplicada, null);
        }

        var parecidaCom = sinais.Dhash is { } dhash
            ? await BuscarFotoParecidaAsync(connection, transaction, usuarioId, atividadeId, dhash, cancellationToken)
            : null;

        Guid evidenciaId;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO evidencias_atividade (
                atividade_id, caminho_relativo, tipo_mime, tamanho_bytes, sha256, ordem, caminho_miniatura,
                capturada_em, origem_data_captura, sha256_original, dhash, latitude, longitude)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)
            RETURNING id
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(atividadeId);
            insert.Parameters.AddWithValue(caminhoRelativo);
            insert.Parameters.AddWithValue(tipoMime);
            insert.Parameters.AddWithValue(tamanho);
            insert.Parameters.AddWithValue(sha256);
            insert.Parameters.AddWithValue((short)proximaOrdem);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)caminhoMiniatura ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, (object?)sinais.CapturadaEm?.ToUniversalTime() ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)sinais.Origem ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)sinais.Sha256Original ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Bigint, (object?)sinais.Dhash ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Double, (object?)sinais.Latitude ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Double, (object?)sinais.Longitude ?? DBNull.Value);
            evidenciaId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        var avisoData = AvisosEvidencia.CalcularAvisoData(
            startedAt,
            duracaoSegundos,
            sinais.CapturadaEm,
            sinais.Origem,
            DateTimeOffset.UtcNow);
        if (avisoData is not null)
        {
            await ConsultasAtividade.InserirAvisoAsync(
                connection, transaction, atividadeId, avisoData, evidenciaId, null, cancellationToken);
        }

        if (parecidaCom is { } relacionada)
        {
            await ConsultasAtividade.InserirAvisoAsync(
                connection, transaction, atividadeId, TipoAviso.FotoParecida, evidenciaId, relacionada, cancellationToken);
        }

        await using (var autoVerificar = new NpgsqlCommand("""
            UPDATE atividades a
            SET situacao_verificacao = 'Verificada', verified_at = coalesce(a.verified_at, now())
            WHERE a.id = $1
              AND NOT EXISTS (SELECT 1 FROM revisores_atividade r WHERE r.atividade_id = a.id)
            """, connection, transaction))
        {
            autoVerificar.Parameters.AddWithValue(atividadeId);
            var verificou = await autoVerificar.ExecuteNonQueryAsync(cancellationToken) > 0;

            // As evidências seguintes de uma atividade já verificada não a concluem de novo.
            if (verificou && situacao != SituacaoVerificacao.Verificada)
            {
                await JogadasConcluidas.NotificarAsync(connection, transaction, atividadeId, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return (
            AtividadeOperacaoStatus.Concluida,
            new EvidenciaCriadaResponse(
                evidenciaId,
                proximaOrdem,
                ConsultasAtividade.BuildEvidenciaUrl(atividadeId, evidenciaId)));
    }

    /// <summary>
    /// Fotos já usadas em qualquer grupo de quem envia, inclusive na mesma atividade (ADR 0052).
    /// Compara o arquivo comprimido e o original, que o navegador informa.
    /// </summary>
    private static async Task<bool> ExisteFotoIgualAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        string[] hashes,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1
                FROM evidencias_atividade e
                JOIN atividades a ON a.id = e.atividade_id
                JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $1 AND m.removed_at IS NULL
                WHERE e.sha256 = ANY($2) OR e.sha256_original = ANY($2))
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Text, hashes);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>A atividade com a foto mais parecida, nos grupos de quem envia, fora a própria atividade.</summary>
    private static async Task<Guid?> BuscarFotoParecidaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        Guid atividadeId,
        long dhash,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT e.atividade_id
            FROM evidencias_atividade e
            JOIN atividades a ON a.id = e.atividade_id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $1 AND m.removed_at IS NULL
            WHERE e.dhash IS NOT NULL
              AND e.atividade_id <> $2
              AND bit_count(int8send(e.dhash # $3)) <= $4
            ORDER BY bit_count(int8send(e.dhash # $3)), e.created_at DESC
            LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(dhash);
        command.Parameters.AddWithValue(AvisosEvidencia.DistanciaMaximaFotoParecida);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid id ? id : null;
    }
}
