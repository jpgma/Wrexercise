using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Progresso.Contracts;

namespace Wrexercise.Features.Jogo;

/// <summary>Uma atividade não rejeitada da pessoa, com o início no fuso do grupo dela.</summary>
/// <param name="AoVivo">Começou pelo "Começar treino" (decisão 0078).</param>
/// <param name="Parceria">Ao vivo junto com um colega do grupo, com treino verificado, por 15 minutos ou mais.</param>
internal sealed record AtividadeEnergia(
    DateTime InicioLocal,
    string Tipo,
    bool Verificada,
    Guid GrupoId = default,
    bool AoVivo = false,
    bool Parceria = false);

/// <summary>Uma Jogada concluída de que a pessoa participou.</summary>
/// <param name="Preco">O preço pago pela Provocação; numa Fusão, a soma das duas.</param>
/// <param name="ComoDestinatario">Se a pessoa cumpriu o objetivo; senão, foi quem enviou.</param>
/// <param name="Aposta">Se quem recebeu aceitou com a APOSTA.</param>
/// <param name="Fusao">Se a Jogada juntou duas Provocações (decisão 0070).</param>
internal sealed record RecompensaJogada(int Preco, bool ComoDestinatario, bool Aposta = false, bool Fusao = false);

/// <param name="Fadiga">O que a FADIGA deixou de render, como valor negativo (decisão 0059).</param>
/// <param name="Despertador">O que rendeu acordar colegas com a Cutucada (decisão 0063).</param>
/// <param name="Devolucoes">Metade do preço das cartas enviadas e não cumpridas (decisão 0087).</param>
internal sealed record EnergiaGanha(
    int Atividades,
    int Bonus,
    int Metas,
    int Sequencias,
    int Desafios,
    int Jogadas = 0,
    int Rivais = 0,
    int Fadiga = 0,
    int Despertador = 0,
    int Devolucoes = 0)
{
    public int Total =>
        Atividades + Bonus + Metas + Sequencias + Desafios + Jogadas + Rivais + Fadiga + Despertador + Devolucoes;
}

/// <summary>
/// Energia ganha, calculada na leitura (docs/gamificacao.md §5 e decisões 0033 e 0034).
/// Só cresce: créditos dependem de atividades <c>Verificada</c>, que é final, e os qualificadores
/// seguem a ordem de registro, então uma atividade nova nunca toma o bônus de outra. A FADIGA só reduz dias
/// verificados depois dela (<see cref="Fadiga"/>). A exceção é a correção da duração por um administrador
/// (decisão 0068): se o dia deixa de alcançar a meta, a Energia que dependia dele sai, e o saldo pode ficar negativo.
/// </summary>
internal static class CalculoEnergia
{
    /// <summary>Por dia com atividade verificada em cada grupo; antes de <see cref="InicioEnergiaPorDia"/>, por atividade.</summary>
    public const int PorDia = 10;
    public const int PorMeta = 20;
    public const int PorDesafioConcluido = 75;
    public const int TetoBonus = 10;

    /// <summary>Para quem cutucou, por colega que voltou a treinar (decisão 0063).</summary>
    public const int PorDespertar = 5;

    public const int BonusAntesDas6 = 5;
    public const int BonusAntesDas8 = 3;
    public const int BonusFimDeSemana = 3;
    public const int BonusPrimeiraDaSemana = 3;
    public const int BonusDiaNovo = 3;
    public const int BonusTipoNovo = 5;

    /// <summary>Treino registrado ao vivo, com "Começar treino" e "Terminar treino" (decisão 0078).</summary>
    public const int BonusAoVivo = 3;

    /// <summary>Treino ao vivo junto com um colega do grupo (decisão 0078).</summary>
    public const int BonusParceria = 3;

    /// <summary>Quanto dois treinos ao vivo precisam acontecer juntos para render Parceria.</summary>
    public static readonly TimeSpan SobreposicaoParceria = TimeSpan.FromMinutes(15);

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
    /// <param name="despertares">Quantos colegas cutucados voltaram a treinar (<see cref="Despertador"/>).</param>
    /// <param name="devolucoes">A soma das devoluções das cartas enviadas e não cumpridas (<see cref="GetDevolucao"/>).</param>
    public static EnergiaGanha Calcular(
        IReadOnlyList<AtividadeEnergia> atividades,
        IReadOnlyList<DesafioDaPessoa> desafios,
        IReadOnlyList<RecompensaJogada>? jogadas = null,
        int rivaisVencidos = 0,
        int diasComFadiga = 0,
        int despertares = 0,
        int devolucoes = 0)
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
            -diasComFadiga * Fadiga.Desconto,
            despertares * PorDespertar,
            devolucoes);
    }

    /// <summary>
    /// Quem enviou uma carta recusada, não cumprida ou expirada recebe metade do preço de volta, arredondada para
    /// baixo (decisão 0087). Na Fusão, metade da soma das duas Provocações.
    /// </summary>
    public static int GetDevolucao(int preco) => preco / 2;

    /// <summary>
    /// Quem cumpriu ganha 1 × o preço, ou 2 × com a APOSTA; quem enviou recebe o preço de volta (decisão 0074).
    /// Numa Fusão, só quem cumpriu ganha o bônus sobre a soma, que a APOSTA não dobra (decisão 0070).
    /// </summary>
    public static int GetRecompensa(RecompensaJogada jogada)
    {
        var bonus = jogada.Fusao ? jogada.Preco * CatalogoCartas.BonusFusaoPorCento / 100 : 0;
        return jogada switch
        {
            { ComoDestinatario: true, Aposta: true } => (jogada.Preco * 2) + bonus,
            { ComoDestinatario: true } => jogada.Preco + bonus,
            _ => jogada.Preco,
        };
    }

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

            // Por atividade, não por dia: não ocupam posição, então nenhuma toma o bônus de outra.
            if (atividade.AoVivo)
            {
                bonus += BonusAoVivo;
            }

            if (atividade.Parceria)
            {
                bonus += BonusParceria;
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

    /// <summary>
    /// A meta paga uma vez por semana, mesmo cumprida em vários grupos ou desafios. A semana de um desafio
    /// cancelado (decisão 0088) só paga se não cruza uma semana já paga: um desafio criado depois do
    /// cancelamento, começando em outro dia da semana, não paga os mesmos dias duas vezes.
    /// </summary>
    private static int CountSemanasCumpridas(IReadOnlyList<DesafioDaPessoa> desafios)
    {
        static IEnumerable<DateOnly> Inicios(IEnumerable<DesafioDaPessoa> lista) =>
            lista.SelectMany(d => d.SemanasCumpridas.Select(indice => d.DataInicio.AddDays(indice * 7)));

        var pagas = Inicios(desafios.Where(d => !d.Cancelado)).Distinct().ToList();
        foreach (var inicio in Inicios(desafios.Where(d => d.Cancelado)).Distinct().Order())
        {
            // Duas semanas de 7 dias se cruzam quando começam a menos de 7 dias uma da outra.
            if (!pagas.Any(paga => Math.Abs(paga.DayNumber - inicio.DayNumber) < 7))
            {
                pagas.Add(inicio);
            }
        }

        return pagas.Count;
    }

    private static DateOnly GetSegunda(DateOnly dia) =>
        dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
}
