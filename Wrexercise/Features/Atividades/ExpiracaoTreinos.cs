using Npgsql;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// O treino ao vivo esquecido (ADR 0078): depois de 3 horas, um lembrete; depois de 24 horas, ele expira e
/// sai, com as evidências, como se a pessoa o tivesse cancelado.
/// </summary>
internal static class ExpiracaoTreinos
{
    /// <returns>Quantos lembretes foram gravados e quantos treinos expiraram.</returns>
    public static async Task<(int Lembretes, int Expirados)> ProcessarAsync(
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var lembretes = await NotificacoesTreino.NotificarTreinosLongosAsync(
            connection, null, TreinoAoVivo.Lembrete, cancellationToken);

        var expirados = new List<Guid>();
        await using (var command = new NpgsqlCommand("""
            SELECT id FROM atividades
            WHERE situacao_verificacao = 'EmAndamento' AND started_at <= now() - $1
            """, connection))
        {
            command.Parameters.AddWithValue(TreinoAoVivo.Expiracao);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                expirados.Add(reader.GetGuid(0));
            }
        }

        var total = 0;
        foreach (var id in expirados)
        {
            if (await ExpirarAsync(connection, armazenamento, id, cancellationToken))
            {
                total++;
            }
        }

        return (lembretes, total);
    }

    private static async Task<bool> ExpirarAsync(
        NpgsqlConnection connection,
        ArmazenamentoEvidencias armazenamento,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        List<string> arquivos;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            // A pessoa pode ter terminado o treino entre a busca e aqui: só expira o que continua aberto.
            await using (var travar = new NpgsqlCommand("""
                SELECT 1 FROM atividades WHERE id = $1 AND situacao_verificacao = 'EmAndamento' FOR UPDATE
                """, connection, transaction))
            {
                travar.Parameters.AddWithValue(atividadeId);
                if (await travar.ExecuteScalarAsync(cancellationToken) is null)
                {
                    return false;
                }
            }

            arquivos = await ExcluirAtividade.ExcluirLinhasAsync(connection, transaction, atividadeId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var arquivo in arquivos)
        {
            armazenamento.ExcluirSeExistir(arquivo);
        }

        return true;
    }
}

internal sealed class ExpiracaoTreinosBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiracaoTreinosBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var (lembretes, expirados) = await ExpiracaoTreinos.ProcessarAsync(
                    scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>(),
                    scope.ServiceProvider.GetRequiredService<ArmazenamentoEvidencias>(),
                    stoppingToken);
                if (lembretes + expirados > 0)
                {
                    logger.LogInformation(
                        "Treinos ao vivo: {Lembretes} lembretes e {Expirados} expirados.", lembretes, expirados);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na manutenção dos treinos ao vivo.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
