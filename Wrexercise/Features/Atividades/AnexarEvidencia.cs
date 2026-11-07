using Npgsql;
using NpgsqlTypes;
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

    private static async Task<(AtividadeOperacaoStatus Status, EvidenciaCriadaResponse? Evidencia)> PersistirAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        string caminhoRelativo,
        string tipoMime,
        int tamanho,
        string sha256,
        string? caminhoMiniatura,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        string tipoComprovacao;
        string situacao;
        Guid autorId;
        await using (var atividade = new NpgsqlCommand("""
            SELECT a.usuario_id, a.tipo_comprovacao, a.situacao_verificacao
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

        Guid evidenciaId;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO evidencias_atividade (
                atividade_id, caminho_relativo, tipo_mime, tamanho_bytes, sha256, ordem, caminho_miniatura)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
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
            evidenciaId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        await using (var autoVerificar = new NpgsqlCommand("""
            UPDATE atividades a
            SET situacao_verificacao = 'Verificada'
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
}
