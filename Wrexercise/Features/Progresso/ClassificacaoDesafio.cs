using Wrexercise.Features.Desafios.Contracts;

namespace Wrexercise.Features.Progresso;

/// <summary>Quem disputa o desafio e o dia local em que entrou no grupo.</summary>
internal sealed record ParticipanteDesafio(Guid UsuarioId, string NomeExibicao, DateOnly Entrada);

/// <summary>A soma das durações e a quantidade das atividades verificadas que valem para o desafio.</summary>
internal readonly record struct TotaisVerificados(int Segundos, int Atividades);

internal sealed record EstatisticasParticipante(
    Guid UsuarioId,
    string NomeExibicao,
    int SemanasCumpridas,
    int SessoesVerificadas,
    int SequenciaSemanas,
    int SegundosVerificados = 0,
    int AtividadesVerificadas = 0,
    int SemanasDisputadas = 0,
    int Posicao = 0);

/// <summary>Os tipos da chamada de disputa do Placar (ADR 0066).</summary>
internal static class TipoProximoClassificacao
{
    /// <summary>A pessoa lidera. A quantidade é a vantagem em dias sobre o segundo, se empatados em semanas.</summary>
    public const string Lider = "Lider";

    /// <summary>A pessoa divide o 1º lugar com quem vem no nome (ADR 0090).</summary>
    public const string EmpateLider = "EmpateLider";

    /// <summary>Mesmas semanas cumpridas que quem está acima: faltam N dias de treino para passar.</summary>
    public const string Treinos = "Treinos";

    /// <summary>Quem está acima tem uma semana a mais e a meta desta semana ainda não foi batida.</summary>
    public const string Meta = "Meta";

    /// <summary>Quem está acima tem N semanas cumpridas a mais.</summary>
    public const string Semanas = "Semanas";
}

internal sealed record ProximoClassificacao(string Tipo, string NomeExibicao, int Quantidade);

/// <summary>
/// A classificação do desafio (ADR 0013, revista pela 0090): semanas cumpridas, dias verificados,
/// sequência atual, tempo verificado e atividades verificadas. Quem empata em tudo divide a posição.
/// </summary>
internal static class ClassificacaoDesafio
{
    public static IReadOnlyList<EstatisticasParticipante> CalcularRanking(
        IReadOnlyList<ParticipanteDesafio> participantes,
        Dictionary<(Guid UsuarioId, int IndiceSemana), (int Verificadas, int Pendentes)> matriz,
        IReadOnlyDictionary<Guid, TotaisVerificados> totais,
        DateOnly inicio,
        DateOnly fim,
        DateOnly hoje,
        int sessoesPorSemana)
    {
        var totalSemanas = CalculoSemanaDesafio.CountSemanas(inicio, fim);
        var indiceRecente = CalculoSemanaDesafio.GetIndiceSemanaMaisRecente(inicio, fim, hoje);
        var emAndamento = CalculoSemanaDesafio.GetSemana(inicio, fim, hoje) is not null;

        var stats = participantes.Select(p =>
        {
            var verificadasTotal = 0;
            var semanasCumpridas = 0;
            var semanasDisputadas = 0;
            var porSemana = new bool[totalSemanas];

            for (var indice = 0; indice < totalSemanas; indice++)
            {
                matriz.TryGetValue((p.UsuarioId, indice), out var contagem);
                verificadasTotal += contagem.Verificadas;
                var progresso = CalculoSemanaDesafio.CalculateProgresso(
                    contagem.Verificadas,
                    contagem.Pendentes,
                    sessoesPorSemana);
                porSemana[indice] = progresso.MetaCumprida;
                if (progresso.MetaCumprida)
                {
                    semanasCumpridas++;
                }

                if (indice <= indiceRecente
                    && CalculoSemanaDesafio.ParticipaDaSemana(inicio.AddDays(indice * 7), p.Entrada))
                {
                    semanasDisputadas++;
                }
            }

            totais.TryGetValue(p.UsuarioId, out var total);

            return new EstatisticasParticipante(
                p.UsuarioId,
                p.NomeExibicao,
                semanasCumpridas,
                verificadasTotal,
                CalculoSemanaDesafio.GetSequenciaAtual(porSemana, indiceRecente, emAndamento),
                total.Segundos,
                total.Atividades,
                semanasDisputadas);
        });

        // O nome só dá uma ordem estável dentro do empate: quem empata em tudo divide a posição.
        var ordenados = stats
            .OrderByDescending(s => s.SemanasCumpridas)
            .ThenByDescending(s => s.SessoesVerificadas)
            .ThenByDescending(s => s.SequenciaSemanas)
            .ThenByDescending(s => s.SegundosVerificados)
            .ThenByDescending(s => s.AtividadesVerificadas)
            .ThenBy(s => s.NomeExibicao, StringComparer.Ordinal)
            .ThenBy(s => s.UsuarioId)
            .ToList();

        var ranking = new List<EstatisticasParticipante>(ordenados.Count);
        for (var i = 0; i < ordenados.Count; i++)
        {
            var posicao = i > 0 && Empatam(ordenados[i - 1], ordenados[i]) ? ranking[i - 1].Posicao : i + 1;
            ranking.Add(ordenados[i] with { Posicao = posicao });
        }

        return ranking;
    }

