using Npgsql;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Jogo.Contracts;

/// <summary>
/// Chamado por toda operação que deixa uma atividade <c>Verificada</c>, na mesma transação.
/// A conclusão de uma Jogada é calculada na leitura; só o aviso precisa ser gravado no momento certo.
/// </summary>
internal static class JogadasConcluidas
{
    /// <summary>
    /// Avisa os dois lados das Jogadas que esta atividade concluiu agora: ela cumpre o objetivo, e
    /// nenhuma outra atividade já verificada o cumpria antes.
    /// </summary>
    public static async Task NotificarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        var jogadas = new Dictionary<Guid, (Guid Remetente, Guid Destinatario, JogadaEstado Estado,
            List<(Guid AtividadeId, AtividadeJogada Atividade)> Verificadas)>();

        // Para cada Jogada aceita cuja janela contém a atividade, todas as verificadas na mesma janela.
        await using (var command = new NpgsqlCommand($"""
            SELECT j.id, j.remetente_id, j.destinatario_id, {Jogadas.ObjetivoSql}, j.enviada_em, j.expira_em, j.resposta,
                j.respondida_em, j.duracao_alvo_segundos, j.tipo_evitar,
                o.id, o.started_at AT TIME ZONE g.fuso_horario, o.tipo, o.duracao_segundos
            FROM atividades x
            JOIN jogadas j ON j.destinatario_id = x.usuario_id AND j.grupo_id = x.grupo_id
                AND j.resposta = 'Aceita'
                AND x.started_at >= j.enviada_em AND x.started_at <= j.expira_em
            JOIN cartas c ON c.id = j.carta_id
            JOIN grupos g ON g.id = j.grupo_id
            JOIN atividades o ON o.usuario_id = j.destinatario_id AND o.grupo_id = j.grupo_id
                AND o.situacao_verificacao = 'Verificada'
                AND o.started_at >= j.enviada_em AND o.started_at <= j.expira_em
            WHERE x.id = $1 AND x.situacao_verificacao = 'Verificada'
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(atividadeId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var jogadaId = reader.GetGuid(0);
                if (!jogadas.TryGetValue(jogadaId, out var jogada))
                {
                    jogada = (
                        reader.GetGuid(1),
                        reader.GetGuid(2),
                        new JogadaEstado(
                            reader.GetString(3),
                            TimestampLeitura.FromReader(reader, 4),
                            TimestampLeitura.FromReader(reader, 5),
                            reader.GetString(6),
                            TimestampLeitura.FromReader(reader, 7),
                            reader.IsDBNull(8) ? null : reader.GetInt32(8),
                            reader.IsDBNull(9) ? null : reader.GetString(9)),
                        []);
                    jogadas[jogadaId] = jogada;
                }

                jogada.Verificadas.Add((
                    reader.GetGuid(10),
                    new AtividadeJogada(reader.GetDateTime(11), reader.GetString(12), reader.GetInt32(13), Verificada: true)));
            }
        }

        foreach (var (jogadaId, jogada) in jogadas)
        {
            if (!ConcluiAgora(jogada.Estado, atividadeId, jogada.Verificadas))
            {
                continue;
            }

            await NotificacoesJogada.NotificarAsync(
                connection, transaction, jogadaId, jogada.Remetente, jogada.Destinatario,
                TipoNotificacaoJogada.JogadaConcluida, cancellationToken);
            await NotificacoesJogada.NotificarAsync(
                connection, transaction, jogadaId, jogada.Destinatario, null,
                TipoNotificacaoJogada.JogadaConcluida, cancellationToken);
        }
    }

    /// <summary>Regra pura: esta atividade conclui a Jogada, e nenhuma outra verificada já a concluía.</summary>
    public static bool ConcluiAgora(
        JogadaEstado jogada,
        Guid atividadeId,
        IReadOnlyList<(Guid AtividadeId, AtividadeJogada Atividade)> verificadas) =>
        jogada.RespondidaEm < jogada.ExpiraEm
        && verificadas.Any(v => v.AtividadeId == atividadeId && ObjetivoCarta.Cumpre(jogada, v.Atividade))
        && !verificadas.Any(v => v.AtividadeId != atividadeId && ObjetivoCarta.Cumpre(jogada, v.Atividade));
}
