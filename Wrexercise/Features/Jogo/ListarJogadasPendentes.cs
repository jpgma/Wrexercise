using Npgsql;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// As cartas recebidas que esperam resposta, em todos os grupos: o aplicativo abre a Chegada de cada uma
/// (decisão 0076). Consulta leve, feita a cada abertura do aplicativo.
/// </summary>
internal static class ListarJogadasPendentes
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var cartas = await CartasSemResposta.ListAsync(connection, usuarioId, cancellationToken);

        return Results.Json(
            new JogadasPendentesResponse(cartas
                .Select(c => new JogadaPendenteResponse(c.JogadaId, c.GrupoId, c.EnviadaEm, c.ExpiraEm))
                .ToList()),
            AppJsonContext.Default.JogadasPendentesResponse);
    }
}
