using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>Uma Convocação do grupo, com todos os convidados e o resultado de cada um (decisão 0091).</summary>
internal static class ObterConvocacao
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        Guid convocacaoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await GrupoAcesso.GetPapelAsync(connection, null, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        var convocacao = await Convocacoes.LoadAsync(connection, null, grupoId, convocacaoId, cancellationToken);
        if (convocacao is null)
        {
            return ErrosJogo.ConvocacaoNaoEncontrada();
        }

        return Results.Json(Convocacoes.ToResponse(convocacao, usuarioId), AppJsonContext.Default.ConvocacaoResponse);
    }
}
