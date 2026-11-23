namespace Wrexercise.Features.Jogo;

internal static class SituacaoJogada
{
    public const string Enviada = "Enviada";
    public const string Recusada = "Recusada";
    public const string Aceita = "Aceita";
    public const string AguardandoVerificacao = "AguardandoVerificacao";
    public const string Concluida = "Concluida";
    public const string Expirada = "Expirada";

    /// <summary>O próximo treino depois do aceite não cumpriu o objetivo (decisão 0075).</summary>
    public const string Falhou = "Falhou";

    public const string RespostaAceita = "Aceita";
    public const string RespostaRecusada = "Recusada";

    /// <summary>A situação calculada na leitura (docs/gamificacao.md §6.4).</summary>
    /// <param name="atividades">
    /// As atividades não rejeitadas nem anuladas do destinatário, no grupo da Jogada, com início entre o envio e o
    /// prazo e na ordem em que foram registradas.
    /// </param>
    /// <param name="agoraLocal">O instante atual no fuso do grupo; sem ele, o horário de <paramref name="agora"/>.</param>
    public static string Calcular(
        JogadaEstado jogada,
        IEnumerable<AtividadeJogada> atividades,
        DateTimeOffset agora,
        DateTime? agoraLocal = null) =>
        Avaliar(jogada, atividades, agora, agoraLocal).Situacao;

    /// <summary>A situação e, se a Jogada falhou, o instante da falha, que inicia a FADIGA.</summary>
    public static (string Situacao, DateTimeOffset? FalhouEm) Avaliar(
        JogadaEstado jogada,
        IEnumerable<AtividadeJogada> atividades,
        DateTimeOffset agora,
        DateTime? agoraLocal = null)
    {
        if (jogada.Resposta == RespostaRecusada)
        {
            return (Recusada, null);
        }

        if (jogada.Resposta is null)
        {
            return (agora < jogada.ExpiraEm ? Enviada : Expirada, null);
        }

        // Aceitar depois do prazo não é permitido; a regra fica aqui para que a leitura nunca dependa disso.
        if (jogada.RespondidaEm >= jogada.ExpiraEm)
        {
            return (Expirada, null);
        }

        var lista = atividades.ToList();
        if (ObjetivoCarta.Cumpre(jogada, lista, apenasVerificadas: true))
        {
            return (Concluida, null);
        }

        if (ObjetivoCarta.Cumpre(jogada, lista, apenasVerificadas: false))
        {
            return (AguardandoVerificacao, null);
        }

        if (ObjetivoCarta.GetFalha(jogada, lista, agora, agoraLocal ?? agora.DateTime) is { } falhouEm)
        {
            return (Falhou, falhouEm);
        }

        return (agora < jogada.ExpiraEm ? Aceita : Expirada, null);
    }

    /// <summary>Ativa para os limites de envio: <c>AguardandoVerificacao</c> já libera a vaga.</summary>
    public static bool IsAtiva(string situacao) => situacao is Enviada or Aceita;

    /// <summary>A carta ainda está em jogo: não voltou para a Mão nem foi gasta.</summary>
    public static bool IsAberta(string situacao) => situacao is Enviada or Aceita or AguardandoVerificacao;
}

/// <summary>Uma Jogada como está gravada.</summary>
/// <param name="CodigoFusao">A segunda Provocação de uma Fusão, ou <c>null</c> (decisão 0070).</param>
internal sealed record JogadaEstado(
    string Codigo,
    DateTimeOffset EnviadaEm,
    DateTimeOffset ExpiraEm,
    string? Resposta,
    DateTimeOffset? RespondidaEm,
    int? DuracaoAlvoSegundos,
    string? TipoEvitar,
    string? CodigoFusao = null);

/// <summary>Uma atividade candidata, com o início no fuso do grupo.</summary>
/// <param name="RegistradaEm">Quando a atividade foi registrada: define qual é o próximo treino (decisão 0075).</param>
/// <param name="AntesDoAceite">
/// Começou depois que a carta chegou e antes do aceite (ou a carta ainda espera resposta): conta se cumpre o
/// objetivo e nunca faz a Jogada falhar (decisão 0076).
/// </param>
internal sealed record AtividadeJogada(
    DateTime InicioLocal,
    string Tipo,
    int DuracaoSegundos,
    bool Verificada,
    DateTimeOffset RegistradaEm = default,
    bool AntesDoAceite = false);

