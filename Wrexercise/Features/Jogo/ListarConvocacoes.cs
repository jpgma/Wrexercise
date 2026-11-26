using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// As Convocações do grupo (decisão 0091): a do painel da praça e as mais recentes, para o histórico.
/// </summary>
internal static class ListarConvocacoes
{
    private const int Limite = 10;

    /// <summary>Por quanto tempo o resultado de uma Convocação encerrada continua no painel da praça.</summary>
    public static readonly TimeSpan ResultadoNoPainel = TimeSpan.FromDays(3);

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
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

        var convocacoes = await Convocacoes.LoadDoGrupoAsync(connection, null, grupoId, Limite, cancellationToken);
        var limite = DateTimeOffset.UtcNow - ResultadoNoPainel;
        var destaque = convocacoes.FirstOrDefault(c =>
            c.Situacao != SituacaoConvocacao.Encerrada || c.Fatos.ExpiraEm > limite);

        return Results.Json(
            new ConvocacoesResponse(
                destaque is null ? null : Convocacoes.ToResponse(destaque, usuarioId),
                convocacoes.Select(c => Convocacoes.ToResponse(c, usuarioId)).ToList()),
            AppJsonContext.Default.ConvocacoesResponse);
    }
}
