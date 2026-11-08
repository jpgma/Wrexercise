using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal sealed record EscolherRivalRequest(Guid? RivalId);

internal sealed record RivalEscolhidoResponse(Guid Id, DateOnly SemanaInicio);

/// <summary>
/// Escolhe o Rival da semana neste grupo (docs/gamificacao.md §8 e decisão 0038). Um por grupo e por
/// semana, sem troca, e só antes de alguém dos dois cumprir a meta.
/// </summary>
internal static class EscolherRival
{
    private const string UniqueViolation = "23505";

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        EscolherRivalRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.RivalId is not { } rivalId)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        var semana = await Rivais.LoadSemanaAsync(connection, transaction, grupoId, cancellationToken);
        if (semana is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "SEM_DESAFIO_ATIVO",
                "O Rival da semana precisa de um desafio ativo no grupo.");
        }

        if (semana.Membros.GetValueOrDefault(usuarioId) is not { ModoJogo: true })
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "MODO_JOGO_NECESSARIO",
                "Ative o modo Jogo para escolher um Rival.");
        }

        if (rivalId == usuarioId
            || semana.Membros.GetValueOrDefault(rivalId) is not { ModoJogo: true, Participa: true }
            || !semana.Membros[usuarioId].Participa)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "RIVAL_INDISPONIVEL",
                "Escolha um colega no modo Jogo que participe desta semana do desafio.");
        }

        await using (var existente = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM rivais WHERE grupo_id = $1 AND escolhedor_id = $2 AND semana_inicio = $3)
            """, connection, transaction))
        {
            existente.Parameters.AddWithValue(grupoId);
            existente.Parameters.AddWithValue(usuarioId);
            existente.Parameters.AddWithValue(semana.Semana.Inicio);
            if ((bool)(await existente.ExecuteScalarAsync(cancellationToken))!)
            {
                return RivalJaEscolhido();
            }
        }

        if (!semana.PodeEscolher(usuarioId, rivalId))
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "META_JA_CUMPRIDA",
                "O Rival só pode ser escolhido antes de um de vocês cumprir a meta da semana.");
        }

        Guid id;
        try
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO rivais (grupo_id, desafio_id, semana_inicio, escolhedor_id, rival_id)
                VALUES ($1, $2, $3, $4, $5)
                RETURNING id
                """, connection, transaction);
            insert.Parameters.AddWithValue(grupoId);
            insert.Parameters.AddWithValue(semana.DesafioId);
            insert.Parameters.AddWithValue(semana.Semana.Inicio);
            insert.Parameters.AddWithValue(usuarioId);
            insert.Parameters.AddWithValue(rivalId);
            id = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
        {
            // Dois pedidos ao mesmo tempo: o primeiro grava, o segundo cai aqui.
            return RivalJaEscolhido();
        }

        await NotificacoesRival.NotificarEscolhidoAsync(connection, transaction, id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            new RivalEscolhidoResponse(id, semana.Semana.Inicio),
            AppJsonContext.Default.RivalEscolhidoResponse,
            statusCode: StatusCodes.Status201Created);
    }

    private static IResult RivalJaEscolhido() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "RIVAL_JA_ESCOLHIDO",
        "Você já escolheu o seu Rival desta semana neste grupo.");
}
