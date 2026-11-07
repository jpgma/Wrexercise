using Npgsql;
using Wrexercise.Features.Desafios.Contracts;

namespace Wrexercise.Features.Progresso.Contracts;

internal sealed record DesafioDestaque(
    Guid Id,
    string Nome,
    string Situacao,
    int DuracaoMinimaSegundos,
    int SessoesPorSemana,
    DateOnly DataInicio,
    DateOnly DataFim,
    DateOnly Hoje);

/// <summary>Regra única do "desafio em destaque" de um grupo: resumo do grupo e cena do Jogo a usam.</summary>
internal static class DesafioDestaqueGrupo
{
    /// <returns>O primeiro desafio não cancelado na ordem de destaque, ou <c>null</c>.</returns>
    public static async Task<DesafioDestaque?> LoadAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT d.id, d.nome, d.data_inicio, d.data_fim, d.cancelled_at IS NOT NULL,
                (now() AT TIME ZONE g.fuso_horario)::date, d.duracao_minima_segundos,
                d.sessoes_por_semana
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            WHERE d.grupo_id = $1
            ORDER BY
                d.cancelled_at IS NOT NULL,
                d.data_fim < (now() AT TIME ZONE g.fuso_horario)::date,
                CASE WHEN d.data_fim >= (now() AT TIME ZONE g.fuso_horario)::date THEN d.data_inicio END,
                d.data_inicio DESC,
                d.id
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetBoolean(4))
            {
                continue;
            }

            var inicio = reader.GetFieldValue<DateOnly>(2);
            var fim = reader.GetFieldValue<DateOnly>(3);
            var hoje = reader.GetFieldValue<DateOnly>(5);
            return new DesafioDestaque(
                reader.GetGuid(0),
                reader.GetString(1),
                GetSituacao(inicio, fim, cancelado: false, hoje),
                reader.GetInt32(6),
                reader.GetInt32(7),
                inicio,
                fim,
                hoje);
        }

        return null;
    }

    public static string GetSituacao(DateOnly inicio, DateOnly fim, bool cancelado, DateOnly hoje)
    {
        if (cancelado)
        {
            return SituacaoDesafio.Cancelado;
        }

        if (hoje < inicio)
        {
            return SituacaoDesafio.Agendado;
        }

        return hoje > fim ? SituacaoDesafio.Encerrado : SituacaoDesafio.Ativo;
    }
}
