using Npgsql;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Jogo;

/// <summary>Uma Jogada lida do banco, com a situação já calculada.</summary>
internal sealed record JogadaCarregada(
    Guid Id,
    Guid GrupoId,
    Guid RemetenteId,
    Guid DestinatarioId,
    Guid CartaId,
    int Preco,
    JogadaEstado Estado,
    DateTimeOffset? VistaEm,
    DateTimeOffset? ResultadoVistoEm,
    string Situacao)
{
    public JogadaLimite ToLimite() => new(RemetenteId, DestinatarioId, Estado.EnviadaEm, Situacao);
}

/// <summary>SQL compartilhado: as Jogadas e as atividades que podem cumpri-las.</summary>
internal static class Jogadas
{
    /// <summary>
    /// As Jogadas em que alguma das duas pessoas participa, em qualquer grupo. Para uma pessoa só,
    /// passe o mesmo identificador duas vezes.
    /// </summary>
    public static async Task<List<JogadaCarregada>> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioA,
        Guid usuarioB,
        CancellationToken cancellationToken)
    {
        var linhas = new List<(Guid Id, Guid GrupoId, Guid RemetenteId, Guid DestinatarioId, Guid CartaId, int Preco,
            JogadaEstado Estado, DateTimeOffset? VistaEm, DateTimeOffset? ResultadoVistoEm)>();
        DateTimeOffset agora;

        await using (var command = new NpgsqlCommand("""
            SELECT j.id, j.grupo_id, j.remetente_id, j.destinatario_id, j.carta_id, c.preco, c.codigo,
                j.enviada_em, j.expira_em, j.resposta, j.respondida_em, j.duracao_alvo_segundos, j.tipo_evitar,
                j.vista_em, j.resultado_visto_em, now()
            FROM jogadas j
            JOIN cartas c ON c.id = j.carta_id
            WHERE $1 IN (j.remetente_id, j.destinatario_id) OR $2 IN (j.remetente_id, j.destinatario_id)
            ORDER BY j.enviada_em DESC, j.id
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(usuarioA);
            command.Parameters.AddWithValue(usuarioB);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            agora = DateTimeOffset.UtcNow;
            while (await reader.ReadAsync(cancellationToken))
            {
                agora = TimestampLeitura.FromReader(reader, 15);
                linhas.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetGuid(3),
                    reader.GetGuid(4),
                    reader.GetInt32(5),
                    new JogadaEstado(
                        reader.GetString(6),
                        TimestampLeitura.FromReader(reader, 7),
                        TimestampLeitura.FromReader(reader, 8),
                        reader.IsDBNull(9) ? null : reader.GetString(9),
                        reader.IsDBNull(10) ? null : TimestampLeitura.FromReader(reader, 10),
                        reader.IsDBNull(11) ? null : reader.GetInt32(11),
                        reader.IsDBNull(12) ? null : reader.GetString(12)),
                    reader.IsDBNull(13) ? null : TimestampLeitura.FromReader(reader, 13),
                    reader.IsDBNull(14) ? null : TimestampLeitura.FromReader(reader, 14)));
            }
        }

        var atividades = await LoadAtividadesAsync(connection, transaction, usuarioA, usuarioB, cancellationToken);

        return linhas
            .Select(l => new JogadaCarregada(
                l.Id,
                l.GrupoId,
                l.RemetenteId,
                l.DestinatarioId,
                l.CartaId,
                l.Preco,
                l.Estado,
                l.VistaEm,
                l.ResultadoVistoEm,
                SituacaoJogada.Calcular(
                    l.Estado,
                    atividades.TryGetValue(l.Id, out var candidatas) ? candidatas : [],
                    agora)))
            .ToList();
    }

    /// <summary>
    /// As atividades que podem cumprir cada Jogada aceita: não rejeitadas, do destinatário, no grupo da
    /// Jogada e com início entre o envio e o prazo. O objetivo da carta é conferido em <see cref="ObjetivoCarta"/>.
    /// </summary>
    public static async Task<Dictionary<Guid, List<AtividadeJogada>>> LoadAtividadesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioA,
        Guid usuarioB,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT j.id, a.started_at AT TIME ZONE g.fuso_horario, a.tipo, a.duracao_segundos,
                a.situacao_verificacao = 'Verificada'
            FROM jogadas j
            JOIN grupos g ON g.id = j.grupo_id
            JOIN atividades a ON a.usuario_id = j.destinatario_id AND a.grupo_id = j.grupo_id
            WHERE ($1 IN (j.remetente_id, j.destinatario_id) OR $2 IN (j.remetente_id, j.destinatario_id))
              AND j.resposta = 'Aceita'
              AND a.situacao_verificacao <> 'Rejeitada'
              AND a.started_at >= j.enviada_em
              AND a.started_at <= j.expira_em
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioA);
        command.Parameters.AddWithValue(usuarioB);

        var atividades = new Dictionary<Guid, List<AtividadeJogada>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var jogadaId = reader.GetGuid(0);
            if (!atividades.TryGetValue(jogadaId, out var lista))
            {
                atividades[jogadaId] = lista = [];
            }

            lista.Add(new AtividadeJogada(reader.GetDateTime(1), reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4)));
        }

        return atividades;
    }
}
