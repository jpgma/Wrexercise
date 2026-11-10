namespace Wrexercise.Infrastructure.Database;

/// <summary>
/// Fragmento SQL canônico para contagens de dias elegíveis a um desafio: as atividades de um dia se somam,
/// e um dia conta como uma sessão quando a soma alcança a duração mínima do desafio ou quando a soma
/// dos tipos de uma meta por tipo alcança o mínimo dela.
/// Ver docs/decisoes/0013-competicao-e-progresso.md, 0040-meta-diaria.md e 0043-metas-por-tipo.md.
/// </summary>
internal static class ContagensAtividadeDesafio
{
    internal const string DiaSql = "(a.started_at AT TIME ZONE g.fuso_horario)::date";

    internal const string IndiceSemanaSql =
        "((a.started_at AT TIME ZONE g.fuso_horario)::date - d.data_inicio) / 7";

    /// <summary>Dias em que as atividades verificadas cumprem alguma condição.</summary>
    internal const string VerificadasSql = "count(*) FILTER (WHERE dias.conta_verificada)::int";

    /// <summary>Dias que só cumprem alguma condição somando as atividades pendentes.</summary>
    internal const string PendentesSql =
        "count(*) FILTER (WHERE NOT dias.conta_verificada AND dias.conta_provisoria)::int";

    /// <summary>
    /// Corpo comum: atividades de membros ativos, só em semanas que começaram no dia
    /// da entrada do membro ou depois. Parâmetro $1 = desafio_id.
    /// </summary>
    internal const string FromElegiveisSql = $"""
        FROM atividades a
        JOIN desafios d ON d.id = $1 AND d.grupo_id = a.grupo_id
        JOIN grupos g ON g.id = d.grupo_id
        JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = a.usuario_id AND m.removed_at IS NULL
        WHERE {DiaSql} >= d.data_inicio
          AND {DiaSql} <= d.data_fim
          AND d.data_inicio + {IndiceSemanaSql} * 7 >= (m.joined_at AT TIME ZONE g.fuso_horario)::date
        """;

    /// <summary>Só os dias entre $2 e $3.</summary>
    private const string FiltroSemanaSql = $"AND {DiaSql} >= $2 AND {DiaSql} <= $3";

    /// <summary>
    /// Avalia as condições de cada pessoa e dia. <paramref name="diasPorTipoSql"/> devolve uma linha por
    /// desafio, pessoa, dia e tipo, com as colunas <c>desafio_id</c>, <c>usuario_id</c>, <c>dia</c>,
    /// <c>indice_semana</c>, <c>minimo</c>, <c>tipo</c>, <c>segundos_verificados</c> e <c>segundos_provisorios</c>.
    /// O resultado tem uma linha por desafio, pessoa e dia, com as somas totais, <c>conta_verificada</c>,
    /// <c>conta_provisoria</c> e <c>meta_por_tipo_id</c>: nulo quando a duração mínima do desafio foi
    /// alcançada, senão a primeira meta por tipo cumprida, contando as pendentes.
    /// </summary>
    internal static string BuildDiasPorCondicao(string diasPorTipoSql) => $"""
        WITH dias_tipo AS ({diasPorTipoSql}),
        condicoes AS (
            SELECT dt.desafio_id, dt.usuario_id, dt.dia, dt.indice_semana,
                NULL::uuid AS meta_id, NULL::smallint AS meta_ordem, dt.minimo,
                sum(dt.segundos_verificados) AS segundos_verificados,
                sum(dt.segundos_provisorios) AS segundos_provisorios
            FROM dias_tipo dt
            GROUP BY dt.desafio_id, dt.usuario_id, dt.dia, dt.indice_semana, dt.minimo
            UNION ALL
            SELECT dt.desafio_id, dt.usuario_id, dt.dia, dt.indice_semana,
                mt.id, mt.ordem, mt.duracao_minima_segundos,
                sum(dt.segundos_verificados),
                sum(dt.segundos_provisorios)
            FROM dias_tipo dt
            JOIN metas_tipo_desafio mt ON mt.desafio_id = dt.desafio_id AND dt.tipo = ANY(mt.tipos)
            GROUP BY dt.desafio_id, dt.usuario_id, dt.dia, dt.indice_semana,
                mt.id, mt.ordem, mt.duracao_minima_segundos
        )
        SELECT c.desafio_id, c.usuario_id, c.dia, c.indice_semana,
            max(c.segundos_verificados) FILTER (WHERE c.meta_id IS NULL)::int AS segundos_verificados,
            max(c.segundos_provisorios) FILTER (WHERE c.meta_id IS NULL)::int AS segundos_provisorios,
            bool_or(c.segundos_verificados >= c.minimo) AS conta_verificada,
            bool_or(c.segundos_provisorios >= c.minimo) AS conta_provisoria,
            CASE WHEN bool_or(c.segundos_provisorios >= c.minimo) FILTER (WHERE c.meta_id IS NULL) THEN NULL
                ELSE (array_agg(c.meta_id ORDER BY c.meta_ordem)
                    FILTER (WHERE c.meta_id IS NOT NULL AND c.segundos_provisorios >= c.minimo))[1]
            END AS meta_por_tipo_id
        FROM condicoes c
        GROUP BY c.desafio_id, c.usuario_id, c.dia, c.indice_semana
        """;

