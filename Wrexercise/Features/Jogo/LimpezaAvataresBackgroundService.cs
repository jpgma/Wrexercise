using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Jogo;

/// <summary>Roda <see cref="LimpezaAvatares"/> ao iniciar e depois uma vez por dia.</summary>
internal sealed class LimpezaAvataresBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<LimpezaAvataresBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                // Resolvidos aqui, e não no construtor: o host sobe mesmo sem banco configurado.
                await using var scope = scopeFactory.CreateAsyncScope();
                var dataSource = scope.ServiceProvider.GetRequiredService<Npgsql.NpgsqlDataSource>();
                var armazenamento = scope.ServiceProvider.GetRequiredService<ArmazenamentoAvatares>();
                var apagados = await LimpezaAvatares.LimparAsync(dataSource, armazenamento, stoppingToken);
                if (apagados > 0)
                {
                    logger.LogInformation("Apagadas {Quantidade} folhas de avatar sem uso.", apagados);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na limpeza das folhas de avatar.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
