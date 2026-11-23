using Npgsql;
using Wrexercise.Features.Notificacoes.Contracts;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// Avisa quem enviou que a carta falhou ou expirou e que metade do preço voltou (decisão 0087). A situação é
/// calculada na leitura, sem evento no instante em que a carta vence, por isso um serviço periódico procura as
/// Jogadas recentes que acabaram assim. A recusa é avisada na própria resposta (<see cref="ResponderJogada"/>).
/// </summary>
internal static class CartasNaoCumpridas
{
    /// <summary>
    /// Só os resultados a partir daqui viram aviso: a devolução vale para as cartas antigas também, mas elas não
    /// enchem a lista de ninguém.
    /// </summary>
    public static readonly DateTimeOffset InicioAviso = new(2026, 11, 22, 0, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>Por quanto tempo depois do prazo uma Jogada ainda é procurada.</summary>
    private static readonly TimeSpan Janela = TimeSpan.FromDays(7);

    /// <returns>Quantos avisos foram gravados.</returns>
    public static async Task<int> ProcessarAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // As candidatas: aceitas (podem ter falhado) ou sem resposta e vencidas, ainda sem aviso.
        var candidatas = new HashSet<Guid>();
        var remetentes = new HashSet<Guid>();
        await using (var command = new NpgsqlCommand("""
            SELECT j.id, j.remetente_id
            FROM jogadas j
            WHERE j.expira_em >= $1
              AND j.expira_em > now() - $2
              AND (j.resposta = 'Aceita' OR (j.resposta IS NULL AND j.expira_em <= now()))
              AND NOT EXISTS (
                  SELECT 1 FROM notificacoes n WHERE n.jogada_id = j.id AND n.tipo = 'CartaNaoCumprida')
            """, connection))
        {
            // O Npgsql só grava timestamptz em UTC.
            command.Parameters.AddWithValue(InicioAviso.ToUniversalTime());
            command.Parameters.AddWithValue(Janela);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidatas.Add(reader.GetGuid(0));
                remetentes.Add(reader.GetGuid(1));
            }
        }

        var total = 0;
        foreach (var remetenteId in remetentes)
        {
            var jogadas = await Jogadas.LoadAsync(connection, null, remetenteId, remetenteId, cancellationToken);
            foreach (var jogada in jogadas.Where(j => j.RemetenteId == remetenteId && candidatas.Contains(j.Id)))
            {
                if (GetResultado(jogada) is { } situacao
                    && await NotificacoesJogada.NotificarNaoCumpridaAsync(
                        connection,
                        jogada.Id,
                        jogada.RemetenteId,
                        jogada.DestinatarioId,
                        situacao,
                        jogada.Devolucao,
                        cancellationToken))
                {
                    total++;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// <c>Falhou</c> ou <c>Expirada</c>, se a carta acabou assim depois de <see cref="InicioAviso"/> e devolveu
    /// Energia; senão, <c>null</c>.
    /// </summary>
    public static string? GetResultado(JogadaCarregada jogada)
    {
        if (jogada.Devolucao <= 0)
        {
            return null;
        }

        return jogada.Situacao switch
        {
            SituacaoJogada.Falhou when jogada.FalhouEm >= InicioAviso => SituacaoJogada.Falhou,
            SituacaoJogada.Expirada when jogada.Estado.ExpiraEm >= InicioAviso => SituacaoJogada.Expirada,
            _ => null,
        };
    }
}

internal sealed class CartasNaoCumpridasBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<CartasNaoCumpridasBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var avisos = await CartasNaoCumpridas.ProcessarAsync(
                    scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>(),
                    stoppingToken);
                if (avisos > 0)
                {
                    logger.LogInformation("Cartas não cumpridas: {Avisos} avisos.", avisos);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao avisar as cartas não cumpridas.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