/// <summary>Se as atividades de uma Jogada cumprem o objetivo da carta.</summary>
internal static class ObjetivoCarta
{
    /// <summary>
    /// Desde quando vale a regra do próximo treino e a ANTES DAS 8 pelo fim do treino (decisão 0075). As Jogadas
    /// enviadas antes seguem a regra antiga até o fim: qualquer treino no prazo conta, e a ANTES DAS 8 olha o início.
    /// </summary>
    public static readonly DateTimeOffset InicioProximoTreino = new(2026, 11, 20, 13, 0, 0, TimeSpan.FromHours(-3));

    public static bool IsProximoTreino(JogadaEstado jogada) => jogada.EnviadaEm >= InicioProximoTreino;

    /// <summary>
    /// +10 MIN e +20 MIN somam as atividades de um mesmo dia (decisão 0040); as outras cartas pedem uma atividade.
    /// Numa Fusão, um mesmo treino cumpre os dois objetivos: a duração é a dessa atividade, sem somar o dia
    /// (decisão 0070). Na regra do próximo treino, só contam a vez e os treinos de antes do aceite
    /// (<see cref="GetVez"/>).
    /// </summary>
    /// <param name="apenasVerificadas">Só as verificadas contam; senão, também as pendentes.</param>
    public static bool Cumpre(JogadaEstado jogada, IEnumerable<AtividadeJogada> atividades, bool apenasVerificadas)
    {
        var queContam = GetVez(jogada, atividades).Where(a => !apenasVerificadas || a.Verificada);
        if (jogada.CodigoFusao is { } fusao)
        {
            return queContam.Any(a => CumpreSozinha(jogada.Codigo, jogada, a) && CumpreSozinha(fusao, jogada, a));
        }

        if (SomaODia(jogada))
        {
            return jogada.DuracaoAlvoSegundos is { } alvo
                && queContam
                    .GroupBy(a => DateOnly.FromDateTime(a.InicioLocal))
                    .Any(dia => dia.Sum(a => a.DuracaoSegundos) >= alvo);
        }

        return queContam.Any(a => CumpreSozinha(jogada.Codigo, jogada, a));
    }

    /// <summary>Se uma atividade, sozinha, cumpre o objetivo.</summary>
    public static bool Cumpre(JogadaEstado jogada, AtividadeJogada atividade) =>
        Cumpre(jogada, [atividade], apenasVerificadas: false);

    /// <summary>
    /// As atividades que contam para a Jogada: os treinos de antes do aceite (decisão 0076) mais a vez do próximo
    /// treino (<see cref="GetProximoTreino"/>). Na regra antiga, todas as atividades.
    /// </summary>
    public static IReadOnlyList<AtividadeJogada> GetVez(JogadaEstado jogada, IEnumerable<AtividadeJogada> atividades)
    {
        var lista = atividades.ToList();
        if (!IsProximoTreino(jogada))
        {
            return lista;
        }

        return [.. lista.Where(a => a.AntesDoAceite), .. GetProximoTreino(jogada, lista)];
    }

    /// <summary>
    /// O próximo treino (decisão 0075): o primeiro registrado entre os que começaram depois do aceite, dentro da
    /// janela da carta (decisão 0086), ou, em +10 MIN e +20 MIN fora da Fusão, todos os de depois do aceite no dia
    /// dele. Só ele pode fazer a Jogada falhar.
    /// </summary>
    public static IReadOnlyList<AtividadeJogada> GetProximoTreino(
        JogadaEstado jogada,
        IEnumerable<AtividadeJogada> atividades)
    {
        var depois = atividades.Where(a => !a.AntesDoAceite && IsNaJanela(jogada, a)).ToList();
        if (depois.Count == 0)
        {
            return [];
        }

        var primeira = depois.OrderBy(a => a.RegistradaEm).First();
        return SomaODia(jogada)
            ? depois.Where(a => a.InicioLocal.Date == primeira.InicioLocal.Date).ToList()
            : [primeira];
    }

