using Npgsql;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Reacoes;

/// <summary>SQL compartilhado entre operações de verificação comunitária.</summary>
internal static class VerificacaoAtividade
{
    public static async Task<(ContagensReacao Contagens, int TotalRevisores)> LoadContagensAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var revisores = new NpgsqlCommand(
            "SELECT count(*)::int FROM revisores_atividade WHERE atividade_id = $1",
            connection,
            transaction);
        revisores.Parameters.AddWithValue(atividadeId);
        var totalRevisores = (int)(await revisores.ExecuteScalarAsync(cancellationToken))!;

        await using var reacoes = new NpgsqlCommand("""
            SELECT
                count(*) FILTER (WHERE tipo = 'Kudos')::int,
                count(*) FILTER (WHERE tipo = 'Suspeito')::int,
                count(*) FILTER (WHERE tipo = 'Rejeitar')::int
            FROM reacoes_atividade
            WHERE atividade_id = $1
            """, connection, transaction);
        reacoes.Parameters.AddWithValue(atividadeId);

        await using var reader = await reacoes.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (
            new ContagensReacao(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)),
            totalRevisores);
    }

    public static async Task AplicarSituacaoCalculadaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        DateTimeOffset submittedAt,
        string situacaoAtual,
        CancellationToken cancellationToken)
    {
        var (contagens, totalRevisores) = await LoadContagensAsync(
            connection,
            transaction,
            atividadeId,
            cancellationToken);
        var janelaEncerrada = !ConsensoAtividade.JanelaAberta(submittedAt, DateTimeOffset.UtcNow);
        var nova = ConsensoAtividade.DeterminarSituacao(
            totalRevisores,
            contagens,
            janelaEncerrada,
            situacaoAtual);

        if (nova == situacaoAtual)
        {
            return;
        }

        await using var update = new NpgsqlCommand(
            """
            UPDATE atividades
            SET situacao_verificacao = $2,
                verified_at = CASE WHEN $2 = 'Verificada' THEN coalesce(verified_at, now()) ELSE verified_at END
            WHERE id = $1
            """,
            connection,
            transaction);
        update.Parameters.AddWithValue(atividadeId);
        update.Parameters.AddWithValue(nova);
        await update.ExecuteNonQueryAsync(cancellationToken);

        // Voltar a aguardar feedback e ser verificada são o caminho esperado: não avisam (decisão 0048).
        if (nova is SituacaoVerificacao.Rejeitada or SituacaoVerificacao.EmRevisao)
        {
            await NotificacoesAtividade.NotificarSituacaoAsync(connection, transaction, atividadeId, nova, cancellationToken);
        }

        if (nova == SituacaoVerificacao.EmRevisao)
        {
            await NotificacoesAtividade.NotificarDisputaAsync(connection, transaction, atividadeId, cancellationToken);
        }

        if (nova == SituacaoVerificacao.Verificada)
        {
            await JogadasConcluidas.NotificarAsync(connection, transaction, atividadeId, cancellationToken);
        }
    }

    public static async Task<int> EncerrarJanelasExpiradasAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var ids = new List<(Guid Id, DateTimeOffset SubmittedAt, string Situacao)>();

        await using (var select = new NpgsqlCommand("""
            SELECT id, submitted_at, situacao_verificacao
            FROM atividades
            WHERE situacao_verificacao = 'AguardandoFeedback'
              AND submitted_at + interval '48 hours' <= now()
            """, connection))
        {
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add((
                    reader.GetGuid(0),
                    TimestampLeitura.FromReader(reader, 1),
                    reader.GetString(2)));
            }
        }

        var alteradas = 0;
        foreach (var (id, submittedAt, situacao) in ids)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var antes = situacao;
            await AplicarSituacaoCalculadaAsync(connection, transaction, id, submittedAt, antes, cancellationToken);

            await using var check = new NpgsqlCommand(
                "SELECT situacao_verificacao FROM atividades WHERE id = $1",
                connection,
                transaction);
            check.Parameters.AddWithValue(id);
            var depois = (string)(await check.ExecuteScalarAsync(cancellationToken))!;
            if (depois != antes)
            {
                alteradas++;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return alteradas;
    }
}
