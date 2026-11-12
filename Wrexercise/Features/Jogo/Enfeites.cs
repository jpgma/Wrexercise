using Wrexercise.Features.Usuarios.Contracts;

namespace Wrexercise.Features.Jogo;

/// <param name="Codigo">O código do Enfeite (a família): é o que a vitrine guarda.</param>
/// <param name="Conquista">A conquista do nível mais alto que a pessoa tem; define o desenho.</param>
internal readonly record struct EnfeiteLiberado(string Codigo, string Conquista, string Nome);

/// <param name="Conquista">A conquista do primeiro nível, que libera o Enfeite; define o desenho.</param>
/// <param name="Atual">O progresso da pessoa na conquista, de <paramref name="Alvo"/>.</param>
internal readonly record struct EnfeiteBloqueado(
    string Codigo,
    string Conquista,
    string Nome,
    string TituloConquista,
    int Atual,
    int Alvo);

/// <summary>
/// Os Enfeites liberados pelas conquistas e a vitrine (docs/gamificacao.md §9). A vitrine guarda o código do
/// Enfeite, e não o da conquista: quando a planta ou o kettlebell evolui, ela continua no mesmo lugar.
/// </summary>
internal static class Enfeites
{
    public const int MaxVitrine = 3;

    /// <summary>Do nível mais baixo ao mais alto: a conquista mais alta que a pessoa tem vale.</summary>
    private static readonly (string Codigo, (string Conquista, string Nome)[] Niveis)[] Catalogo =
    [
        ("planta", [("Sequencia4", "Muda num vaso"), ("Sequencia8", "Planta com flor"), ("Sequencia12", "Árvore em vaso")]),
        ("kettlebell", [("Treinos10", "Kettlebell de bronze"), ("Treinos50", "Kettlebell de prata"), ("Treinos100", "Kettlebell de ouro")]),
        ("trofeu", [("DesafioCompleto", "Troféu no pedestal")]),
        ("haltere", [("RatoDeAcademia", "Suporte de halteres")]),
        ("largada", [("Corredor", "Bloco de largada")]),
        ("cachorro", [("Andarilho", "Cachorro")]),
        ("bicicleta", [("Ciclista", "Bicicleta")]),
        ("piscina", [("Peixe", "Piscininha com boia")]),
        ("golzinho", [("Boleiro", "Golzinho")]),
        ("tapete", [("Zen", "Tapete de ioga")]),
        ("paralelas", [("PesoDoCorpo", "Paralelas")]),
        ("galo", [("Madrugador", "Galo")]),
        ("poste", [("Noturno", "Poste de luz da praça")]),
        ("caixa-de-som", [("FimDeSemana", "Caixa de som")]),
        ("mochila", [("Ecletico", "Bolsa de academia")]),
    ];

    /// <summary>Os Enfeites que as conquistas liberam, na ordem do catálogo.</summary>
    public static IReadOnlyList<EnfeiteLiberado> Liberados(IEnumerable<string> conquistas)
    {
        var tem = conquistas.ToHashSet(StringComparer.Ordinal);
        var liberados = new List<EnfeiteLiberado>();
        foreach (var (codigo, niveis) in Catalogo)
        {
            for (var i = niveis.Length - 1; i >= 0; i--)
            {
                if (tem.Contains(niveis[i].Conquista))
                {
                    liberados.Add(new EnfeiteLiberado(codigo, niveis[i].Conquista, niveis[i].Nome));
                    break;
                }
            }
        }

        return liberados;
    }

    /// <summary>
    /// Os Enfeites que a pessoa ainda não tem em nenhum nível, na ordem do catálogo, com o progresso na
    /// conquista do primeiro nível. Quem já tem a muda não vê a planta com flor aqui: ela é evolução.
    /// </summary>
    public static IReadOnlyList<EnfeiteBloqueado> Bloqueados(IEnumerable<ProgressoConquista> progresso)
    {
        var porCodigo = progresso.ToDictionary(c => c.Codigo, StringComparer.Ordinal);
        var bloqueados = new List<EnfeiteBloqueado>();
        foreach (var (codigo, niveis) in Catalogo)
        {
            var temAlgum = niveis.Any(n => porCodigo.TryGetValue(n.Conquista, out var c) && c.Liberada);
            if (temAlgum || !porCodigo.TryGetValue(niveis[0].Conquista, out var primeira))
            {
                continue;
            }

            bloqueados.Add(new EnfeiteBloqueado(
                codigo, primeira.Codigo, niveis[0].Nome, primeira.Titulo, primeira.Atual, primeira.Alvo));
        }

        return bloqueados;
    }

    /// <summary>
    /// A vitrine em vigor: a guardada, sem o que não vale mais, e os Enfeites liberados que a pessoa ainda
    /// não viu entram nas vagas. <paramref name="novos"/> são os liberados que a pessoa ainda não viu.
    /// </summary>
    public static (IReadOnlyList<string> Vitrine, IReadOnlyList<string> Novos) Resolver(
        IReadOnlyList<EnfeiteLiberado> liberados,
        IEnumerable<string> vitrineGuardada,
        IEnumerable<string> vistos)
    {
        var codigosLiberados = liberados.Select(e => e.Codigo).ToHashSet(StringComparer.Ordinal);
        var vitrine = vitrineGuardada
            .Where(codigosLiberados.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxVitrine)
            .ToList();

        var jaVistos = vistos.ToHashSet(StringComparer.Ordinal);
        var novos = liberados.Where(e => !jaVistos.Contains(e.Codigo)).Select(e => e.Codigo).ToList();
        foreach (var codigo in novos)
        {
            if (vitrine.Count >= MaxVitrine)
            {
                break;
            }

            if (!vitrine.Contains(codigo))
            {
                vitrine.Add(codigo);
            }
        }

        return (vitrine, novos);
    }

    /// <summary>Se a lista é uma vitrine válida: até 3 Enfeites, sem repetir, todos liberados.</summary>
    public static bool IsVitrineValida(IReadOnlyList<EnfeiteLiberado> liberados, IReadOnlyList<string> vitrine) =>
        vitrine.Count <= MaxVitrine
        && vitrine.Distinct(StringComparer.Ordinal).Count() == vitrine.Count
        && vitrine.All(codigo => liberados.Any(e => e.Codigo == codigo));
}