    /// <summary>
    /// Se um treino já feito cumpre o objetivo de uma carta que ainda espera resposta: ao aceitar, ela é cumprida
    /// na hora (decisão 0076).
    /// </summary>
    public static bool JaCumpre(JogadaEstado jogada, IEnumerable<AtividadeJogada> atividades) =>
        Cumpre(jogada, atividades.Select(a => a with { AntesDoAceite = true }), apenasVerificadas: false);

    /// <summary>
    /// Quando o próximo treino deixou de cumprir o objetivo, ou <c>null</c>. Um treino só falha assim que é
    /// registrado; nas cartas de duração, o dia falha quando acaba sem a soma, se isso acontece antes do prazo.
    /// Um treino de antes do aceite nunca faz falhar, mas o que cumpre salva a Jogada (decisão 0076).
    /// </summary>
    public static DateTimeOffset? GetFalha(
        JogadaEstado jogada,
        IReadOnlyList<AtividadeJogada> atividades,
        DateTimeOffset agora,
        DateTime agoraLocal)
    {
        var vez = IsProximoTreino(jogada) ? GetProximoTreino(jogada, atividades) : [];
        if (vez.Count == 0 || Cumpre(jogada, atividades, apenasVerificadas: false))
        {
            return null;
        }

        if (!SomaODia(jogada))
        {
            return vez[0].RegistradaEm;
        }

        var fimDoDia = vez[0].InicioLocal.Date.AddDays(1);
        if (agoraLocal < fimDoDia)
        {
            return null;
        }

        // O fim do dia no fuso do grupo, com o deslocamento de agora.
        var deslocamento = TimeSpan.FromMinutes(Math.Round((agoraLocal - agora.UtcDateTime).TotalMinutes));
        var falhouEm = new DateTimeOffset(DateTime.SpecifyKind(fimDoDia, DateTimeKind.Unspecified), deslocamento);
        return falhouEm < jogada.ExpiraEm ? falhouEm : null;
    }

    /// <summary>
    /// A janela da carta (decisão 0086): se o momento do treino permite cumprir o objetivo. Um treino fora dela não
    /// é o próximo treino: não conta nem faz falhar. A ANTES DAS 8 pede um treino que comece antes das 08:00, e a
    /// FIM DE SEMANA, um treino no sábado ou no domingo. As outras cartas aceitam qualquer treino. Na Fusão, o treino
    /// precisa estar na janela das duas cartas.
    /// </summary>
    public static bool IsNaJanela(JogadaEstado jogada, AtividadeJogada atividade) =>
        IsNaJanela(jogada.Codigo, atividade)
        && (jogada.CodigoFusao is not { } fusao || IsNaJanela(fusao, atividade));

    private static bool IsNaJanela(string codigo, AtividadeJogada atividade) => codigo switch
    {
        CodigoCarta.AntesDas8 => atividade.InicioLocal.Hour < 8,
        CodigoCarta.FimDeSemana => atividade.InicioLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        _ => true,
    };

    private static bool SomaODia(JogadaEstado jogada) =>
        jogada.CodigoFusao is null && jogada.Codigo is CodigoCarta.Mais10Min or CodigoCarta.Mais20Min;

    private static bool CumpreSozinha(string codigo, JogadaEstado jogada, AtividadeJogada atividade) => codigo switch
    {
        // Só na Fusão: o alvo já soma os minutos das duas cartas.
        CodigoCarta.Mais10Min or CodigoCarta.Mais20Min =>
            jogada.DuracaoAlvoSegundos is { } alvo && atividade.DuracaoSegundos >= alvo,
        // Sem treino de referência no envio (decisão 0071), não há tipo a evitar: qualquer tipo vale.
        CodigoCarta.Variedade => atividade.Tipo != jogada.TipoEvitar,
        CodigoCarta.FimDeSemana => atividade.InicioLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        // Decisão 0075: o treino termina até as 08:00 do dia em que começou. Antes, valia o início.
        CodigoCarta.AntesDas8 => IsProximoTreino(jogada)
            ? atividade.InicioLocal.AddSeconds(atividade.DuracaoSegundos) <= atividade.InicioLocal.Date.AddHours(8)
            : atividade.InicioLocal.Hour < 8,
        _ => false,
    };
}
