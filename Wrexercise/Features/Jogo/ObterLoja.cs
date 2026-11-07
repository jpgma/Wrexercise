using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ObterLoja
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var estado = await EstadoJogo.LoadAsync(connection, null, usuarioId, cancellationToken);

        return Results.Json(
            new LojaResponse(
                estado.Saldo,
                LimitesJogada.CartasNaMao,
                CatalogoCartas.ParaVenda.Select(ErrosJogo.ToResponse).ToList(),
                estado.Mao.Select(c => ToMaoResponse(c, estado.IsEmJogo(c.Id))).ToList()),
            AppJsonContext.Default.LojaResponse);
    }

    public static CartaMaoResponse ToMaoResponse(CartaComprada comprada, bool emJogo)
    {
        var carta = CatalogoCartas.Find(comprada.Codigo)!;
        // O preço pago fica na carta: mudar o catálogo não altera o que já foi comprado.
        return new CartaMaoResponse(comprada.Id, carta.Codigo, carta.Nome, carta.Tipo, comprada.Preco, carta.Efeito, emJogo);
    }
}
