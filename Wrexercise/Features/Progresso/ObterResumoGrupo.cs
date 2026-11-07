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
            return Results.Ok(new ResumoGrupoResponse(grupoId, null, null, null, []));
        }

        var desafio = await ContagensDesafio.LoadDesafioAsync(connection, destacado.Id, usuarioId, cancellationToken);
        if (desafio is null)
        {
            return OperacaoProgresso.GrupoNaoEncontrado();
        }

        var participantes = await ContagensDesafio.LoadParticipantesAsync(connection, destacado.Id, cancellationToken);
        var matriz = await ContagensDesafio.LoadMatrizAsync(connection, destacado.Id, cancellationToken);

        var ranking = ClassificacaoDesafio.CalcularRanking(
            participantes,
            matriz,
            desafio.DataInicio,
            desafio.DataFim,
            desafio.Hoje,
            desafio.SessoesPorSemana);

        var top = ranking.Take(3).Select((item, index) => new ClassificacaoResumoItemResponse(
            index + 1,
            item.UsuarioId,
            item.NomeExibicao,
            item.SemanasCumpridas)).ToList();

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
                    CalculoSemanaDesafio.ParticipaDaSemana(semana.Inicio, desafio.EntradaUsuario));
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
            top));
    }
}
