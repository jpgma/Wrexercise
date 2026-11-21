using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// A imagem do Percurso (ADR 0084): o navegador a desenha depois de "Terminar treino" e a manda por aqui. Ela vira
/// evidência com a origem <c>Percurso</c>, datada pelo fim do treino, com o selo "Percurso". Uma por atividade e
/// dentro do limite de 10. Ao contrário de uma foto, nunca verifica a atividade nem muda a comprovação: o GPS é um
/// sinal do aparelho, não uma prova.
/// </summary>
internal static class AnexarImagemPercurso
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
        var salvos = new List<string>();
        try
        {
            EvidenciaSalva salvo;
            await using (var stream = arquivo.OpenReadStream())
            {
                salvo = await armazenamento.SalvarAsync(
                    stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytes, cancellationToken);
            }

            salvos.Add(salvo.CaminhoRelativo);

            string? caminhoMiniatura = null;
            if (miniatura is not null)
            {
                await using var streamMiniatura = miniatura.OpenReadStream();
                var salva = await armazenamento.SalvarAsync(
                    streamMiniatura, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytesMiniatura, cancellationToken);
                salvos.Add(salva.CaminhoRelativo);
                if (salva.TipoMime != ValidacaoMidiaEvidencia.TipoMimeMiniatura)
                {
                    throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
                }

                caminhoMiniatura = salva.CaminhoRelativo;
            }

            var (status, evidencia) = await PersistirAsync(
                dataSource,
                id,
                usuarioId,
                new ArquivoIntegracao(salvo.CaminhoRelativo, salvo.TipoMime, salvo.TamanhoBytes, salvo.Sha256, caminhoMiniatura),
                cancellationToken);
            if (status != AtividadeOperacaoStatus.Concluida)
            {
                salvos.ForEach(armazenamento.ExcluirSeExistir);
                return OperacaoAtividade.ToResult(status);
            }

            return Results.Json(
                evidencia!,
                AppJsonContext.Default.EvidenciaCriadaResponse,
                statusCode: StatusCodes.Status201Created);
        }
        catch (EvidenciaInvalidaException ex)
        {
            salvos.ForEach(armazenamento.ExcluirSeExistir);
            return OperacaoAtividade.EvidenciaInvalida(ex.Codigo);
        }
    }

    private static async Task<(AtividadeOperacaoStatus Status, EvidenciaCriadaResponse? Evidencia)> PersistirAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        ArquivoIntegracao arquivo,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid autorId;
        string situacao;
        DateTimeOffset startedAt;
        int duracao;
        bool temPercurso;
        bool temImagem;
        int total;
        await using (var load = new NpgsqlCommand("""
            SELECT a.usuario_id, a.situacao_verificacao, a.started_at, a.duracao_segundos,
                EXISTS (SELECT 1 FROM percursos p WHERE p.atividade_id = a.id),
                EXISTS (SELECT 1 FROM evidencias_atividade e
                        WHERE e.atividade_id = a.id AND e.origem_data_captura = 'Percurso'),
                (SELECT count(*)::int FROM evidencias_atividade e WHERE e.atividade_id = a.id)
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            FOR UPDATE OF a
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(atividadeId);
            load.Parameters.AddWithValue(usuarioId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (AtividadeOperacaoStatus.AtividadeNaoEncontrada, null);
            }

            autorId = reader.GetGuid(0);
            situacao = reader.GetString(1);
            startedAt = reader.GetFieldValue<DateTimeOffset>(2);
            duracao = reader.GetInt32(3);
            temPercurso = reader.GetBoolean(4);
            temImagem = reader.GetBoolean(5);
            total = reader.GetInt32(6);
        }

        if (autorId != usuarioId)
        {
            return (AtividadeOperacaoStatus.AcessoNegado, null);
        }

        if (!temPercurso)
        {
            return (AtividadeOperacaoStatus.PercursoNaoEncontrado, null);
        }

        // Ao vivo, o percurso ainda muda; anulada ou rejeitada, não conta mais.
        if (situacao is SituacaoVerificacao.EmAndamento or SituacaoVerificacao.Anulada or SituacaoVerificacao.Rejeitada)
        {
            return (AtividadeOperacaoStatus.AtividadeEncerrada, null);
        }

        if (temImagem)
        {
            return (AtividadeOperacaoStatus.EvidenciaJaAnexada, null);
        }

        if (total >= ValidacaoAtividade.MaxEvidencias)
        {
            return (AtividadeOperacaoStatus.LimiteEvidencias, null);
        }

        var ordem = (short)(total + 1);
        var evidenciaId = await EvidenciaIntegracao.InserirAsync(
            connection,
            transaction,
            atividadeId,
            arquivo,
            ordem,
            startedAt.AddSeconds(duracao),
            startedAt,
            duracao,
            cancellationToken,
            origem: OrigemDataCaptura.Percurso);

        await transaction.CommitAsync(cancellationToken);
        return (
            AtividadeOperacaoStatus.Concluida,
            new EvidenciaCriadaResponse(evidenciaId, ordem, ConsultasAtividade.BuildEvidenciaUrl(atividadeId, evidenciaId)));
    }
}
