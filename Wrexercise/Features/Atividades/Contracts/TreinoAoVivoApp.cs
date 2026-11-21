using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Atividades;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades.Contracts;

/// <summary>Um arquivo já gravado que veio de um app conectado e vira evidência com a origem <c>Integracao</c>.</summary>
internal sealed record ArquivoIntegracao(
    string CaminhoRelativo,
    string TipoMime,
    int TamanhoBytes,
    string Sha256,
    string? CaminhoMiniatura);

/// <summary>O treino ao vivo começado por um app conectado, como o app o vê (ADR 0081).</summary>
internal sealed record TreinoAppSituacao(
    Guid AtividadeId,
    Guid GrupoId,
    string GrupoNome,
    string Situacao,
    DateTimeOffset StartedAt,
    bool FimInformado,
    bool ResumoAnexado);

internal enum TreinoAppStatus
{
    Concluido,
    NaoEncontrado,
    NaoEmAndamento,

    /// <summary>O treino ainda está aberto: o resumo vai pelo fim (ADR 0081).</summary>
    EmAndamento,

    /// <summary>A atividade foi anulada: nada mais entra nela (ADR 0083).</summary>
    Encerrado,

    /// <summary>A atividade já tem o máximo de evidências.</summary>
    LimiteEvidencias,
}

