using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Autenticacao;

internal sealed record AlterarModoInicioRequest(string? ModoInicio);

internal static class AlterarModoInicio
{
    public static async Task<IResult> HandleAsync(
        AlterarModoInicioRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!ModoInicio.IsValid(request.ModoInicio))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "DADOS_INVALIDOS",
                "Verifique os dados informados.");
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using (var update = dataSource.CreateCommand("UPDATE usuarios SET modo_inicio = $2 WHERE id = $1"))
        {
            update.Parameters.AddWithValue(usuarioId);
            update.Parameters.AddWithValue(request.ModoInicio!);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        var usuario = await Sessoes.GetUsuarioAsync(dataSource, usuarioId, cancellationToken);
        return usuario is null
            ? SessaoFilter.NotAuthenticated()
            : Results.Json(new SessaoResponse(usuario), AppJsonContext.Default.SessaoResponse);
    }
}
