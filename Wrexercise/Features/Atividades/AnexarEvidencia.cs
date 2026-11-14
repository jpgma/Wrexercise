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
            var salvo = await armazenamento.SalvarAsync(
                stream,
                aceitaVideo: true,
                ValidacaoMidiaEvidencia.MaxBytes,
                cancellationToken);
            caminhosSalvos.Add(salvo.CaminhoRelativo);

            string? caminhoMiniatura = null;
            if (miniatura is not null)
            {
                await using var streamMiniatura = miniatura.OpenReadStream();
                var salva = await armazenamento.SalvarAsync(
                    streamMiniatura,
                    aceitaVideo: false,
                    ValidacaoMidiaEvidencia.MaxBytesMiniatura,
                    cancellationToken);
                caminhosSalvos.Add(salva.CaminhoRelativo);
                caminhoMiniatura = salva.CaminhoRelativo;

                // Sempre JPEG: é o que o navegador gera, e a leitura não precisa guardar outro tipo.
                if (salva.TipoMime != ValidacaoMidiaEvidencia.TipoMimeMiniatura)
                {
                    throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
                }
            }

            var (status, evidencia) = await PersistirAsync(
                dataSource,
                id,
                usuarioId,
                salvo,
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
        catch (EvidenciaInvalidaException ex)
        {
            ExcluirSalvos();
            return OperacaoAtividade.EvidenciaInvalida(ex.Codigo);
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
        long? DhashInicio,
        long? DhashFim,
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
            AvisosEvidencia.TryParseDhash(form["dhashInicio"], out var dhashInicio) ? dhashInicio : null,
            AvisosEvidencia.TryParseDhash(form["dhashFim"], out var dhashFim) ? dhashFim : null,
            temCoordenadas ? latitude : null,
            temCoordenadas ? longitude : null);
    }

    private static async Task<(AtividadeOperacaoStatus Status, EvidenciaCriadaResponse? Evidencia)> PersistirAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        EvidenciaSalva salvo,
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

        // Antes do limite: ao repetir o registro (ADR 0053), o navegador reenvia as fotos que já chegaram.
        string[] hashes = sinais.Sha256Original is { } original ? [salvo.Sha256, original] : [salvo.Sha256];
        if (await BuscarFotoIgualAsync(connection, transaction, usuarioId, atividadeId, hashes, cancellationToken)
            is { } atividadeDaFoto)
        {
            return (
                atividadeDaFoto == atividadeId
                    ? AtividadeOperacaoStatus.EvidenciaJaAnexada
                    : AtividadeOperacaoStatus.EvidenciaDuplicada,
                null);
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

        var parecidaCom = sinais.Dhash is { } dhash
            ? await BuscarFotoParecidaAsync(connection, transaction, usuarioId, atividadeId, dhash, cancellationToken)
            : null;

        Guid evidenciaId;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO evidencias_atividade (
                atividade_id, caminho_relativo, tipo_mime, tamanho_bytes, sha256, ordem, caminho_miniatura,
                capturada_em, origem_data_captura, sha256_original, dhash, latitude, longitude, duracao_ms)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
            RETURNING id
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(atividadeId);
            insert.Parameters.AddWithValue(salvo.CaminhoRelativo);
            insert.Parameters.AddWithValue(salvo.TipoMime);
            insert.Parameters.AddWithValue(salvo.TamanhoBytes);
            insert.Parameters.AddWithValue(salvo.Sha256);
            insert.Parameters.AddWithValue((short)proximaOrdem);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)caminhoMiniatura ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, (object?)sinais.CapturadaEm?.ToUniversalTime() ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)sinais.Origem ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)sinais.Sha256Original ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Bigint, (object?)sinais.Dhash ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Double, (object?)sinais.Latitude ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Double, (object?)sinais.Longitude ?? DBNull.Value);
            insert.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)salvo.DuracaoMs ?? DBNull.Value);
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

        if (salvo.DuracaoMs is not null
            && AvisosEvidencia.IsVideoParado(sinais.DhashInicio, sinais.Dhash, sinais.DhashFim))
        {
            await ConsultasAtividade.InserirAvisoAsync(
                connection, transaction, atividadeId, TipoAviso.VideoParado, evidenciaId, null, cancellationToken);
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
    /// Compara o arquivo comprimido e o original, que o navegador informa. Devolve a atividade da foto,
    /// preferindo a própria: aí é uma repetição do envio, não um reaproveitamento (ADR 0053).
    /// </summary>
    private static async Task<Guid?> BuscarFotoIgualAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        Guid atividadeId,
        string[] hashes,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT e.atividade_id
            FROM evidencias_atividade e
            JOIN atividades a ON a.id = e.atividade_id
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $1 AND m.removed_at IS NULL
            WHERE e.sha256 = ANY($2) OR e.sha256_original = ANY($2)
            ORDER BY e.atividade_id = $3 DESC
            LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Text, hashes);
        command.Parameters.AddWithValue(atividadeId);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid id ? id : null;
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
