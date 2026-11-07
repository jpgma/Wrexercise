using Npgsql;
using Wrexercise.Features.Convites.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Autenticacao;

// Os campos são anuláveis porque o JSON recebido pode omiti-los; ValidacaoConta valida antes de usar.
internal sealed record RegistrarRequest(
    string? NomeExibicao,
    string? Email,
    string? Senha,
    string? ConviteToken = null);

internal enum RegistroStatus
{
    Criado,
    EmailJaCadastrado,
    ConviteInvalido,
    GrupoCheio,
}

internal readonly record struct RegistroResult(
    RegistroStatus Status,
    UsuarioResponse? Usuario = null,
    string? Token = null);

internal static class Registrar
{
    private const string UniqueViolation = "23505";
    private const string EmailUniqueIndex = "usuarios_email_unique";
    private const string LoggerCategory = "Wrexercise.Autenticacao";

    public static async Task<IResult> HandleAsync(
        RegistrarRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Texto em branco conta como ausência de convite; o convite recusado é decidido na transação.
        var conviteToken = string.IsNullOrWhiteSpace(request.ConviteToken) ? null : request.ConviteToken.Trim();

        // O convite é o único caminho; a primeira conta nasce na configuração inicial.
        if (conviteToken is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "CADASTRO_FECHADO",
                "Para criar uma conta, você precisa de um convite.");
        }

        if (!ValidacaoConta.IsValidNomeExibicao(request.NomeExibicao)
            || !ValidacaoConta.IsValidEmail(request.Email)
            || !ValidacaoConta.IsValidSenha(request.Senha))
        {
            return InvalidData();
        }

        var result = await ExecutarAsync(
            dataSource,
            ValidacaoConta.NormalizeNomeExibicao(request.NomeExibicao!),
            ValidacaoConta.NormalizeEmail(request.Email!),
            Senhas.Hash(request.Senha!),
            conviteToken,
            cancellationToken);

        switch (result.Status)
        {
            case RegistroStatus.EmailJaCadastrado:
                return ApiProblem.Create(
                    StatusCodes.Status409Conflict,
                    "EMAIL_JA_CADASTRADO",
                    "Já existe uma conta com este e-mail.");
            case RegistroStatus.ConviteInvalido:
                return ConviteAceite.ConviteInvalido();
            case RegistroStatus.GrupoCheio:
                return GrupoAcesso.GrupoCheio();
        }

        SessaoCookie.Write(context, result.Token!);
        loggerFactory.CreateLogger(LoggerCategory).LogInformation(
            "Conta criada para o usuário {UsuarioId}.", result.Usuario!.Id);

        return Results.Json(
            new SessaoResponse(result.Usuario),
            AppJsonContext.Default.SessaoResponse,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Conta, sessão e entrada no grupo acontecem na mesma transação: um convite recusado
    /// não deixa conta para trás, e um convite nunca cria mais de uma conta.
    /// </summary>
    private static async Task<RegistroResult> ExecutarAsync(
        NpgsqlDataSource dataSource,
        string nomeExibicao,
        string email,
        string passwordHash,
        string? conviteToken,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid usuarioId;
        try
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO usuarios (nome_exibicao, email, password_hash)
                VALUES ($1, $2, $3)
                RETURNING id
                """, connection, transaction);
            insert.Parameters.AddWithValue(nomeExibicao);
            insert.Parameters.AddWithValue(email);
            insert.Parameters.AddWithValue(passwordHash);
            usuarioId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (PostgresException exception) when (
            exception.SqlState == UniqueViolation && exception.ConstraintName == EmailUniqueIndex)
        {
            return new RegistroResult(RegistroStatus.EmailJaCadastrado);
        }

        if (conviteToken is not null)
        {
            var convite = await ConviteAceite.AceitarAsync(
                connection,
                transaction,
                conviteToken,
                usuarioId,
                cancellationToken);

            // Uma conta recém-criada não pode já ser membro, então só restam estes dois motivos de recusa.
            if (convite.Status != ConviteAceiteStatus.Aceito)
            {
                return new RegistroResult(convite.Status == ConviteAceiteStatus.GrupoCheio
                    ? RegistroStatus.GrupoCheio
                    : RegistroStatus.ConviteInvalido);
            }
        }

        var token = await Sessoes.InsertAsync(connection, transaction, usuarioId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new RegistroResult(RegistroStatus.Criado, new UsuarioResponse(usuarioId, nomeExibicao, email, ModoInicio.App), token);
    }

    private static IResult InvalidData() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");
}
