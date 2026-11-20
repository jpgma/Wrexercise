using Wrexercise.Features.Desafios.Contracts;

namespace Wrexercise.Features.Progresso;

internal sealed record EstatisticasParticipante(
    Guid UsuarioId,
    string NomeExibicao,
    int SemanasCumpridas,
    int SessoesVerificadas,
    int SequenciaSemanas);

/// <summary>Os tipos da chamada de disputa do Placar (ADR 0066).</summary>
internal static class TipoProximoClassificacao
{
    /// <summary>A pessoa lidera. A quantidade é a vantagem em treinos sobre o segundo, se empatados em semanas.</summary>
    public const string Lider = "Lider";

    /// <summary>Mesmas semanas cumpridas que quem está acima: faltam N treinos para passar.</summary>
    public const string Treinos = "Treinos";

    /// <summary>Quem está acima tem uma semana a mais e a meta desta semana ainda não foi batida.</summary>
    public const string Meta = "Meta";

    /// <summary>Quem está acima tem N semanas cumpridas a mais.</summary>
    public const string Semanas = "Semanas";
}

internal sealed record ProximoClassificacao(string Tipo, string NomeExibicao, int Quantidade);

internal static class ClassificacaoDesafio
{
    public static IReadOnlyList<EstatisticasParticipante> CalcularRanking(
        IReadOnlyList<(Guid UsuarioId, string NomeExibicao)> participantes,
        Dictionary<(Guid UsuarioId, int IndiceSemana), (int Verificadas, int Pendentes)> matriz,
        DateOnly inicio,
        DateOnly fim,
        DateOnly hoje,
        int sessoesPorSemana)
    {
        var totalSemanas = CalculoSemanaDesafio.CountSemanas(inicio, fim);
        var indiceRecente = CalculoSemanaDesafio.GetIndiceSemanaMaisRecente(inicio, fim, hoje);

        var stats = participantes.Select(p =>
        {
            var verificadasTotal = 0;
            var semanasCumpridas = 0;
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
            }

            var sequencia = CalculoSemanaDesafio.GetSequenciaAtual(porSemana, indiceRecente);

            return new EstatisticasParticipante(
                p.UsuarioId,
                p.NomeExibicao,
                semanasCumpridas,
                verificadasTotal,
                sequencia);
        }).ToList();

        return stats
            .OrderByDescending(s => s.SemanasCumpridas)
            .ThenByDescending(s => s.SessoesVerificadas)
            .ThenByDescending(s => s.SequenciaSemanas)
            .ThenBy(s => s.NomeExibicao, StringComparer.Ordinal)
            .ThenBy(s => s.UsuarioId)
            .ToList();
    }

    /// <summary>Quem a pessoa persegue na classificação, ou a vantagem dela quando lidera.</summary>
    /// <returns><c>null</c> se ela não participa, se está sozinha ou se ninguém tem treino verificado.</returns>
    public static ProximoClassificacao? GetProximo(
        IReadOnlyList<EstatisticasParticipante> ranking,
        Guid usuarioId,
        bool metaDaSemanaCumprida)
    {
        var posicao = ranking.ToList().FindIndex(r => r.UsuarioId == usuarioId);
        if (posicao < 0 || ranking.Count < 2 || ranking.All(r => r.SessoesVerificadas == 0))
        {
            return null;
        }

        var eu = ranking[posicao];
        if (posicao == 0)
        {
            var segundo = ranking[1];
            var vantagem = segundo.SemanasCumpridas == eu.SemanasCumpridas
                ? eu.SessoesVerificadas - segundo.SessoesVerificadas
                : 0;
            return new ProximoClassificacao(TipoProximoClassificacao.Lider, segundo.NomeExibicao, vantagem);
        }

        var acima = ranking[posicao - 1];
        var diferenca = acima.SemanasCumpridas - eu.SemanasCumpridas;
        if (diferenca == 0)
        {
            // Empate em sessões é decidido pela sequência ou pelo nome: um treino a mais basta.
            var treinos = Math.Max(1, acima.SessoesVerificadas - eu.SessoesVerificadas + 1);
            return new ProximoClassificacao(TipoProximoClassificacao.Treinos, acima.NomeExibicao, treinos);
        }

        return diferenca == 1 && !metaDaSemanaCumprida
            ? new ProximoClassificacao(TipoProximoClassificacao.Meta, acima.NomeExibicao, 1)
            : new ProximoClassificacao(TipoProximoClassificacao.Semanas, acima.NomeExibicao, diferenca);
    }
}
