using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Wrexercise.Features.Jogo;
using Wrexercise.Features.Notificacoes;
using Wrexercise.Features.Reacoes;

namespace Wrexercise.IntegrationTests;

internal static class TestHostDefaults
{
    /// <summary>
    /// Servidor de teste mais leve: só registra avisos e erros, e não roda a manutenção periódica.
    /// Os testes que precisam da manutenção chamam a regra diretamente.
    /// </summary>
    public static IWebHostBuilder UseTestDefaults(this IWebHostBuilder builder) =>
        builder
            .UseSetting("Logging:LogLevel:Default", "Warning")
            .ConfigureTestServices(services =>
            {
                var periodicos = services
                    .Where(service =>
                        service.ImplementationType == typeof(EncerramentoRevisoesBackgroundService)
                        || service.ImplementationType == typeof(LimpezaAvataresBackgroundService)
                        || service.ImplementationType == typeof(EnvioPushBackgroundService))
                    .ToList();
                foreach (var service in periodicos)
                {
                    services.Remove(service);
                }
            });
}
