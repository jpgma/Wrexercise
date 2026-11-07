using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Autenticacao;

internal sealed record EntrarRequest(string? Email, string? Senha);

internal sealed record UsuarioCredenciais(
    Guid Id,
    string NomeExibicao,
    string Email,
    string PasswordHash,
    string ModoInicio);

internal static class Entrar
{
    private const string LoggerCategory = "Wrexercise.Autenticacao";

    public static async Task<IResult> HandleAsync(
        EntrarRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Senha))
        {
            return InvalidData();
        }

        var logger = loggerFactory.CreateLogger(LoggerCategory);

        // Uma senha acima do limite nunca foi aceita no cadastro: não há por que gastar CPU com ela.
        if (request.Senha.Length > ValidacaoConta.MaxSenhaLength)
        {
            return InvalidCredentials(logger);
        }

        var credenciais = await GetCredenciaisByEmailAsync(
            dataSource,
            ValidacaoConta.NormalizeEmail(request.Email),
            cancellationToken);

        if (credenciais is null)
        {
            Senhas.VerifyDummy(request.Senha);
            return InvalidCredentials(logger);
        }

        if (!Senhas.Verify(request.Senha, credenciais.PasswordHash))
        {
            return InvalidCredentials(logger);
        }

        var token = await Sessoes.CreateAsync(dataSource, credenciais.Id, cancellationToken);
        SessaoCookie.Write(context, token);
        logger.LogInformation("Usuário {UsuarioId} entrou.", credenciais.Id);

        return Results.Ok(new SessaoResponse(
            new UsuarioResponse(
                credenciais.Id,
                credenciais.NomeExibicao,
                credenciais.Email,
                credenciais.ModoInicio)));
    }

    private static async Task<UsuarioCredenciais?> GetCredenciaisByEmailAsync(
        NpgsqlDataSource dataSource,
        string email,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, nome_exibicao, email, password_hash, modo_inicio
            FROM usuarios
            WHERE lower(email) = lower($1)
            """);
        command.Parameters.AddWithValue(email);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UsuarioCredenciais(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4))
            : null;
    }

    private static IResult InvalidData() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");

    private static IResult InvalidCredentials(ILogger logger)
    {
        logger.LogInformation("Tentativa de entrada recusada.");
        return ApiProblem.Create(
            StatusCodes.Status401Unauthorized,
            "CREDENCIAIS_INVALIDAS",
            "E-mail ou senha incorretos.");
    }
}
