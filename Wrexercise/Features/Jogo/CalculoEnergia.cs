using Wrexercise.Features.Desafios.Contracts;

namespace Wrexercise.Features.Jogo;

/// <summary>Uma atividade não rejeitada da pessoa, com o início no fuso do grupo dela.</summary>
internal sealed record AtividadeEnergia(DateTime InicioLocal, string Tipo, bool Verificada);

/// <summary>Um desafio já iniciado de um grupo de que a pessoa é ou foi membro.</summary>
/// <param name="SemanasCumpridas">Índices (a partir de 0) das semanas em que a pessoa cumpriu a meta.</param>
internal sealed record DesafioEnergia(
    DateOnly DataInicio,
    DateOnly DataFim,
    DateOnly EntradaLocal,
    bool Encerrado,
    IReadOnlyList<int> SemanasCumpridas);

/// <summary>Uma Jogada concluída de que a pessoa participou.</summary>
/// <param name="Preco">O preço pago pela Provocação.</param>
/// <param name="ComoDestinatario">Se a pessoa cumpriu o objetivo; senão, foi quem enviou.</param>
/// <param name="Aposta">Se quem recebeu aceitou com a APOSTA.</param>
internal sealed record RecompensaJogada(int Preco, bool ComoDestinatario, bool Aposta = false);

internal sealed record EnergiaGanha(int Atividades, int Bonus, int Metas, int Sequencias, int Desafios, int Jogadas = 0)
{
    public int Total => Atividades + Bonus + Metas + Sequencias + Desafios + Jogadas;
}

/// <summary>
/// Energia ganha, calculada na leitura (docs/gamificacao.md §5 e decisões 0033 e 0034).
/// Só cresce: créditos dependem de atividades <c>Verificada</c>, que é final, e os qualificadores
/// seguem a ordem de registro, então uma atividade nova nunca toma o bônus de outra.
/// </summary>
internal static class CalculoEnergia
{
    public const int PorAtividade = 10;
    public const int PorMeta = 20;
    public const int PorDesafioConcluido = 75;
    public const int TetoBonus = 10;

    public const int BonusAntesDas6 = 5;
    public const int BonusAntesDas8 = 3;
    public const int BonusFimDeSemana = 3;
    public const int BonusPrimeiraDaSemana = 3;
    public const int BonusDiaNovo = 3;
    public const int BonusTipoNovo = 5;

    private static readonly (int Semanas, int Energia)[] MarcosSequencia = [(4, 25), (8, 50), (12, 100)];

    /// <param name="atividades">As atividades não rejeitadas de todos os grupos, em ordem de registro.</param>
    /// <param name="jogadas">As Jogadas concluídas de que a pessoa participou.</param>
    public static EnergiaGanha Calcular(
        IReadOnlyList<AtividadeEnergia> atividades,
        IReadOnlyList<DesafioEnergia> desafios,
        IReadOnlyList<RecompensaJogada>? jogadas = null)
    {
        var melhorSequencia = desafios
            .Select(d => CalculoSemanaDesafio.GetMelhorSequencia(d.SemanasCumpridas))
            .DefaultIfEmpty(0)
            .Max();

        return new EnergiaGanha(
            atividades.Count(a => a.Verificada) * PorAtividade,
            CalculateBonus(atividades),
            CountSemanasCumpridas(desafios) * PorMeta,
            MarcosSequencia.Where(m => melhorSequencia >= m.Semanas).Sum(m => m.Energia),
            desafios.Count(IsConcluido) * PorDesafioConcluido,
            (jogadas ?? []).Sum(GetRecompensa));
    }

    /// <summary>
    /// Quem cumpriu ganha 1 × o preço, ou 2 × com a APOSTA; quem enviou, metade, arredondada para baixo
    /// (decisões 0033 e 0035).
    /// </summary>
    public static int GetRecompensa(RecompensaJogada jogada) => jogada switch
    {
        { ComoDestinatario: true, Aposta: true } => jogada.Preco * 2,
        { ComoDestinatario: true } => jogada.Preco,
        _ => jogada.Preco / 2,
    };

    /// <summary>
    /// Toda atividade não rejeitada ocupa a sua posição, mas só as verificadas recebem o crédito.
    /// Assim, se uma pendente for rejeitada, a seguinte passa a ganhar o bônus.
    /// </summary>
    private static int CalculateBonus(IReadOnlyList<AtividadeEnergia> atividades)
    {
        var semanas = new HashSet<DateOnly>();
        var dias = new HashSet<DateOnly>();
        var diasComBonusCedo = new HashSet<DateOnly>();
        var diasComBonusFimDeSemana = new HashSet<DateOnly>();
        var tiposPorSemana = new Dictionary<DateOnly, HashSet<string>>();

        var total = 0;
        foreach (var atividade in atividades)
        {
            var dia = DateOnly.FromDateTime(atividade.InicioLocal);
            var semana = GetSegunda(dia);
            var bonus = 0;

            if (semanas.Add(semana))
            {
                bonus += BonusPrimeiraDaSemana;
            }

            if (dias.Add(dia))
            {
                bonus += BonusDiaNovo;
            }

            // O tipo da primeira atividade da semana só é registrado: o bônus é para o segundo tipo em diante.
            if (!tiposPorSemana.TryGetValue(semana, out var tipos))
            {
                tiposPorSemana[semana] = [atividade.Tipo];
            }
            else if (tipos.Add(atividade.Tipo))
            {
                bonus += BonusTipoNovo;
            }

            var hora = atividade.InicioLocal.Hour;
            if (hora < 8 && diasComBonusCedo.Add(dia))
            {
                bonus += hora < 6 ? BonusAntesDas6 : BonusAntesDas8;
            }

            if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && diasComBonusFimDeSemana.Add(dia))
            {
                bonus += BonusFimDeSemana;
            }

            if (atividade.Verificada)
            {
                total += Math.Min(bonus, TetoBonus);
            }
        }

        return total;
    }

    /// <summary>A meta paga uma vez por semana, mesmo cumprida em vários grupos ou desafios.</summary>
    private static int CountSemanasCumpridas(IReadOnlyList<DesafioEnergia> desafios) =>
        desafios
            .SelectMany(d => d.SemanasCumpridas.Select(indice => d.DataInicio.AddDays(indice * 7)))
            .Distinct()
            .Count();

    /// <summary>Encerrado, com todas as semanas que contavam para a pessoa cumpridas.</summary>
    private static bool IsConcluido(DesafioEnergia desafio)
    {
        if (!desafio.Encerrado)
        {
            return false;
        }

        var cumpridas = desafio.SemanasCumpridas.ToHashSet();
        var contavam = 0;
        for (var indice = 0; indice < CalculoSemanaDesafio.CountSemanas(desafio.DataInicio, desafio.DataFim); indice++)
        {
            if (!CalculoSemanaDesafio.ParticipaDaSemana(desafio.DataInicio.AddDays(indice * 7), desafio.EntradaLocal))
            {
                continue;
            }

            if (!cumpridas.Contains(indice))
            {
                return false;
            }

            contavam++;
        }

        return contavam > 0;
    }

    private static DateOnly GetSegunda(DateOnly dia) =>
        dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
}