/// <summary>
/// O que a fatia Integracoes pode fazer com o treino ao vivo em nome do app (ADR 0081): começar, informar o
/// fim com a imagem do resumo, cancelar enquanto está aberto e consultar. Terminar continua com a pessoa.
/// </summary>
internal static class TreinoAoVivoApp
{
    /// <summary>Coordenadas malformadas não impedem o treino: ele só começa sem local.</summary>
    /// <returns>O erro do começo (os mesmos do navegador), ou o treino e se ele foi criado agora.</returns>
    public static async Task<(IResult? Erro, TreinoAppSituacao? Treino, bool Criado)> ComecarAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        Guid grupoId,
        string tipo,
        double? latitude,
        double? longitude,
        string app,
        string externalId,
        CancellationToken cancellationToken)
    {
        var local = ComecarTreino.IsLocalValido(latitude, longitude)
            ? (latitude!.Value, longitude!.Value)
            : ((double, double)?)null;
        var (status, _, repetida) = await ComecarTreino.ExecutarAsync(
            dataSource, grupoId, usuarioId, tipo, local, (app, externalId), cancellationToken);
        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return (OperacaoAtividade.ToResult(status), null, false);
        }

        var treino = await ObterAsync(dataSource, usuarioId, app, externalId, cancellationToken);
        return treino is null
            ? (OperacaoAtividade.AtividadeNaoEncontrada(), null, false)
            : (null, treino, !repetida);
    }

    /// <summary>
    /// Guarda o fim e a duração ativa como sugestão e anexa a imagem do resumo, uma vez só. Repetir o envio
    /// atualiza a sugestão e não duplica a imagem.
    /// </summary>
    /// <param name="resumo">A imagem já gravada; sem ela, só a sugestão.</param>
    /// <returns>O status, o treino e se a imagem enviada entrou (senão, quem chama apaga os arquivos).</returns>
    public static async Task<(TreinoAppStatus Status, TreinoAppSituacao? Treino, bool ResumoUsado)> RegistrarFimAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        string app,
        string externalId,
        int duracaoAtivaSegundos,
        DateTimeOffset endedAt,
        ArquivoIntegracao? resumo,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid atividadeId;
        string situacao;
        DateTimeOffset startedAt;
        int totalEvidencias;
        bool temResumo;
        await using (var load = new NpgsqlCommand("""
            SELECT a.id, a.situacao_verificacao, a.started_at,
                (SELECT count(*)::int FROM evidencias_atividade e WHERE e.atividade_id = a.id),
                EXISTS (SELECT 1 FROM evidencias_atividade e
                        WHERE e.atividade_id = a.id AND e.origem_data_captura = $4)
            FROM atividades a
            WHERE a.usuario_id = $1 AND a.app = $2 AND a.app_external_id = $3
            FOR UPDATE OF a
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(usuarioId);
            load.Parameters.AddWithValue(app);
            load.Parameters.AddWithValue(externalId);
            load.Parameters.AddWithValue(OrigemDataCaptura.Integracao);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (TreinoAppStatus.NaoEncontrado, null, false);
            }

            atividadeId = reader.GetGuid(0);
            situacao = reader.GetString(1);
            startedAt = reader.GetFieldValue<DateTimeOffset>(2);
            totalEvidencias = reader.GetInt32(3);
            temResumo = reader.GetBoolean(4);
        }

        if (situacao != SituacaoVerificacao.EmAndamento)
        {
            return (TreinoAppStatus.NaoEmAndamento, null, false);
        }

        var agora = DateTimeOffset.UtcNow;
        var fim = TreinoAoVivo.FimApp(endedAt, startedAt, agora);
        await using (var update = new NpgsqlCommand("""
            UPDATE atividades SET app_duracao_segundos = $2, app_ended_at = $3 WHERE id = $1
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(atividadeId);
            update.Parameters.AddWithValue(TreinoAoVivo.SugestaoApp(duracaoAtivaSegundos, startedAt, agora));
            update.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, fim.ToUniversalTime());
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        var usado = false;
        if (resumo is not null && !temResumo && totalEvidencias < ValidacaoAtividade.MaxEvidencias)
        {
            // Durante o treino ao vivo, o período vai até agora, como nas fotos enviadas pelo navegador.
            await EvidenciaIntegracao.InserirAsync(
                connection,
                transaction,
                atividadeId,
                resumo,
                (short)(totalEvidencias + 1),
                fim,
                startedAt,
                TreinoAoVivo.Decorridos(startedAt, agora),
                cancellationToken);
            usado = true;
        }

        await transaction.CommitAsync(cancellationToken);
        var treino = await ObterAsync(dataSource, usuarioId, app, externalId, cancellationToken);
        return (TreinoAppStatus.Concluido, treino, usado);
    }

    /// <summary>
    /// O resumo que não chegou no fim (ADR 0083): depois que a pessoa terminou o treino aqui, o app ainda manda a
    /// imagem, uma vez só. Entra como evidência <c>Integracao</c> com o selo "Adicionada depois": aparece na
    /// atividade, mas não verifica nem muda a comprovação. Repetir não duplica.
    /// </summary>
    /// <returns>O status, o treino e se a imagem enviada entrou (senão, quem chama apaga os arquivos).</returns>
    public static async Task<(TreinoAppStatus Status, TreinoAppSituacao? Treino, bool ResumoUsado)> AnexarResumoDepoisAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        string app,
        string externalId,
        ArquivoIntegracao resumo,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid atividadeId;
        string situacao;
        DateTimeOffset startedAt;
        int duracaoSegundos;
        DateTimeOffset? appEndedAt;
        int totalEvidencias;
        bool temResumo;
        await using (var load = new NpgsqlCommand("""
            SELECT a.id, a.situacao_verificacao, a.started_at, a.duracao_segundos, a.app_ended_at,
                (SELECT count(*)::int FROM evidencias_atividade e WHERE e.atividade_id = a.id),
                EXISTS (SELECT 1 FROM evidencias_atividade e
                        WHERE e.atividade_id = a.id AND e.origem_data_captura = $4)
            FROM atividades a
            WHERE a.usuario_id = $1 AND a.app = $2 AND a.app_external_id = $3
            FOR UPDATE OF a
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(usuarioId);
            load.Parameters.AddWithValue(app);
            load.Parameters.AddWithValue(externalId);
            load.Parameters.AddWithValue(OrigemDataCaptura.Integracao);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (TreinoAppStatus.NaoEncontrado, null, false);
            }

            atividadeId = reader.GetGuid(0);
            situacao = reader.GetString(1);
            startedAt = reader.GetFieldValue<DateTimeOffset>(2);
            duracaoSegundos = reader.GetInt32(3);
            appEndedAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4);
            totalEvidencias = reader.GetInt32(5);
            temResumo = reader.GetBoolean(6);
        }

        if (situacao == SituacaoVerificacao.EmAndamento)
        {
            return (TreinoAppStatus.EmAndamento, null, false);
        }

        if (situacao == SituacaoVerificacao.Anulada)
        {
            return (TreinoAppStatus.Encerrado, null, false);
        }

        var usado = false;
        if (!temResumo)
        {
            if (totalEvidencias >= ValidacaoAtividade.MaxEvidencias)
            {
                return (TreinoAppStatus.LimiteEvidencias, null, false);
            }

            await EvidenciaIntegracao.InserirAsync(
                connection,
                transaction,
                atividadeId,
                resumo,
                (short)(totalEvidencias + 1),
                appEndedAt ?? startedAt.AddSeconds(duracaoSegundos),
                startedAt,
                duracaoSegundos,
                cancellationToken,
                adicionadaDepois: true);
            usado = true;
        }

        await transaction.CommitAsync(cancellationToken);
        var treino = await ObterAsync(dataSource, usuarioId, app, externalId, cancellationToken);
        return (TreinoAppStatus.Concluido, treino, usado);
    }

    /// <summary>Cancela o treino do app enquanto ele está aberto. Os arquivos saem depois do commit.</summary>
    public static async Task<(TreinoAppStatus Status, List<string> Arquivos)> CancelarAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        string app,
        string externalId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid atividadeId;
        string situacao;
        await using (var load = new NpgsqlCommand("""
            SELECT id, situacao_verificacao FROM atividades
            WHERE usuario_id = $1 AND app = $2 AND app_external_id = $3
            FOR UPDATE
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(usuarioId);
            load.Parameters.AddWithValue(app);
            load.Parameters.AddWithValue(externalId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (TreinoAppStatus.NaoEncontrado, []);
            }

            atividadeId = reader.GetGuid(0);
            situacao = reader.GetString(1);
        }

        // Depois de terminado, o treino é da pessoa: o app não apaga uma atividade registrada.
        if (situacao != SituacaoVerificacao.EmAndamento)
        {
            return (TreinoAppStatus.NaoEmAndamento, []);
        }

        var arquivos = await ExcluirAtividade.ExcluirLinhasAsync(connection, transaction, atividadeId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (TreinoAppStatus.Concluido, arquivos);
    }

    public static async Task<TreinoAppSituacao?> ObterAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        string app,
        string externalId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT a.id, a.grupo_id, g.nome, a.situacao_verificacao, a.started_at, a.app_ended_at IS NOT NULL,
                EXISTS (SELECT 1 FROM evidencias_atividade e
                        WHERE e.atividade_id = a.id AND e.origem_data_captura = $4)
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.usuario_id = $1 AND a.app = $2 AND a.app_external_id = $3
            """);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(app);
        command.Parameters.AddWithValue(externalId);
        command.Parameters.AddWithValue(OrigemDataCaptura.Integracao);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new TreinoAppSituacao(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6))
            : null;
    }

    /// <summary>Apaga arquivos que não chegaram a virar evidência, ou os de um treino cancelado.</summary>
    public static void ExcluirArquivos(ArmazenamentoEvidencias armazenamento, IEnumerable<string> caminhos)
    {
        foreach (var caminho in caminhos)
        {
            armazenamento.ExcluirSeExistir(caminho);
        }
    }
}
