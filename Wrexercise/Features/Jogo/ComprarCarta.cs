using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ComprarCarta
{
    public static async Task<IResult> HandleAsync(
        ComprarCartaRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var carta = CatalogoCartas.Find(request.Codigo);
        if (carta is null)
        {
            return ErrosJogo.CartaInvalida();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Duas compras ao mesmo tempo esperam uma pela outra: o saldo lido abaixo já inclui a anterior.
        await EstadoJogo.LockUsuariosAsync(connection, transaction, [usuarioId], cancellationToken);
        var estado = await EstadoJogo.LoadAsync(connection, transaction, usuarioId, cancellationToken);

        if (estado.Mao.Count >= LimitesJogada.CartasNaMao)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "MAO_CHEIA",
                $"A sua Mão já tem {LimitesJogada.CartasNaMao} cartas.");
        }

        if (estado.Saldo < carta.Preco)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "SALDO_INSUFICIENTE",
                "Você não tem Energia suficiente para esta carta.");
        }

        CartaComprada comprada;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO cartas (usuario_id, codigo, preco)
            VALUES ($1, $2, $3)
            RETURNING id, created_at
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(usuarioId);
            insert.Parameters.AddWithValue(carta.Codigo);
            insert.Parameters.AddWithValue(carta.Preco);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            comprada = new CartaComprada(reader.GetGuid(0), carta.Codigo, carta.Preco, TimestampLeitura.FromReader(reader, 1));
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            ObterLoja.ToMaoResponse(comprada, emJogo: false),
            AppJsonContext.Default.CartaMaoResponse,
            statusCode: StatusCodes.Status201Created);
    }
}
