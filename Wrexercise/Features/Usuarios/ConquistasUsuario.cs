namespace Wrexercise.Features.Usuarios;

/// <param name="TotalTreinos">Atividades verificadas, mostradas no Perfil.</param>
/// <param name="TreinosPorTipo">Atividades verificadas por tipo; tipos sem treino podem faltar.</param>
/// <param name="DiasComTreino">
/// Dias com atividade verificada em cada grupo, que dão as medalhas; antes de
/// <see cref="ConquistasUsuario.InicioTreinosPorDia"/>, cada atividade conta um.
/// </param>
internal readonly record struct EstatisticasConquistas(
    int TotalTreinos,
    int MelhorSequencia,
    int TreinosAntesDas7,
    int TreinosNoturnos = 0,
    int TreinosFimDeSemana = 0,
    int DesafiosCompletos = 0,
    IReadOnlyDictionary<string, int>? TreinosPorTipo = null,
    int DiasComTreino = 0);

/// <summary>Conquistas são só reconhecimento: calculadas na leitura, sem efeito na classificação.</summary>
internal static class ConquistasUsuario
{
    public const string DesafioCompleto = "DesafioCompleto";
    public const string Madrugador = "Madrugador";
    public const string Noturno = "Noturno";
    public const string FimDeSemana = "FimDeSemana";
    public const string Ecletico = "Ecletico";

    public const int TreinosMadrugador = 10;
    public const int TreinosNoturno = 10;
    public const int TreinosDeFimDeSemana = 10;
    public const int TreinosPorTipoMarco = 10;
    public const int TiposEcletico = 5;
    public const int HoraNoturno = 20;

    /// <summary>
    /// A partir deste dia, as medalhas contam dias com treino, não atividades (decisão 0040): dividir um
    /// treino não as acelera. Antes dele, cada atividade conta um, para que ninguém perca uma medalha.
    /// </summary>
    public static readonly DateOnly InicioTreinosPorDia = new(2026, 11, 9);

    private static readonly int[] MarcosSequencia = [12, 8, 4];
    private static readonly int[] MarcosTreinos = [100, 50, 10];

    /// <summary>Uma conquista por tipo de atividade; <c>Outro</c> não tem.</summary>
    private static readonly (string Tipo, string Codigo, string Titulo)[] ConquistasDeTipo =
    [
        (Tipos.Musculacao, "RatoDeAcademia", "Rato de academia"),
        (Tipos.Corrida, "Corredor", "Corredor"),
        (Tipos.Caminhada, "Andarilho", "Andarilho"),
        (Tipos.Ciclismo, "Ciclista", "Ciclista"),
        (Tipos.Natacao, "Peixe", "Peixe"),
        (Tipos.Esporte, "Boleiro", "Boleiro"),
        (Tipos.Yoga, "Zen", "Zen"),
        (Tipos.Calistenia, "PesoDoCorpo", "Peso do corpo"),
    ];

    /// <summary>As conquistas que a pessoa já tem.</summary>
    public static IReadOnlyList<ConquistaResponse> Calcular(EstatisticasConquistas estatisticas) =>
        Listar(estatisticas).Where(c => c.Liberada).ToList();

