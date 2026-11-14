using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Progresso.Contracts;

namespace Wrexercise.Features.Jogo;

/// <summary>Uma atividade não rejeitada da pessoa, com o início no fuso do grupo dela.</summary>
internal sealed record AtividadeEnergia(DateTime InicioLocal, string Tipo, bool Verificada, Guid GrupoId = default);

/// <summary>Uma Jogada concluída de que a pessoa participou.</summary>
/// <param name="Preco">O preço pago pela Provocação.</param>
/// <param name="ComoDestinatario">Se a pessoa cumpriu o objetivo; senão, foi quem enviou.</param>
/// <param name="Aposta">Se quem recebeu aceitou com a APOSTA.</param>
internal sealed record RecompensaJogada(int Preco, bool ComoDestinatario, bool Aposta = false);

/// <param name="Fadiga">O que a FADIGA deixou de render, como valor negativo (decisão 0059).</param>
internal sealed record EnergiaGanha(
    int Atividades, int Bonus, int Metas, int Sequencias, int Desafios, int Jogadas = 0, int Rivais = 0, int Fadiga = 0)
{
    public int Total => Atividades + Bonus + Metas + Sequencias + Desafios + Jogadas + Rivais + Fadiga;
}

/// <summary>
/// Energia ganha, calculada na leitura (docs/gamificacao.md §5 e decisões 0033 e 0034).
/// Só cresce: créditos dependem de atividades <c>Verificada</c>, que é final, e os qualificadores
/// seguem a ordem de registro, então uma atividade nova nunca toma o bônus de outra. A FADIGA só reduz dias
/// verificados depois dela (<see cref="Fadiga"/>).
/// </summary>
internal static class CalculoEnergia
{
    /// <summary>Por dia com atividade verificada em cada grupo; antes de <see cref="InicioEnergiaPorDia"/>, por atividade.</summary>
    public const int PorDia = 10;
    public const int PorMeta = 20;
    public const int PorDesafioConcluido = 75;
    public const int TetoBonus = 10;

    public const int BonusAntesDas6 = 5;
    public const int BonusAntesDas8 = 3;
    public const int BonusFimDeSemana = 3;
    public const int BonusPrimeiraDaSemana = 3;
    public const int BonusDiaNovo = 3;
    public const int BonusTipoNovo = 5;

    /// <summary>
    /// A partir deste dia, a base é por dia, não por atividade (decisão 0040). Antes dele, cada atividade
    /// continua com a sua base, para que a Energia já ganha nunca diminua.
    /// </summary>
    public static readonly DateOnly InicioEnergiaPorDia = new(2026, 11, 9);

    private static readonly (int Semanas, int Energia)[] MarcosSequencia = [(4, 25), (8, 50), (12, 100)];

    /// <param name="atividades">As atividades não rejeitadas de todos os grupos, em ordem de registro.</param>
    /// <param name="jogadas">As Jogadas concluídas de que a pessoa participou.</param>
    /// <param name="rivaisVencidos">
    /// Quantas vezes a pessoa venceu o Rival que escolheu. Cada grupo conta à parte (decisão 0038).
    /// </param>
    /// <param name="diasComFadiga">Os dias pagos que rendem só metade da base (<see cref="Fadiga"/>).</param>
    public static EnergiaGanha Calcular(
        IReadOnlyList<AtividadeEnergia> atividades,
        IReadOnlyList<DesafioDaPessoa> desafios,
        IReadOnlyList<RecompensaJogada>? jogadas = null,
        int rivaisVencidos = 0,
        int diasComFadiga = 0)
    {
        var melhorSequencia = desafios
            .Select(d => CalculoSemanaDesafio.GetMelhorSequencia(d.SemanasCumpridas))
            .DefaultIfEmpty(0)
            .Max();

        return new EnergiaGanha(
            CountBase(atividades) * PorDia,
            CalculateBonus(atividades),
            CountSemanasCumpridas(desafios) * PorMeta,
            MarcosSequencia.Where(m => melhorSequencia >= m.Semanas).Sum(m => m.Energia),
            desafios.Count(d => d.IsConcluido()) * PorDesafioConcluido,
            (jogadas ?? []).Sum(GetRecompensa),
            rivaisVencidos * ResultadoRival.Energia,
            -diasComFadiga * Fadiga.Desconto);
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

    /// <summary>
    /// Uma base por dia com atividade verificada em cada grupo: dividir um treino não rende mais.
    /// Atividades de antes de <see cref="InicioEnergiaPorDia"/> contam uma a uma.
    /// </summary>
    private static int CountBase(IReadOnlyList<AtividadeEnergia> atividades)
    {
        var verificadas = atividades.Where(a => a.Verificada).ToList();
        var antigas = verificadas.Count(a => DateOnly.FromDateTime(a.InicioLocal) < InicioEnergiaPorDia);
        var dias = verificadas
            .Where(a => DateOnly.FromDateTime(a.InicioLocal) >= InicioEnergiaPorDia)
            .Select(a => (a.GrupoId, DateOnly.FromDateTime(a.InicioLocal)))
            .Distinct()
            .Count();
        return antigas + dias;
    }

    /// <summary>A meta paga uma vez por semana, mesmo cumprida em vários grupos ou desafios.</summary>
    private static int CountSemanasCumpridas(IReadOnlyList<DesafioDaPessoa> desafios) =>
        desafios
            .SelectMany(d => d.SemanasCumpridas.Select(indice => d.DataInicio.AddDays(indice * 7)))
            .Distinct()
            .Count();

    private static DateOnly GetSegunda(DateOnly dia) =>
        dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
}
