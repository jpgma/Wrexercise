namespace Wrexercise.Infrastructure.Database;

/// <summary>
/// Fragmento SQL canônico para contagens de dias elegíveis a um desafio: as atividades de um dia se somam,
/// e um dia com a soma igual ou maior que a duração mínima conta como uma sessão.
/// Ver docs/decisoes/0013-competicao-e-progresso.md e docs/decisoes/0040-meta-diaria.md.
/// </summary>
internal static class ContagensAtividadeDesafio
{
    internal const string DiaSql = "(a.started_at AT TIME ZONE g.fuso_horario)::date";

    internal const string IndiceSemanaSql =
        "((a.started_at AT TIME ZONE g.fuso_horario)::date - d.data_inicio) / 7";

    /// <summary>Dias em que a soma das atividades verificadas alcança o mínimo.</summary>
    internal const string VerificadasSql =
        "count(*) FILTER (WHERE dias.segundos_verificados >= dias.minimo)::int";

    /// <summary>Dias que ainda não alcançam o mínimo só com as verificadas, mas alcançam com as pendentes.</summary>
    internal const string PendentesSql = """
        count(*) FILTER (WHERE dias.segundos_verificados < dias.minimo
            AND dias.segundos_provisorios >= dias.minimo)::int
        """;

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
    /// Uma linha por pessoa e dia, com a soma das durações verificadas e das não rejeitadas.
    /// <paramref name="filtros"/> acrescenta condições <c>AND</c> ao <see cref="FromElegiveisSql"/>.
    /// </summary>
    private static string BuildDias(string filtros = "") => $"""
        SELECT a.usuario_id,
            {DiaSql} AS dia,
            {IndiceSemanaSql} AS indice_semana,
            d.duracao_minima_segundos AS minimo,
            coalesce(sum(a.duracao_segundos) FILTER (WHERE a.situacao_verificacao = 'Verificada'), 0)
                AS segundos_verificados,
            coalesce(sum(a.duracao_segundos) FILTER (WHERE a.situacao_verificacao <> 'Rejeitada'), 0)
                AS segundos_provisorios
        {FromElegiveisSql}
        {filtros}
        GROUP BY a.usuario_id, {DiaSql}, {IndiceSemanaSql}, d.duracao_minima_segundos
        """;

    internal static string BuildAgregadoPorUsuarioESemana() => $"""
        SELECT dias.usuario_id,
            dias.indice_semana,
            {VerificadasSql} AS verificadas,
            {PendentesSql} AS pendentes
        FROM ({BuildDias()}) dias
        WHERE dias.segundos_provisorios >= dias.minimo
        GROUP BY dias.usuario_id, dias.indice_semana
        """;

    /// <summary>
    /// Os dias da semana com atividade não rejeitada, com a soma das durações e se o dia conta para a meta
    /// (contando as pendentes). $1 = desafio_id, $2 e $3 = a semana, $4 = usuario_id.
    /// </summary>
    internal static string BuildDiasDaSemana() => $"""
        SELECT dias.dia, dias.segundos_provisorios::int, dias.segundos_provisorios >= dias.minimo
        FROM ({BuildDias(FiltroSemanaSql + " AND a.usuario_id = $4")}) dias
        WHERE dias.segundos_provisorios > 0
        ORDER BY dias.dia
        """;

    internal static string BuildAgregadoPorUsuarioSemanaAtual() => $"""
        SELECT dias.usuario_id,
            {VerificadasSql} AS verificadas,
            {PendentesSql} AS pendentes
        FROM ({BuildDias(FiltroSemanaSql)}) dias
        WHERE dias.segundos_provisorios >= dias.minimo
        GROUP BY dias.usuario_id
        """;
}
