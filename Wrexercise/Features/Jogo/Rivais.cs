using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Jogo;

/// <summary>Um Rival escolhido, com o resultado calculado na leitura.</summary>
/// <param name="Situacao">Do ponto de vista de quem escolheu (<see cref="ResultadoRival"/>).</param>
/// <param name="Vista">Se o Rival já viu a provocação.</param>
/// <param name="ResultadoVisto">Se quem escolheu já viu o resultado.</param>
internal sealed record RivalCarregado(
    Guid Id,
    Guid GrupoId,
    Guid EscolhedorId,
    string EscolhedorNome,
    Guid RivalId,
    string RivalNome,
    DateOnly SemanaInicio,
    string Situacao,
    bool Vista,
    bool ResultadoVisto);

/// <summary>Um membro ativo na semana do desafio em destaque.</summary>
/// <param name="Provisorias">As sessões verificadas mais as pendentes da semana.</param>
internal sealed record MembroSemanaRival(Guid UsuarioId, bool ModoJogo, bool Participa, int Provisorias);

/// <summary>A semana em curso do desafio em destaque, quando ele está <c>Ativo</c>.</summary>
internal sealed record SemanaRival(
    Guid DesafioId,
    SemanaDesafio Semana,
    int SessoesPorSemana,
    IReadOnlyDictionary<Guid, MembroSemanaRival> Membros)
{
    /// <summary>
    /// As duas pessoas estão no modo Jogo, participam da semana e ainda não cumpriram a meta, nem
    /// contando as pendentes (decisão 0038).
    /// </summary>
    public bool PodeEscolher(Guid escolhedorId, Guid rivalId) =>
        escolhedorId != rivalId
        && Membros.TryGetValue(escolhedorId, out var escolhedor)
        && Membros.TryGetValue(rivalId, out var rival)
        && escolhedor is { ModoJogo: true, Participa: true }
        && rival is { ModoJogo: true, Participa: true }
        && ResultadoRival.PodeEscolher(escolhedor.Provisorias, rival.Provisorias, SessoesPorSemana);

    public IReadOnlyList<Guid> GetCandidatos(Guid escolhedorId) =>
        Membros.Keys.Where(id => PodeEscolher(escolhedorId, id)).ToList();
}

internal static class Rivais
{
    /// <summary>
    /// Quando a pessoa cumpriu a meta da semana: cada dia conta no <c>verified_at</c> da primeira atividade verificada
    /// que levou as partes do dia a 100% (cada atividade vale os segundos dela divididos pelo mínimo do tipo), e a
    /// meta, no N-ésimo dia a contar, N = sessões por semana (decisões 0040 e 0045).
    /// </summary>
    private static string MetaCumpridaEmSql(string usuarioColuna) => $"""
        (SELECT v.concluido_em
         FROM (
             SELECT c.concluido_em, row_number() OVER (ORDER BY c.concluido_em) AS n
             FROM (
                 SELECT min(s.verified_at) FILTER (WHERE {ContagensAtividadeDesafio.CompletaODiaSql("s.acumulado")})
                     AS concluido_em
                 FROM (
                     SELECT a.verified_at, (a.started_at AT TIME ZONE g.fuso_horario)::date AS dia,
                         sum({ContagensAtividadeDesafio.ParteDoDiaSql(
                             "a.duracao_segundos", "coalesce(mt.duracao_minima_segundos, d.duracao_minima_segundos)")})
                             OVER (
                                 PARTITION BY (a.started_at AT TIME ZONE g.fuso_horario)::date
                                 ORDER BY a.verified_at, a.id) AS acumulado
                     FROM atividades a
                     {ContagensAtividadeDesafio.JoinMetaDoTipoSql()}
                     WHERE a.usuario_id = {usuarioColuna}
                       AND a.grupo_id = r.grupo_id
                       AND a.situacao_verificacao = 'Verificada'
                       AND (a.started_at AT TIME ZONE g.fuso_horario)::date BETWEEN r.semana_inicio AND r.semana_inicio + 6
                 ) s
                 GROUP BY s.dia
             ) c
             WHERE c.concluido_em IS NOT NULL
         ) v
         WHERE v.n = d.sessoes_por_semana)
        """;

