namespace Wrexercise.Infrastructure.Database;

/// <summary>
/// Regra canônica de quem dorme num grupo: nenhuma atividade válida ali nas últimas 48 horas
/// (decisões 0057 e 0062). Vale para a pose na praça, para os revisores e para a Cutucada.
/// </summary>
internal static class MembrosDormindo
{
    /// <summary>Sem atividade no grupo por este tempo, o membro dorme.</summary>
    internal static readonly TimeSpan TempoParaDormir = TimeSpan.FromHours(48);

    /// <summary>O mesmo tempo, como intervalo do PostgreSQL.</summary>
    internal const string TempoParaDormirSql = "interval '48 hours'";

    /// <summary>
    /// Quando o membro <paramref name="membro"/> (alias de <c>membros_grupo</c>) "acordou" pela última vez:
    /// o início da última atividade deste grupo que não foi rejeitada nem anulada (as pendentes contam), ou
    /// a entrada no grupo, o que for mais recente. GREATEST ignora a falta de atividade.
    /// </summary>
    internal static string AcordadoEmSql(string membro = "m") => $"""
        GREATEST({membro}.joined_at, (
            SELECT max(a_sono.started_at)
            FROM atividades a_sono
            WHERE a_sono.grupo_id = {membro}.grupo_id
              AND a_sono.usuario_id = {membro}.usuario_id
              AND a_sono.situacao_verificacao NOT IN ('Rejeitada', 'Anulada')))
        """;

    /// <summary>Condição SQL: o membro <paramref name="membro"/> está acordado agora.</summary>
    internal static string AcordadoSql(string membro = "m") =>
        $"{AcordadoEmSql(membro)} > now() - {TempoParaDormirSql}";

    /// <summary>Condição SQL: o membro <paramref name="membro"/> dorme agora.</summary>
    internal static string DormindoSql(string membro = "m") =>
        $"{AcordadoEmSql(membro)} <= now() - {TempoParaDormirSql}";
}
