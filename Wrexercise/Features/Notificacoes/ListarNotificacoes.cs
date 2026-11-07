using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Notificacoes;

/// <summary>Toda leitura cruza com o vínculo ativo: quem saiu de um grupo não vê mais nada dele.</summary>
internal static class ListarNotificacoes
{
    // Sem paginação: a retenção de 90 dias e este limite mantêm a lista curta.
    private const int Limite = 50;

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            SELECT n.id, n.tipo, n.atividade_id, n.jogada_id, n.grupo_id, g.nome, u.nome_exibicao, n.situacao,
                n.total, n.read_at IS NOT NULL, n.updated_at
            FROM notificacoes n
            JOIN membros_grupo m ON m.grupo_id = n.grupo_id AND m.usuario_id = n.usuario_id AND m.removed_at IS NULL
            JOIN grupos g ON g.id = n.grupo_id
            LEFT JOIN usuarios u ON u.id = n.ator_id
            WHERE n.usuario_id = $1
            ORDER BY n.updated_at DESC, n.id
            LIMIT {Limite}
            """);
        command.Parameters.AddWithValue(context.GetSessaoAtual().UsuarioId);

        var notificacoes = new List<NotificacaoResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notificacoes.Add(new NotificacaoResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetGuid(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt32(8),
                reader.GetBoolean(9),
                TimestampLeitura.FromReader(reader, 10)));
        }

        return Results.Json(new NotificacoesResponse(notificacoes), AppJsonContext.Default.NotificacoesResponse);
    }

    public static async Task<IResult> ContarNaoLidasAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT count(*)::int
            FROM notificacoes n
            JOIN membros_grupo m ON m.grupo_id = n.grupo_id AND m.usuario_id = n.usuario_id AND m.removed_at IS NULL
            WHERE n.usuario_id = $1 AND n.read_at IS NULL
            """);
        command.Parameters.AddWithValue(context.GetSessaoAtual().UsuarioId);
        var total = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        return Results.Json(new NotificacoesNaoLidasResponse(total), AppJsonContext.Default.NotificacoesNaoLidasResponse);
    }
}
