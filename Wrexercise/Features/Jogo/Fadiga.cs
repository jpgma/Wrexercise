namespace Wrexercise.Features.Jogo;

/// <summary>
/// Um dia com atividade verificada num grupo, no instante em que a primeira atividade dele foi verificada.
/// </summary>
/// <param name="Pago">Se o dia ainda rende a base: falso quando todas as atividades dele foram anuladas.</param>
internal sealed record DiaVerificado(Guid GrupoId, DateOnly Dia, DateTimeOffset VerificadoEm, bool Pago = true);

/// <param name="DiasComFadiga">Os dias pagos que renderam só metade da base.</param>
/// <param name="Restantes">Quantos dos próximos dias verificados ainda vão render metade (0 a 3).</param>
internal sealed record ResultadoFadiga(int DiasComFadiga, int Restantes);

/// <summary>
/// A FADIGA (docs/gamificacao.md §5.4 e decisão 0059), calculada na leitura. Recusar, deixar expirar ou falhar
/// uma Provocação recebida faz os próximos 3 dias com atividade verificada renderem metade da base.
/// Não acumula nem renova: um gatilho com a FADIGA ativa não muda nada.
/// </summary>
/// <remarks>
/// Os eventos seguem o <c>verified_at</c> de cada dia, que é gravado uma vez e nunca muda. Um dia só fica com
/// FADIGA se foi verificado depois do gatilho, então nenhuma leitura anterior pagou a base inteira a ele:
/// a Energia já ganha não diminui.
/// </remarks>
internal static class Fadiga
{
    public const int Cargas = 3;

    /// <summary>Recusas e expirações de antes da implantação não contam, para não reduzir dias já pagos.</summary>
    public static readonly DateTimeOffset Inicio = new(2026, 11, 13, 0, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>O quanto um dia com FADIGA deixa de render: metade da base, com o resultado arredondado para baixo.</summary>
    public const int Desconto = CalculoEnergia.PorDia - CalculoEnergia.PorDia / 2;

    /// <param name="gatilhos">Quando cada Provocação recebida foi recusada, expirou ou falhou.</param>
    /// <param name="dias">
    /// Os dias com atividade verificada, inclusive os anulados depois: um dia anulado gasta a carga dele,
    /// para que ela não passe a outro dia já pago.
    /// </param>
    public static ResultadoFadiga Calcular(IEnumerable<DateTimeOffset> gatilhos, IEnumerable<DiaVerificado> dias)
    {
        // Os dias vêm antes do gatilho no mesmo instante: na dúvida, a pessoa não perde.
        var eventos = gatilhos
            .Where(g => g >= Inicio)
            .Select(g => (Em: g, Ordem: 1, Dia: (DiaVerificado?)null))
            .Concat(dias
                .Where(d => d.Dia >= CalculoEnergia.InicioEnergiaPorDia)
                .Select(d => (Em: d.VerificadoEm, Ordem: 0, Dia: (DiaVerificado?)d)))
            .OrderBy(e => e.Em)
            .ThenBy(e => e.Ordem);

        var restantes = 0;
        var diasComFadiga = 0;
        foreach (var evento in eventos)
        {
            if (evento.Dia is null)
            {
                if (restantes == 0)
                {
                    restantes = Cargas;
                }
            }
            else if (restantes > 0)
            {
                restantes--;
                if (evento.Dia.Pago)
                {
                    diasComFadiga++;
                }
            }
        }

        return new ResultadoFadiga(diasComFadiga, restantes);
    }

    /// <summary>
    /// O instante em que a Jogada deixou a pessoa com FADIGA, ou <c>null</c>. Só conta para quem recebeu.
    /// </summary>
    public static DateTimeOffset? GetGatilho(JogadaCarregada jogada, Guid usuarioId)
    {
        if (jogada.DestinatarioId != usuarioId)
        {
            return null;
        }

        return jogada.Situacao switch
        {
            SituacaoJogada.Recusada => jogada.Estado.RespondidaEm,
            // Sem resposta ou aceita sem cumprir: a falha acontece no prazo.
            SituacaoJogada.Expirada => jogada.Estado.ExpiraEm,
            _ => null,
        };
    }
}