    private static readonly string SelectSql = $"""
        SELECT r.id, r.grupo_id, r.escolhedor_id, ue.nome_exibicao, r.rival_id, ur.nome_exibicao,
            r.semana_inicio, r.vista_em IS NOT NULL, r.resultado_visto_em IS NOT NULL,
            (now() AT TIME ZONE g.fuso_horario)::date,
            {MetaCumpridaEmSql("r.escolhedor_id")},
            {MetaCumpridaEmSql("r.rival_id")}
        FROM rivais r
        JOIN desafios d ON d.id = r.desafio_id AND d.cancelled_at IS NULL
        JOIN grupos g ON g.id = r.grupo_id
        JOIN usuarios ue ON ue.id = r.escolhedor_id
        JOIN usuarios ur ON ur.id = r.rival_id
        WHERE $1 IN (r.escolhedor_id, r.rival_id)
          AND ($2::uuid IS NULL OR r.grupo_id = $2)
        ORDER BY r.semana_inicio DESC, r.created_at, r.id
        """;

    /// <summary>
    /// Os Rivais de que a pessoa participa, escolhendo ou escolhida, de todos os grupos ou só de um.
    /// Desafios cancelados não têm Rival.
    /// </summary>
    public static async Task<List<RivalCarregado>> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        Guid? grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(SelectSql, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)grupoId ?? DBNull.Value);

        var rivais = new List<RivalCarregado>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var semanaInicio = reader.GetFieldValue<DateOnly>(6);
            var situacao = ResultadoRival.Calcular(
                reader.IsDBNull(10) ? null : TimestampLeitura.FromReader(reader, 10),
                reader.IsDBNull(11) ? null : TimestampLeitura.FromReader(reader, 11),
                semanaInicio,
                reader.GetFieldValue<DateOnly>(9));
            rivais.Add(new RivalCarregado(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetGuid(4),
                reader.GetString(5),
                semanaInicio,
                situacao,
                reader.GetBoolean(7),
                reader.GetBoolean(8)));
        }

        return rivais;
    }

    /// <returns>A semana em curso do desafio em destaque, ou <c>null</c> sem desafio <c>Ativo</c>.</returns>
    public static async Task<SemanaRival?> LoadSemanaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        var desafio = await DesafioDestaqueGrupo.LoadAsync(connection, grupoId, cancellationToken);
        if (desafio is not { Situacao: SituacaoDesafio.Ativo }
            || CalculoSemanaDesafio.GetSemana(desafio.DataInicio, desafio.DataFim, desafio.Hoje) is not { } semana)
        {
            return null;
        }

        var provisorias = new Dictionary<Guid, int>();
        await using (var command = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildAgregadoPorUsuarioSemanaAtual(), connection, transaction))
        {
            command.Parameters.AddWithValue(desafio.Id);
            command.Parameters.AddWithValue(semana.Inicio);
            command.Parameters.AddWithValue(semana.Fim);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                provisorias[reader.GetGuid(0)] = reader.GetInt32(1) + reader.GetInt32(2);
            }
        }

        var membros = new Dictionary<Guid, MembroSemanaRival>();
        await using (var command = new NpgsqlCommand("""
            SELECT m.usuario_id, u.modo_inicio = 'Jogo', (m.joined_at AT TIME ZONE g.fuso_horario)::date
            FROM membros_grupo m
            JOIN grupos g ON g.id = m.grupo_id
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $1 AND m.removed_at IS NULL
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(grupoId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var usuarioId = reader.GetGuid(0);
                membros[usuarioId] = new MembroSemanaRival(
                    usuarioId,
                    reader.GetBoolean(1),
                    CalculoSemanaDesafio.ParticipaDaSemana(semana.Inicio, reader.GetFieldValue<DateOnly>(2)),
                    provisorias.GetValueOrDefault(usuarioId));
            }
        }

        return new SemanaRival(desafio.Id, semana, desafio.SessoesPorSemana, membros);
    }
}
