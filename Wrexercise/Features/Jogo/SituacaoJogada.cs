namespace Wrexercise.Features.Jogo;

internal static class SituacaoJogada
{
    public const string Enviada = "Enviada";
    public const string Recusada = "Recusada";
    public const string Aceita = "Aceita";
    public const string AguardandoVerificacao = "AguardandoVerificacao";
    public const string Concluida = "Concluida";
    public const string Expirada = "Expirada";

    public const string RespostaAceita = "Aceita";
    public const string RespostaRecusada = "Recusada";

    /// <summary>
    /// A situação calculada na leitura (docs/gamificacao.md §6.4).
    /// </summary>
    /// <param name="atividades">
    /// As atividades não rejeitadas do destinatário, no grupo da Jogada, com início entre o envio e o prazo.
    /// </param>
    public static string Calcular(JogadaEstado jogada, IEnumerable<AtividadeJogada> atividades, DateTimeOffset agora)
    {
        if (jogada.Resposta == RespostaRecusada)
        {
            return Recusada;
        }

        if (jogada.Resposta is null)
        {
            return agora < jogada.ExpiraEm ? Enviada : Expirada;
        }

        // Aceitar depois do prazo não é permitido; a regra fica aqui para que a leitura nunca dependa disso.
        if (jogada.RespondidaEm >= jogada.ExpiraEm)
        {
            return Expirada;
        }

        var lista = atividades.ToList();
        if (ObjetivoCarta.Cumpre(jogada, lista, apenasVerificadas: true))
        {
            return Concluida;
        }

        if (ObjetivoCarta.Cumpre(jogada, lista, apenasVerificadas: false))
        {
            return AguardandoVerificacao;
        }

        return agora < jogada.ExpiraEm ? Aceita : Expirada;
    }

    /// <summary>Ativa para os limites de envio: <c>AguardandoVerificacao</c> já libera a vaga.</summary>
    public static bool IsAtiva(string situacao) => situacao is Enviada or Aceita;

    /// <summary>A carta ainda está em jogo: não voltou para a Mão nem foi gasta.</summary>
    public static bool IsAberta(string situacao) => situacao is Enviada or Aceita or AguardandoVerificacao;
}

/// <summary>Uma Jogada como está gravada.</summary>
internal sealed record JogadaEstado(
    string Codigo,
    DateTimeOffset EnviadaEm,
    DateTimeOffset ExpiraEm,
    string? Resposta,
    DateTimeOffset? RespondidaEm,
    int? DuracaoAlvoSegundos,
    string? TipoEvitar);

/// <summary>Uma atividade candidata, com o início no fuso do grupo.</summary>
internal sealed record AtividadeJogada(DateTime InicioLocal, string Tipo, int DuracaoSegundos, bool Verificada);

/// <summary>Se as atividades de uma Jogada cumprem o objetivo da carta.</summary>
internal static class ObjetivoCarta
{
    /// <summary>
    /// +10 MIN e +20 MIN somam as atividades de um mesmo dia (decisão 0040); as outras cartas pedem uma atividade.
    /// </summary>
    /// <param name="apenasVerificadas">Só as verificadas contam; senão, também as pendentes.</param>
    public static bool Cumpre(JogadaEstado jogada, IEnumerable<AtividadeJogada> atividades, bool apenasVerificadas)
    {
        var queContam = atividades.Where(a => !apenasVerificadas || a.Verificada);
        if (jogada.Codigo is CodigoCarta.Mais10Min or CodigoCarta.Mais20Min)
        {
            return jogada.DuracaoAlvoSegundos is { } alvo
                && queContam
                    .GroupBy(a => DateOnly.FromDateTime(a.InicioLocal))
                    .Any(dia => dia.Sum(a => a.DuracaoSegundos) >= alvo);
        }

        return queContam.Any(a => CumpreSozinha(jogada, a));
    }

    /// <summary>Se uma atividade, sozinha, cumpre o objetivo.</summary>
    public static bool Cumpre(JogadaEstado jogada, AtividadeJogada atividade) =>
        Cumpre(jogada, [atividade], apenasVerificadas: false);

    private static bool CumpreSozinha(JogadaEstado jogada, AtividadeJogada atividade) => jogada.Codigo switch
    {
        // Sem atividade verificada no envio, não há tipo a evitar: qualquer tipo vale.
        CodigoCarta.Variedade => atividade.Tipo != jogada.TipoEvitar,
        CodigoCarta.FimDeSemana => atividade.InicioLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        CodigoCarta.AntesDas8 => atividade.InicioLocal.Hour < 8,
        _ => false,
    };
}
