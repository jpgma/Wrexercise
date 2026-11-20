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
            List<(Guid AtividadeId, AtividadeJogada Atividade)> Candidatas)>();

        // Para cada Jogada aceita cuja janela contém a atividade, todas as candidatas da mesma janela, também as
        // pendentes: elas dizem qual é o próximo treino (decisão 0075).
        await using (var command = new NpgsqlCommand($"""
            SELECT j.id, j.remetente_id, j.destinatario_id, {Jogadas.ObjetivoSql}, j.enviada_em, j.expira_em, j.resposta,
                j.respondida_em, j.duracao_alvo_segundos, j.tipo_evitar,
                o.id, o.started_at AT TIME ZONE g.fuso_horario, o.tipo, o.duracao_segundos, cf.codigo,
                o.situacao_verificacao = 'Verificada', o.created_at
            FROM atividades x
            JOIN jogadas j ON j.destinatario_id = x.usuario_id AND j.grupo_id = x.grupo_id
                AND j.resposta = 'Aceita'
                AND x.started_at >= {Jogadas.InicioJanelaSql(2)} AND x.started_at <= j.expira_em
            JOIN cartas c ON c.id = j.carta_id
            LEFT JOIN cartas cf ON cf.id = j.carta_fusao_id
            JOIN grupos g ON g.id = j.grupo_id
            JOIN atividades o ON o.usuario_id = j.destinatario_id AND o.grupo_id = j.grupo_id
                AND o.situacao_verificacao NOT IN ('Rejeitada', 'Anulada')
                AND o.started_at >= {Jogadas.InicioJanelaSql(2)} AND o.started_at <= j.expira_em
            WHERE x.id = $1 AND x.situacao_verificacao = 'Verificada'
            ORDER BY o.created_at, o.id
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(atividadeId);
            command.Parameters.AddWithValue(ObjetivoCarta.InicioProximoTreino.ToUniversalTime());
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
                            reader.IsDBNull(9) ? null : reader.GetString(9),
                            reader.IsDBNull(14) ? null : reader.GetString(14)),
                        []);
                    jogadas[jogadaId] = jogada;
                }

                jogada.Candidatas.Add((
                    reader.GetGuid(10),
                    new AtividadeJogada(
                        reader.GetDateTime(11),
                        reader.GetString(12),
                        reader.GetInt32(13),
                        reader.GetBoolean(15),
                        TimestampLeitura.FromReader(reader, 16))));
            }
        }

        foreach (var (jogadaId, jogada) in jogadas)
        {
            if (!ConcluiAgora(jogada.Estado, atividadeId, jogada.Candidatas))
            {
                continue;
            }

            // Só o remetente: quem cumpriu já sabe que treinou (decisão 0048).
            await NotificacoesJogada.NotificarAsync(
                connection, transaction, jogadaId, jogada.Remetente, jogada.Destinatario,
                TipoNotificacaoJogada.JogadaConcluida, cancellationToken);
        }
    }

    /// <summary>
    /// Regra pura: com esta atividade verificada, as verificadas cumprem o objetivo, e com ela ainda pendente não
    /// cumpriam. Nas cartas de duração, ela pode completar a soma de um dia (decisão 0040). As pendentes ficam na
    /// lista porque dizem qual é o próximo treino (decisão 0075).
    /// </summary>
    public static bool ConcluiAgora(
        JogadaEstado jogada,
        Guid atividadeId,
        IReadOnlyList<(Guid AtividadeId, AtividadeJogada Atividade)> candidatas) =>
        jogada.RespondidaEm < jogada.ExpiraEm
        && candidatas.Any(v => v.AtividadeId == atividadeId && v.Atividade.Verificada)
        && ObjetivoCarta.Cumpre(jogada, candidatas.Select(v => v.Atividade), apenasVerificadas: true)
        && !ObjetivoCarta.Cumpre(
            jogada,
            candidatas.Select(v => v.AtividadeId == atividadeId ? v.Atividade with { Verificada = false } : v.Atividade),
            apenasVerificadas: true);
}