    /// <summary>Iguais em todos os critérios da classificação.</summary>
    public static bool Empatam(EstatisticasParticipante a, EstatisticasParticipante b) =>
        a.SemanasCumpridas == b.SemanasCumpridas
        && a.SessoesVerificadas == b.SessoesVerificadas
        && a.SequenciaSemanas == b.SequenciaSemanas
        && a.SegundosVerificados == b.SegundosVerificados
        && a.AtividadesVerificadas == b.AtividadesVerificadas;

    /// <summary>Quem a pessoa persegue na classificação, ou a vantagem dela quando lidera.</summary>
    /// <returns><c>null</c> se ela não participa, se está sozinha ou se ninguém tem treino verificado.</returns>
    public static ProximoClassificacao? GetProximo(
        IReadOnlyList<EstatisticasParticipante> ranking,
        Guid usuarioId,
        bool metaDaSemanaCumprida)
    {
        var lista = ranking.ToList();
        var posicao = lista.FindIndex(r => r.UsuarioId == usuarioId);
        if (posicao < 0 || ranking.Count < 2 || ranking.All(r => r.SessoesVerificadas == 0))
        {
            return null;
        }

        var eu = ranking[posicao];
        if (eu.Posicao == 1)
        {
            if (lista.Find(r => r.Posicao == 1 && r.UsuarioId != usuarioId) is { } empatado)
            {
                return new ProximoClassificacao(TipoProximoClassificacao.EmpateLider, empatado.NomeExibicao, 0);
            }

            var segundo = ranking[1];
            var vantagem = segundo.SemanasCumpridas == eu.SemanasCumpridas
                ? eu.SessoesVerificadas - segundo.SessoesVerificadas
                : 0;
            return new ProximoClassificacao(TipoProximoClassificacao.Lider, segundo.NomeExibicao, vantagem);
        }

        // Quem está de fato acima: quem divide a posição com a pessoa não conta.
        var acima = lista.FindLast(r => r.Posicao < eu.Posicao)!;
        var diferenca = acima.SemanasCumpridas - eu.SemanasCumpridas;
        if (diferenca == 0)
        {
            // Empate em dias é decidido pela sequência, pelo tempo ou pelas atividades: um dia a mais basta.
            var dias = Math.Max(1, acima.SessoesVerificadas - eu.SessoesVerificadas + 1);
            return new ProximoClassificacao(TipoProximoClassificacao.Treinos, acima.NomeExibicao, dias);
        }

        return diferenca == 1 && !metaDaSemanaCumprida
            ? new ProximoClassificacao(TipoProximoClassificacao.Meta, acima.NomeExibicao, 1)
            : new ProximoClassificacao(TipoProximoClassificacao.Semanas, acima.NomeExibicao, diferenca);
    }
}