    /// <summary>
    /// Uma linha por pessoa e dia, com as somas e as condições cumpridas (ver <see cref="BuildDiasPorCondicao"/>).
    /// <paramref name="filtros"/> acrescenta condições <c>AND</c> ao <see cref="FromElegiveisSql"/>.
    /// </summary>
    private static string BuildDias(string filtros = "") => BuildDiasPorCondicao($"""
        SELECT d.id AS desafio_id,
            a.usuario_id,
            {DiaSql} AS dia,
            {IndiceSemanaSql} AS indice_semana,
            d.duracao_minima_segundos AS minimo,
            a.tipo,
            coalesce(sum(a.duracao_segundos) FILTER (WHERE a.situacao_verificacao = 'Verificada'), 0)
                AS segundos_verificados,
            coalesce(sum(a.duracao_segundos) FILTER (WHERE a.situacao_verificacao <> 'Rejeitada'), 0)
                AS segundos_provisorios
        {FromElegiveisSql}
        {filtros}
        GROUP BY d.id, a.usuario_id, {DiaSql}, {IndiceSemanaSql}, d.duracao_minima_segundos, a.tipo
        """);

    internal static string BuildAgregadoPorUsuarioESemana() => $"""
        SELECT dias.usuario_id,
            dias.indice_semana,
            {VerificadasSql} AS verificadas,
            {PendentesSql} AS pendentes
        FROM ({BuildDias()}) dias
        WHERE dias.conta_provisoria
        GROUP BY dias.usuario_id, dias.indice_semana
        """;

    /// <summary>
    /// Os dias da semana com atividade não rejeitada de quem consulta: a soma das durações, se o dia conta
    /// para a meta (contando as pendentes) e a meta por tipo que o fez contar.
    /// $1 = desafio_id, $2 e $3 = a semana, $4 = usuario_id.
    /// </summary>
    internal static string BuildDiasDaSemana() => $"""
        SELECT dias.dia, dias.segundos_provisorios, dias.conta_provisoria, dias.meta_por_tipo_id
        FROM ({BuildDias(FiltroSemanaSql + " AND a.usuario_id = $4")}) dias
        WHERE dias.segundos_provisorios > 0
        ORDER BY dias.dia
        """;

    /// <summary>
    /// A soma das durações não rejeitadas de quem consulta, por dia e tipo, na semana.
    /// $1 = desafio_id, $2 e $3 = a semana, $4 = usuario_id.
    /// </summary>
    internal const string SegundosPorTipoDaSemanaSql = $"""
        SELECT {DiaSql}, a.tipo, sum(a.duracao_segundos)::int
        {FromElegiveisSql}
        {FiltroSemanaSql}
          AND a.usuario_id = $4
          AND a.situacao_verificacao <> 'Rejeitada'
        GROUP BY {DiaSql}, a.tipo
        ORDER BY {DiaSql}, a.tipo
        """;

    internal static string BuildAgregadoPorUsuarioSemanaAtual() => $"""
        SELECT dias.usuario_id,
            {VerificadasSql} AS verificadas,
            {PendentesSql} AS pendentes
        FROM ({BuildDias(FiltroSemanaSql)}) dias
        WHERE dias.conta_provisoria
        GROUP BY dias.usuario_id
        """;
}
