namespace Wrexercise.Features.Jogo;

/// <summary>
/// Um dia com atividade verificada num grupo, no instante em que a primeira atividade dele foi verificada.
/// </summary>
/// <param name="Pago">Se o dia ainda rende a base: falso quando todas as atividades dele foram anuladas.</param>
internal sealed record DiaVerificado(Guid GrupoId, DateOnly Dia, DateTimeOffset VerificadoEm, bool Pago = true);

/// <summary>Por que a pessoa ficou com FADIGA (decisão 0089).</summary>
internal enum MotivoFadiga
{
    /// <summary>Recusou a Provocação.</summary>
    Recusa,

    /// <summary>Aceitou e não cumpriu: o próximo treino falhou ou o prazo acabou depois do aceite.</summary>
    Falha,

    /// <summary>Deixou a carta expirar sem resposta.</summary>
    Expiracao,
}

/// <summary>Um momento em que uma Provocação recebida deu FADIGA.</summary>
/// <param name="JogadaId">A Jogada que causou, para mostrar o motivo ao grupo.</param>
internal sealed record GatilhoFadiga(DateTimeOffset Em, MotivoFadiga Motivo, Guid? JogadaId = null);

/// <param name="DiasComFadiga">Os dias pagos que renderam só metade da base.</param>
/// <param name="Restantes">Quantos dos próximos dias verificados ainda vão render metade (0 a <see cref="Fadiga.Teto"/>).</param>
/// <param name="Causa">Com FADIGA ativa: o último gatilho que deu ou somou cargas.</param>
internal sealed record ResultadoFadiga(int DiasComFadiga, int Restantes, GatilhoFadiga? Causa = null)
{
    // A causa só serve para mostrar ao grupo: dois resultados com as mesmas cargas valem o mesmo.
    public bool Equals(ResultadoFadiga? other) =>
        other is not null && DiasComFadiga == other.DiasComFadiga && Restantes == other.Restantes;

    public override int GetHashCode() => HashCode.Combine(DiasComFadiga, Restantes);
}

/// <summary>
/// A FADIGA (docs/gamificacao.md §5.4 e decisões 0059 e 0089), calculada na leitura. Recusar, deixar expirar ou não
/// cumprir uma Provocação recebida faz os próximos dias com atividade verificada renderem metade da base.
/// </summary>
/// <remarks>
/// Os eventos seguem o <c>verified_at</c> de cada dia, que é gravado uma vez e nunca muda. Um dia só fica com
/// FADIGA se foi verificado depois do gatilho, então nenhuma leitura anterior pagou a base inteira a ele:
/// a Energia já ganha não diminui.
/// </remarks>
internal static class Fadiga
{
    /// <summary>As cargas da recusa e da expiração sem resposta.</summary>
    public const int Cargas = 3;

    /// <summary>As cargas de quem aceitou e não cumpriu: tentar pesa menos que recusar (decisão 0089).</summary>
    public const int CargasFalha = 2;

    /// <summary>O máximo de cargas: cada recusa com FADIGA ativa soma 1, até aqui (decisão 0089).</summary>
    public const int Teto = 5;

