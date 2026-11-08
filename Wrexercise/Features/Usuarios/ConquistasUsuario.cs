namespace Wrexercise.Features.Usuarios;

/// <param name="TreinosPorTipo">Atividades verificadas por tipo; tipos sem treino podem faltar.</param>
internal readonly record struct EstatisticasConquistas(
    int TotalTreinos,
    int MelhorSequencia,
    int TreinosAntesDas7,
    int TreinosNoturnos = 0,
    int TreinosFimDeSemana = 0,
    int DesafiosCompletos = 0,
    IReadOnlyDictionary<string, int>? TreinosPorTipo = null);

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

    public static IReadOnlyList<ConquistaResponse> Calcular(EstatisticasConquistas estatisticas)
    {
        var conquistas = new List<ConquistaResponse>();

        foreach (var marco in MarcosSequencia.Where(marco => estatisticas.MelhorSequencia >= marco))
        {
            conquistas.Add(new ConquistaResponse(
                $"Sequencia{marco}",
                $"{marco} semanas seguidas",
                "Meta batida sem falhar em um desafio"));
        }

        foreach (var marco in MarcosTreinos.Where(marco => estatisticas.TotalTreinos >= marco))
        {
            conquistas.Add(new ConquistaResponse(
                $"Treinos{marco}",
                $"{marco} treinos",
                "Treinos verificados pelos amigos"));
        }

        if (estatisticas.DesafiosCompletos > 0)
        {
            conquistas.Add(new ConquistaResponse(
                DesafioCompleto,
                "Desafio completo",
                "Todas as semanas de um desafio com a meta cumprida"));
        }

        var porTipo = estatisticas.TreinosPorTipo;
        foreach (var (tipo, codigo, titulo) in ConquistasDeTipo)
        {
            if (porTipo is not null && porTipo.TryGetValue(tipo, out var total) && total >= TreinosPorTipoMarco)
            {
                conquistas.Add(new ConquistaResponse(
                    codigo,
                    titulo,
                    $"{TreinosPorTipoMarco} treinos verificados de {NomeDoTipo(tipo)}"));
            }
        }

        if (estatisticas.TreinosAntesDas7 >= TreinosMadrugador)
        {
            conquistas.Add(new ConquistaResponse(
                Madrugador,
                "Madrugador",
                $"{TreinosMadrugador} treinos verificados antes das 7h"));
        }

        if (estatisticas.TreinosNoturnos >= TreinosNoturno)
        {
            conquistas.Add(new ConquistaResponse(
                Noturno,
                "Noturno",
                $"{TreinosNoturno} treinos verificados a partir das {HoraNoturno}h"));
        }

        if (estatisticas.TreinosFimDeSemana >= TreinosDeFimDeSemana)
        {
            conquistas.Add(new ConquistaResponse(
                FimDeSemana,
                "Fim de semana",
                $"{TreinosDeFimDeSemana} treinos verificados no sábado ou no domingo"));
        }

        if (porTipo is not null
            && porTipo.Count(par => par.Key != Tipos.Outro && par.Value > 0) >= TiposEcletico)
        {
            conquistas.Add(new ConquistaResponse(
                Ecletico,
                "Eclético",
                $"Treinos verificados de {TiposEcletico} tipos diferentes"));
        }

        return conquistas;
    }

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
