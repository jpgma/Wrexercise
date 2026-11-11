namespace Wrexercise.Infrastructure.Database;

/// <summary>
/// Fragmento SQL canônico para contagens de dias elegíveis a um desafio: as atividades de um dia se somam,
/// cada uma valendo a parte do dia dada pelo mínimo do tipo dela, e um dia conta como uma sessão quando
/// as partes somam 100%.
/// Ver docs/decisoes/0013-competicao-e-progresso.md, 0040-meta-diaria.md, 0043-metas-por-tipo.md e
/// 0045-metas-proporcionais.md.
/// </summary>
internal static class ContagensAtividadeDesafio
{
    internal const string DiaSql = "(a.started_at AT TIME ZONE g.fuso_horario)::date";

    internal const string IndiceSemanaSql =
        "((a.started_at AT TIME ZONE g.fuso_horario)::date - d.data_inicio) / 7";

    /// <summary>Dias que as atividades verificadas completam.</summary>
    internal const string VerificadasSql = "count(*) FILTER (WHERE dias.conta_verificada)::int";

    /// <summary>Dias que só se completam somando as atividades pendentes.</summary>
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
    /// Junta à atividade <c>a</c> a meta por tipo do desafio <c>d</c> que inclui o tipo dela, se houver
    /// (colunas <c>mt.id</c>, <c>mt.ordem</c> e <c>mt.duracao_minima_segundos</c>). Um tipo fica em no
    /// máximo uma meta (decisão 0045); o <c>LIMIT 1</c> só protege dados anteriores a essa regra.
    /// </summary>
    internal static string JoinMetaDoTipoSql(string desafioId = "d.id", string tipo = "a.tipo") => $"""
        LEFT JOIN LATERAL (
            SELECT m.id, m.ordem, m.duracao_minima_segundos
            FROM metas_tipo_desafio m
            WHERE m.desafio_id = {desafioId} AND {tipo} = ANY(m.tipos)
            ORDER BY m.ordem
            LIMIT 1
        ) mt ON true
        """;

    /// <summary>
    /// A parte do dia que <paramref name="segundos"/> preenchem: os segundos divididos pelo mínimo do tipo
    /// (o da meta por tipo dele ou, sem meta, o do desafio).
    /// </summary>
    internal static string ParteDoDiaSql(string segundos, string minimo) => $"({segundos})::numeric / ({minimo})";

    /// <summary>
    /// O dia conta quando as partes somam 1. O arredondamento evita perder um dia por um resíduo da divisão
    /// (por exemplo, 3 × 30/90): a diferença ignorada é menor que um milissegundo.
    /// </summary>
    internal static string CompletaODiaSql(string somaDasPartes) => $"round({somaDasPartes}, 9) >= 1";

    /// <summary>
    /// Avalia cada pessoa e dia pela regra proporcional (decisão 0045): cada atividade preenche uma parte
    /// do dia, os segundos dela divididos pelo mínimo do tipo, e o dia conta quando as partes somam 100%.
    /// <paramref name="diasPorTipoSql"/> devolve uma linha por desafio, pessoa, dia e tipo, com as colunas
    /// <c>desafio_id</c>, <c>usuario_id</c>, <c>dia</c>, <c>indice_semana</c>, <c>minimo</c>, <c>tipo</c>,
    /// <c>segundos_verificados</c> e <c>segundos_provisorios</c>.
    /// O resultado tem uma linha por desafio, pessoa e dia, com as somas totais, <c>conta_verificada</c>,
    /// <c>conta_provisoria</c> e <c>meta_por_tipo_id</c>: a meta por tipo que mais preencheu o dia, ou
    /// nulo quando os tipos sem meta preencheram mais (ou quando o dia não conta).
    /// </summary>
    internal static string BuildDiasPorCondicao(string diasPorTipoSql) => $"""
        WITH dias_tipo AS ({diasPorTipoSql}),
        partes AS (
            SELECT dt.desafio_id, dt.usuario_id, dt.dia, dt.indice_semana, mt.id AS meta_id, mt.ordem AS meta_ordem,
                sum(dt.segundos_verificados) AS segundos_verificados,
                sum(dt.segundos_provisorios) AS segundos_provisorios,
                sum({ParteDoDiaSql("dt.segundos_verificados", "coalesce(mt.duracao_minima_segundos, dt.minimo)")})
                    AS parte_verificada,
                sum({ParteDoDiaSql("dt.segundos_provisorios", "coalesce(mt.duracao_minima_segundos, dt.minimo)")})
                    AS parte_provisoria
            FROM dias_tipo dt
            {JoinMetaDoTipoSql("dt.desafio_id", "dt.tipo")}
            GROUP BY dt.desafio_id, dt.usuario_id, dt.dia, dt.indice_semana, mt.id, mt.ordem
        )
        SELECT p.desafio_id, p.usuario_id, p.dia, p.indice_semana,
            sum(p.segundos_verificados)::int AS segundos_verificados,
            sum(p.segundos_provisorios)::int AS segundos_provisorios,
            {CompletaODiaSql("sum(p.parte_verificada)")} AS conta_verificada,
            {CompletaODiaSql("sum(p.parte_provisoria)")} AS conta_provisoria,
            CASE WHEN {CompletaODiaSql("sum(p.parte_provisoria)")}
                THEN (array_agg(p.meta_id ORDER BY p.parte_provisoria DESC, p.meta_ordem NULLS FIRST))[1]
            END AS meta_por_tipo_id
        FROM partes p
        GROUP BY p.desafio_id, p.usuario_id, p.dia, p.indice_semana
        """;

    /// <summary>
    /// Uma linha por pessoa e dia, com as somas e se o dia conta (ver <see cref="BuildDiasPorCondicao"/>).
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
