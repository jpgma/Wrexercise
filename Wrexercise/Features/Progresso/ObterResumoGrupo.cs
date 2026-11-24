using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Progresso;

internal static class ObterResumoGrupo
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        if (await GrupoAcesso.GetPapelAsync(dataSource, grupoId, usuarioId, cancellationToken) is null)
        {
            return OperacaoProgresso.GrupoNaoEncontrado();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var destacado = await DesafioDestaqueGrupo.LoadAsync(connection, grupoId, cancellationToken);
        if (destacado is null)
        {
            return Results.Ok(new ResumoGrupoResponse(grupoId, null, null, null, [], null));
        }

        var desafio = await ContagensDesafio.LoadDesafioAsync(connection, destacado.Id, usuarioId, cancellationToken);
        if (desafio is null)
        {
            return OperacaoProgresso.GrupoNaoEncontrado();
        }

        var participantes = await ContagensDesafio.LoadParticipantesAsync(connection, destacado.Id, cancellationToken);
        var matriz = await ContagensDesafio.LoadMatrizAsync(connection, destacado.Id, cancellationToken);
        var totais = await ContagensDesafio.LoadTotaisVerificadosAsync(connection, destacado.Id, cancellationToken);

        var ranking = ClassificacaoDesafio.CalcularRanking(
            participantes,
            matriz,
            totais,
            desafio.DataInicio,
            desafio.DataFim,
            desafio.Hoje,
            desafio.SessoesPorSemana);
        var ativo = destacado.Situacao == SituacaoDesafio.Ativo;

        var classificacao = ranking.Select(item => new ClassificacaoResumoItemResponse(
            item.Posicao,
            item.UsuarioId,
            item.NomeExibicao,
            item.SemanasCumpridas,
            item.SessoesVerificadas,
            item.SequenciaSemanas,
            item.SegundosVerificados,
            item.AtividadesVerificadas,
            item.SemanasDisputadas)).ToList();

        ProgressoResumoResponse? meuProgresso = null;
        if (!desafio.Cancelado)
        {
            var semana = CalculoSemanaDesafio.GetSemana(desafio.DataInicio, desafio.DataFim, desafio.Hoje);
            if (semana is not null)
            {
                var indice = CalculoSemanaDesafio.GetIndiceSemana(desafio.DataInicio, semana.Inicio);
                matriz.TryGetValue((usuarioId, indice), out var contagem);
                var progresso = CalculoSemanaDesafio.CalculateProgresso(
                    contagem.Verificadas,
                    contagem.Pendentes,
                    desafio.SessoesPorSemana);
                meuProgresso = new ProgressoResumoResponse(
                    progresso.Verificadas,
                    progresso.Provisorias,
                    progresso.MetaCumprida,
                    semana.Numero,
                    CalculoSemanaDesafio.ParticipaDaSemana(semana.Inicio, desafio.EntradaUsuario),
                    CalculoSemanaDesafio.CountDiasRestantes(semana, desafio.Hoje));
            }
        }

        return Results.Ok(new ResumoGrupoResponse(
            grupoId,
            new DesafioDestaqueResponse(
                destacado.Id,
                destacado.Nome,
                destacado.Situacao,
                desafio.SessoesPorSemana,
                destacado.DuracaoMinimaSegundos),
            desafio.SessoesPorSemana,
            meuProgresso,
            classificacao,
            ativo && ClassificacaoDesafio.GetProximo(ranking, usuarioId, meuProgresso?.MetaCumprida ?? false) is { } proximo
                ? new ProximoClassificacaoResponse(proximo.Tipo, proximo.NomeExibicao, proximo.Quantidade)
                : null));
    }
}
