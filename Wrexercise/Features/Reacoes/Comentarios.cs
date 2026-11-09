using Npgsql;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Reacoes;

/// <summary>Membros ativos do grupo comentam qualquer atividade, a qualquer momento (decisão 0042).</summary>
internal static class Comentarios
{
    public static async Task<IResult> ListarAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (!await IsMembroAsync(connection, null, id, usuarioId, cancellationToken))
        {
            return OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada);
        }

        var comentarios = new List<ComentarioResponse>();
        await using var lista = new NpgsqlCommand("""
            SELECT c.id, c.usuario_id, u.nome_exibicao, c.texto, c.created_at
            FROM comentarios c
            JOIN usuarios u ON u.id = c.usuario_id
            WHERE c.atividade_id = $1
            ORDER BY c.created_at, c.id
            """, connection);
        lista.Parameters.AddWithValue(id);
        await using var reader = await lista.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            comentarios.Add(new ComentarioResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                TimestampLeitura.FromReader(reader, 4)));
        }

        return Results.Json(new ComentariosResponse(comentarios), AppJsonContext.Default.ComentariosResponse);
    }

    public static async Task<IResult> CriarAsync(
        Guid id,
        ComentarioRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!ValidacaoRevisao.IsValidTexto(request.Texto))
        {
            return OperacaoReacao.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await IsMembroAsync(connection, transaction, id, usuarioId, cancellationToken))
        {
            return OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada);
        }

        var texto = ValidacaoRevisao.NormalizeJustificativa(request.Texto!);
        Guid comentarioId;
        DateTimeOffset createdAt;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO comentarios (atividade_id, usuario_id, texto)
            VALUES ($1, $2, $3)
            RETURNING id, created_at
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(id);
            insert.Parameters.AddWithValue(usuarioId);
            insert.Parameters.AddWithValue(texto);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            comentarioId = reader.GetGuid(0);
            createdAt = TimestampLeitura.FromReader(reader, 1);
        }

        await NotificacoesAtividade.NotificarComentarioAsync(connection, transaction, id, usuarioId, cancellationToken);

        string nome;
        await using (var autor = new NpgsqlCommand(
            "SELECT nome_exibicao FROM usuarios WHERE id = $1",
            connection,
            transaction))
        {
            autor.Parameters.AddWithValue(usuarioId);
            nome = (string)(await autor.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            new ComentarioResponse(comentarioId, usuarioId, nome, texto, createdAt),
            AppJsonContext.Default.ComentarioResponse,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>A atividade existe e o usuário é membro ativo do grupo dela.</summary>
    private static async Task<bool> IsMembroAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid atividadeId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var acesso = new NpgsqlCommand("""
            SELECT 1
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            """, connection, transaction);
        acesso.Parameters.AddWithValue(atividadeId);
        acesso.Parameters.AddWithValue(usuarioId);
        return await acesso.ExecuteScalarAsync(cancellationToken) is not null;
    }
}