    /// <summary>
    /// Todas as conquistas, liberadas ou não, com o progresso da pessoa (<c>Atual</c> nunca passa de
    /// <c>Alvo</c>). Nos níveis, a ordem vai do mais alto ao mais baixo.
    /// </summary>
    public static IReadOnlyList<ConquistaResponse> Listar(EstatisticasConquistas estatisticas)
    {
        var conquistas = new List<ConquistaResponse>();

        foreach (var marco in MarcosSequencia)
        {
            conquistas.Add(Criar(
                $"Sequencia{marco}",
                $"{marco} semanas seguidas",
                "Meta batida sem falhar em um desafio",
                estatisticas.MelhorSequencia,
                marco));
        }

        foreach (var marco in MarcosTreinos)
        {
            conquistas.Add(Criar(
                $"Treinos{marco}",
                $"{marco} treinos",
                "Dias com treino verificado pelos amigos",
                estatisticas.DiasComTreino,
                marco));
        }

        conquistas.Add(Criar(
            DesafioCompleto,
            "Desafio completo",
            "Todas as semanas de um desafio com a meta cumprida",
            estatisticas.DesafiosCompletos,
            1));

        var porTipo = estatisticas.TreinosPorTipo;
        foreach (var (tipo, codigo, titulo) in ConquistasDeTipo)
        {
            var total = porTipo is not null && porTipo.TryGetValue(tipo, out var valor) ? valor : 0;
            conquistas.Add(Criar(
                codigo,
                titulo,
                $"{TreinosPorTipoMarco} treinos verificados de {NomeDoTipo(tipo)}",
                total,
                TreinosPorTipoMarco));
        }

        conquistas.Add(Criar(
            Madrugador,
            "Madrugador",
            $"{TreinosMadrugador} treinos verificados antes das 7h",
            estatisticas.TreinosAntesDas7,
            TreinosMadrugador));

        conquistas.Add(Criar(
            Noturno,
            "Noturno",
            $"{TreinosNoturno} treinos verificados a partir das {HoraNoturno}h",
            estatisticas.TreinosNoturnos,
            TreinosNoturno));

        conquistas.Add(Criar(
            FimDeSemana,
            "Fim de semana",
            $"{TreinosDeFimDeSemana} treinos verificados no sábado ou no domingo",
            estatisticas.TreinosFimDeSemana,
            TreinosDeFimDeSemana));

        conquistas.Add(Criar(
            Ecletico,
            "Eclético",
            $"Treinos verificados de {TiposEcletico} tipos diferentes",
            porTipo?.Count(par => par.Key != Tipos.Outro && par.Value > 0) ?? 0,
            TiposEcletico));

        return conquistas;
    }

    /// <summary>
    /// O que o Perfil mostra: as liberadas, na ordem de <see cref="Listar"/>, e depois as bloqueadas, da mais
    /// perto à mais longe de sair. Nos níveis (Sequência, Treinos), só o próximo nível bloqueado aparece.
    /// </summary>
    public static IReadOnlyList<ConquistaResponse> Visiveis(IReadOnlyList<ConquistaResponse> todas)
    {
        var bloqueadas = todas.Where(c => !c.Liberada).ToList();
        var proximas = bloqueadas
            .Where(c => !bloqueadas.Any(outra => Familia(outra) == Familia(c) && outra.Alvo < c.Alvo))
            .OrderByDescending(c => (double)c.Atual / c.Alvo);
        return [.. todas.Where(c => c.Liberada), .. proximas];
    }

    /// <summary>Os níveis de uma família só diferem pelo número no fim do código (<c>Sequencia4</c>, <c>Sequencia8</c>).</summary>
    private static string Familia(ConquistaResponse conquista) => conquista.Codigo.TrimEnd("0123456789".ToCharArray());

    private static ConquistaResponse Criar(string codigo, string titulo, string descricao, int atual, int alvo) =>
        new(codigo, titulo, descricao, Math.Min(Math.Max(atual, 0), alvo), alvo, atual >= alvo);

    private static string NomeDoTipo(string tipo) => tipo switch
    {
        Tipos.Musculacao => "musculação",
        Tipos.Corrida => "corrida",
        Tipos.Caminhada => "caminhada",
        Tipos.Ciclismo => "ciclismo",
        Tipos.Natacao => "natação",
        Tipos.Esporte => "esporte",
        Tipos.Yoga => "yoga",
        Tipos.Calistenia => "calistenia",
        _ => tipo,
    };
}

/// <summary>
/// Os nomes dos tipos de atividade, como gravados. Copiados de propósito: uma fatia não referencia
/// os detalhes internos de outra.
/// </summary>
internal static class Tipos
{
    public const string Musculacao = "Musculacao";
    public const string Corrida = "Corrida";
    public const string Caminhada = "Caminhada";
    public const string Ciclismo = "Ciclismo";
    public const string Natacao = "Natacao";
    public const string Esporte = "Esporte";
    public const string Yoga = "Yoga";
    public const string Calistenia = "Calistenia";
    public const string Outro = "Outro";
}
