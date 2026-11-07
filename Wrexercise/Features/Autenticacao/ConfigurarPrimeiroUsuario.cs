using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Autenticacao;

// Os campos são anuláveis porque o JSON recebido pode omiti-los; ValidacaoConta valida antes de usar.
internal sealed record ConfiguracaoInicialRequest(
    string? NomeExibicao,
    string? Email,
    string? Senha);

internal static class ConfigurarPrimeiroUsuario
{
    // Chave do lock consultivo que serializa as tentativas de configuração inicial.
    private const long LockKey = 7_201_001;
    private const string LoggerCategory = "Wrexercise.Autenticacao";

    public static async Task<IResult> HandleAsync(
        ConfiguracaoInicialRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!ValidacaoConta.IsValidNomeExibicao(request.NomeExibicao)
            || !ValidacaoConta.IsValidEmail(request.Email)
            || !ValidacaoConta.IsValidSenha(request.Senha))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "DADOS_INVALIDOS",
                "Verifique os dados informados.");
        }

        var result = await ExecutarAsync(
            dataSource,
            ValidacaoConta.NormalizeNomeExibicao(request.NomeExibicao!),
            ValidacaoConta.NormalizeEmail(request.Email!),
            Senhas.Hash(request.Senha!),
            cancellationToken);

        if (result is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "CONFIGURACAO_JA_CONCLUIDA",
                "O app já foi configurado. Para criar uma conta, você precisa de um convite.");
        }

        SessaoCookie.Write(context, result.Value.Token);
        loggerFactory.CreateLogger(LoggerCategory).LogInformation(
            "Primeiro usuário criado pela configuração inicial: {UsuarioId}.", result.Value.Usuario.Id);

        return Results.Json(
            new SessaoResponse(result.Value.Usuario),
            AppJsonContext.Default.SessaoResponse,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// O lock consultivo faz duas requisições simultâneas esperarem uma pela outra; só a primeira
    /// encontra a tabela vazia. Devolve <c>null</c> quando já existe algum usuário.
    /// </summary>
    private static async Task<(UsuarioResponse Usuario, string Token)?> ExecutarAsync(
        NpgsqlDataSource dataSource,
        string nomeExibicao,
        string email,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var bloqueio = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            bloqueio.Parameters.AddWithValue(LockKey);
            await bloqueio.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var existe = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM usuarios)", connection, transaction))
        {
            if ((bool)(await existe.ExecuteScalarAsync(cancellationToken))!)
            {
                return null;
            }
        }

        Guid usuarioId;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO usuarios (nome_exibicao, email, password_hash)
            VALUES ($1, $2, $3)
            RETURNING id
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(nomeExibicao);
            insert.Parameters.AddWithValue(email);
            insert.Parameters.AddWithValue(passwordHash);
            usuarioId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        var token = await Sessoes.InsertAsync(connection, transaction, usuarioId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return (new UsuarioResponse(usuarioId, nomeExibicao, email, ModoInicio.App), token);
    }
}
