using System.Text.Json;
using Npgsql;
using Wrexercise.Infrastructure.Json;
using Wrexercise.Infrastructure.Push;

namespace Wrexercise.Features.Notificacoes;

/// <summary>O que o Service Worker recebe, já cifrado, para mostrar o aviso (`webapp/sw.js`).</summary>
internal sealed record PushMensagem(Guid Id, string Titulo, string Corpo, string Url, int NaoLidas);

/// <summary>
/// Envia ao telefone as notificações ainda não enviadas. Marca antes de enviar: no máximo uma
/// tentativa por mudança, e o que falhar continua na lista do app. Ver docs/decisoes/0049-notificacoes-push.md.
/// </summary>
internal static class EnvioPush
{
    private const int Lote = 100;

    public static async Task<int> EnviarPendentesAsync(
        NpgsqlDataSource dataSource,
        WebPushClient push,
        CancellationToken cancellationToken)
    {
        var enviadas = 0;
        while (true)
        {
            var (marcadas, avisos) = await MarcarPendentesAsync(dataSource, cancellationToken);
            if (avisos.Count > 0)
            {
                var destinos = await GetDestinosAsync(
                    dataSource,
                    avisos.Select(a => a.UsuarioId).Distinct().ToArray(),
                    cancellationToken);
                var expiradas = new List<string>();
                foreach (var aviso in avisos)
                {
                    var conteudo = JsonSerializer.SerializeToUtf8Bytes(aviso.Mensagem, AppJsonContext.Default.PushMensagem);
                    foreach (var destino in destinos.Where(d => d.UsuarioId == aviso.UsuarioId))
                    {
                        var resultado = await push.EnviarAsync(
                            destino.Destino, conteudo, aviso.Mensagem.Id.ToString("N"), cancellationToken);
                        if (resultado == ResultadoPush.Entregue)
                        {
                            enviadas++;
                        }
                        else if (resultado == ResultadoPush.Expirada)
                        {
                            expiradas.Add(destino.Destino.Endpoint);
                        }
                    }
                }

                await RemoverExpiradasAsync(dataSource, expiradas, cancellationToken);
            }

            if (marcadas < Lote)
            {
                return enviadas;
            }
        }
    }

    // Marca o lote inteiro, mas só vai ao telefone o que ainda importa: não lida, recente, de um tipo com push
    // e de um grupo do qual a pessoa ainda participa.
    private static async Task<(int Marcadas, List<(Guid UsuarioId, PushMensagem Mensagem)> Avisos)> MarcarPendentesAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        await using (var marcar = dataSource.CreateCommand($"""
            UPDATE notificacoes
            SET push_enviado_em = now()
            WHERE id IN (
                SELECT id FROM notificacoes
                WHERE push_enviado_em IS NULL
                ORDER BY updated_at
                LIMIT {Lote})
            RETURNING id
            """))
        {
            await using var reader = await marcar.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
        }

        var avisos = new List<(Guid, PushMensagem)>();
        if (ids.Count == 0)
        {
            return (0, avisos);
        }

        await using var command = dataSource.CreateCommand("""
            SELECT n.id, n.usuario_id, n.tipo, n.grupo_id, g.nome, n.atividade_id, u.nome_exibicao, n.situacao, n.total,
                (SELECT count(*)::int
                 FROM notificacoes t
                 JOIN membros_grupo tm ON tm.grupo_id = t.grupo_id AND tm.usuario_id = t.usuario_id AND tm.removed_at IS NULL
                 WHERE t.usuario_id = n.usuario_id AND t.read_at IS NULL)
            FROM notificacoes n
            JOIN membros_grupo m ON m.grupo_id = n.grupo_id AND m.usuario_id = n.usuario_id AND m.removed_at IS NULL
            JOIN grupos g ON g.id = n.grupo_id
            LEFT JOIN usuarios u ON u.id = n.ator_id
            WHERE n.id = ANY($1)
              AND n.read_at IS NULL
              AND n.updated_at > now() - interval '1 hour'
              AND n.tipo = ANY($2)
              AND EXISTS (SELECT 1 FROM push_subscriptions s WHERE s.usuario_id = n.usuario_id)
            ORDER BY n.updated_at
            """);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid, ids.ToArray());
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, TextosPush.Tipos);

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var aviso = new AvisoPush(
                    reader.GetString(2),
                    reader.GetGuid(3),
                    reader.IsDBNull(5) ? null : reader.GetGuid(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetInt32(8));
                avisos.Add((reader.GetGuid(1), new PushMensagem(
                    reader.GetGuid(0),
                    reader.GetString(4),
                    TextosPush.GetTexto(aviso),
                    TextosPush.GetDestino(aviso),
                    reader.GetInt32(9))));
            }
        }

        return (ids.Count, avisos);
    }

    private static async Task<List<(Guid UsuarioId, DestinoPush Destino)>> GetDestinosAsync(
        NpgsqlDataSource dataSource,
        Guid[] usuarios,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT usuario_id, endpoint, p256dh, auth FROM push_subscriptions WHERE usuario_id = ANY($1)");
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid, usuarios);

        var destinos = new List<(Guid, DestinoPush)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            destinos.Add((reader.GetGuid(0), new DestinoPush(reader.GetString(1), reader.GetString(2), reader.GetString(3))));
        }

        return destinos;
    }

    private static async Task RemoverExpiradasAsync(
        NpgsqlDataSource dataSource,
        List<string> endpoints,
        CancellationToken cancellationToken)
    {
        if (endpoints.Count == 0)
        {
            return;
        }

        await using var command = dataSource.CreateCommand("DELETE FROM push_subscriptions WHERE endpoint = ANY($1)");
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, endpoints.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
