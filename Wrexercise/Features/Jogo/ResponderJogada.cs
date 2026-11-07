using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Jogo;

internal static class ResponderJogada
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        ResponderJogadaRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Aceitar com uma Resposta da Mão entra na G4.
        if (request.Resposta is not (SituacaoJogada.RespostaAceita or SituacaoJogada.RespostaRecusada))
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid grupoId;
        Guid remetenteId;
        bool aberta;
        await using (var load = new NpgsqlCommand("""
            SELECT grupo_id, remetente_id, resposta IS NULL AND expira_em > now()
            FROM jogadas
            WHERE id = $1 AND destinatario_id = $2
            FOR UPDATE
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(id);
            load.Parameters.AddWithValue(usuarioId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ErrosJogo.JogadaNaoEncontrada();
            }

            grupoId = reader.GetGuid(0);
            remetenteId = reader.GetGuid(1);
            aberta = reader.GetBoolean(2);
        }

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return ErrosJogo.JogadaNaoEncontrada();
        }

        if (!aberta)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "JOGADA_ENCERRADA",
                "Esta carta já foi respondida ou expirou.");
        }

        await using (var update = new NpgsqlCommand(
            "UPDATE jogadas SET resposta = $2, respondida_em = now() WHERE id = $1",
            connection,
            transaction))
        {
            update.Parameters.AddWithValue(id);
            update.Parameters.AddWithValue(request.Resposta);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await NotificacoesJogada.NotificarAsync(
            connection,
            transaction,
            id,
            remetenteId,
            usuarioId,
            request.Resposta == SituacaoJogada.RespostaAceita
                ? TipoNotificacaoJogada.CartaAceita
                : TipoNotificacaoJogada.CartaRecusada,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }
}
