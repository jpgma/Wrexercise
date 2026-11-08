using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ObterEnergia
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Só lê os dados de quem consulta, em todos os grupos: não depende de um grupo da rota.
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var estado = await EstadoJogo.LoadAsync(connection, null, usuarioId, cancellationToken);
        var ganha = estado.Ganha;

        return Results.Json(
            new EnergiaResponse(
                estado.Saldo,
                new EnergiaDetalhamentoResponse(
                    ganha.Atividades, ganha.Bonus, ganha.Metas, ganha.Sequencias, ganha.Desafios, ganha.Jogadas, ganha.Rivais)),
            AppJsonContext.Default.EnergiaResponse);
    }
}