    /// <summary>Recusas e expirações de antes da implantação não contam, para não reduzir dias já pagos.</summary>
    public static readonly DateTimeOffset Inicio = new(2026, 11, 13, 0, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>
    /// Desde quando valem as cargas por motivo, o acúmulo da recusa e a redenção (decisão 0089). Os eventos
    /// anteriores seguem a regra da decisão 0059: 3 cargas, sem acúmulo e sem redenção.
    /// </summary>
    public static readonly DateTimeOffset InicioRegras2 = new(2026, 11, 24, 0, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>O quanto um dia com FADIGA deixa de render: metade da base, com o resultado arredondado para baixo.</summary>
    public const int Desconto = CalculoEnergia.PorDia - CalculoEnergia.PorDia / 2;

    /// <summary>Compatível com a regra antiga: só os instantes, todos como recusa.</summary>
    public static ResultadoFadiga Calcular(IEnumerable<DateTimeOffset> gatilhos, IEnumerable<DiaVerificado> dias) =>
        Calcular(gatilhos.Select(g => new GatilhoFadiga(g, MotivoFadiga.Recusa)), dias, []);

    /// <param name="gatilhos">Quando cada Provocação recebida foi recusada, expirou ou falhou.</param>
    /// <param name="dias">
    /// Os dias com atividade verificada, inclusive os anulados depois: um dia anulado gasta a carga dele,
    /// para que ela não passe a outro dia já pago.
    /// </param>
    /// <param name="cumpridas">Quando cada Provocação recebida foi cumprida: cada uma tira 1 carga.</param>
    public static ResultadoFadiga Calcular(
        IEnumerable<GatilhoFadiga> gatilhos,
        IEnumerable<DiaVerificado> dias,
        IEnumerable<DateTimeOffset> cumpridas)
    {
        // No mesmo instante, o dia vem antes da redenção, que vem antes do gatilho: na dúvida, a pessoa não perde.
        var eventos = dias
            .Where(d => d.Dia >= CalculoEnergia.InicioEnergiaPorDia)
            .Select(d => (Em: d.VerificadoEm, Ordem: 0, Dia: (DiaVerificado?)d, Gatilho: (GatilhoFadiga?)null))
            .Concat(cumpridas
                .Where(c => c >= InicioRegras2)
                .Select(c => (Em: c, Ordem: 1, Dia: (DiaVerificado?)null, Gatilho: (GatilhoFadiga?)null)))
            .Concat(gatilhos
                .Where(g => g.Em >= Inicio)
                .Select(g => (Em: g.Em, Ordem: 2, Dia: (DiaVerificado?)null, Gatilho: (GatilhoFadiga?)g)))
            .OrderBy(e => e.Em)
            .ThenBy(e => e.Ordem);

        var restantes = 0;
        var diasComFadiga = 0;
        GatilhoFadiga? causa = null;
        foreach (var evento in eventos)
        {
            if (evento.Gatilho is { } gatilho)
            {
                var depois = Aplicar(restantes, gatilho);
                if (depois != restantes)
                {
                    restantes = depois;
                    causa = gatilho;
                }
            }
            else if (evento.Dia is { } dia)
            {
                if (restantes > 0)
                {
                    restantes--;
                    if (dia.Pago)
                    {
                        diasComFadiga++;
                    }
                }
            }
            else if (restantes > 0)
            {
                restantes--;
            }
        }

        return new ResultadoFadiga(diasComFadiga, restantes, restantes > 0 ? causa : null);
    }

    /// <summary>As cargas depois de um gatilho.</summary>
    private static int Aplicar(int restantes, GatilhoFadiga gatilho)
    {
        if (gatilho.Em < InicioRegras2)
        {
            return restantes == 0 ? Cargas : restantes;
        }

        if (restantes == 0)
        {
            return gatilho.Motivo == MotivoFadiga.Falha ? CargasFalha : Cargas;
        }

        // Só a recusa acumula: quem tentou e falhou, ou nem viu a carta, não piora o que já tem.
        return gatilho.Motivo == MotivoFadiga.Recusa ? Math.Min(Teto, restantes + 1) : restantes;
    }

    /// <summary>
    /// O instante e o motivo em que a Jogada deixou a pessoa com FADIGA, ou <c>null</c>. Só conta para quem recebeu.
    /// </summary>
    public static GatilhoFadiga? GetGatilho(JogadaCarregada jogada, Guid usuarioId)
    {
        if (jogada.DestinatarioId != usuarioId)
        {
            return null;
        }

        return jogada.Situacao switch
        {
            SituacaoJogada.Recusada when jogada.Estado.RespondidaEm is { } em =>
                new GatilhoFadiga(em, MotivoFadiga.Recusa, jogada.Id),
            // Sem resposta, ou aceita sem cumprir: a falha acontece no prazo.
            SituacaoJogada.Expirada => new GatilhoFadiga(
                jogada.Estado.ExpiraEm,
                jogada.Estado.Resposta == SituacaoJogada.RespostaAceita ? MotivoFadiga.Falha : MotivoFadiga.Expiracao,
                jogada.Id),
            // O próximo treino não cumpriu: a falha acontece quando ele é registrado ou o dia acaba (decisão 0075).
            SituacaoJogada.Falhou when jogada.FalhouEm is { } em => new GatilhoFadiga(em, MotivoFadiga.Falha, jogada.Id),
            _ => null,
        };
    }

    /// <summary>Quando a pessoa cumpriu esta Provocação recebida, ou <c>null</c>: a redenção (decisão 0089).</summary>
    public static DateTimeOffset? GetCumprida(JogadaCarregada jogada, Guid usuarioId) =>
        jogada.DestinatarioId == usuarioId && jogada.Situacao == SituacaoJogada.Concluida ? jogada.ConcluidaEm : null;

    /// <summary>A FADIGA de uma pessoa a partir das Jogadas dela e dos dias verificados.</summary>
    public static ResultadoFadiga Calcular(
        IEnumerable<JogadaCarregada> jogadas,
        IEnumerable<DiaVerificado> dias,
        Guid usuarioId)
    {
        var lista = jogadas.ToList();
        return Calcular(
            lista.Select(j => GetGatilho(j, usuarioId)).OfType<GatilhoFadiga>(),
            dias,
            lista.Select(j => GetCumprida(j, usuarioId)).OfType<DateTimeOffset>());
    }
}
