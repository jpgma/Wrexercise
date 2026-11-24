using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Progresso;

internal static class ObterClassificacao
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var usuarioId = context.GetSessaoAtual().UsuarioId;

        var desafio = await ContagensDesafio.LoadDesafioAsync(connection, id, usuarioId, cancellationToken);
        if (desafio is null)
        {
            return OperacaoProgresso.DesafioNaoEncontrado();
        }

        var participantes = await ContagensDesafio.LoadParticipantesAsync(connection, id, cancellationToken);
        var matriz = await ContagensDesafio.LoadMatrizAsync(connection, id, cancellationToken);
        var totais = await ContagensDesafio.LoadTotaisVerificadosAsync(connection, id, cancellationToken);

        var ranking = ClassificacaoDesafio.CalcularRanking(
            participantes,
            matriz,
            totais,
            desafio.DataInicio,
            desafio.DataFim,
            desafio.Hoje,
            desafio.SessoesPorSemana);

        var resposta = ranking.Select(item => new ClassificacaoParticipanteResponse(
            item.Posicao,
            item.UsuarioId,
            item.NomeExibicao,
            item.SemanasCumpridas,
            item.SessoesVerificadas,
            item.SequenciaSemanas,
            item.SegundosVerificados,
            item.AtividadesVerificadas,
            item.SemanasDisputadas)).ToList();

        return Results.Ok(new ClassificacaoResponse(
            desafio.SessoesPorSemana,
            CalculoSemanaDesafio.CountSemanas(desafio.DataInicio, desafio.DataFim),
            resposta));
    }
}
