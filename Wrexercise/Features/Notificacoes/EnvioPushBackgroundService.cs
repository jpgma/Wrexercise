using Wrexercise.Infrastructure.Push;

namespace Wrexercise.Features.Notificacoes;

/// <summary>Varre as notificações a cada 30 segundos e envia as novas ao telefone (decisão 0049).</summary>
internal sealed class EnvioPushBackgroundService(
    IServiceScopeFactory scopeFactory,
    WebPushClient push,
    ILogger<EnvioPushBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (push.ChavePublica is null)
        {
            logger.LogInformation("Notificações push desligadas: defina Vapid:PublicKey e Vapid:PrivateKey.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dataSource = scope.ServiceProvider.GetRequiredService<Npgsql.NpgsqlDataSource>();
                await EnvioPush.EnviarPendentesAsync(dataSource, push, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao enviar as notificações push.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
