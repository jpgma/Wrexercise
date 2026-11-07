using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Grupos.Contracts;
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
        var destacado = await LoadDesafioDestaqueAsync(connection, grupoId, cancellationToken);
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

    private sealed record DesafioDestaque(Guid Id, string Nome, string Situacao, int DuracaoMinimaSegundos);

    private static async Task<DesafioDestaque?> LoadDesafioDestaqueAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT d.id, d.nome, d.data_inicio, d.data_fim, d.cancelled_at IS NOT NULL,
                (now() AT TIME ZONE g.fuso_horario)::date, d.duracao_minima_segundos
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            WHERE d.grupo_id = $1
            ORDER BY
                d.cancelled_at IS NOT NULL,
                d.data_fim < (now() AT TIME ZONE g.fuso_horario)::date,
                CASE WHEN d.data_fim >= (now() AT TIME ZONE g.fuso_horario)::date THEN d.data_inicio END,
                d.data_inicio DESC,
                d.id
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetBoolean(4))
            {
                continue;
            }

            var inicio = reader.GetFieldValue<DateOnly>(2);
            var fim = reader.GetFieldValue<DateOnly>(3);
            var hoje = reader.GetFieldValue<DateOnly>(5);
            var situacao = GetSituacao(inicio, fim, cancelado: false, hoje);
            return new DesafioDestaque(reader.GetGuid(0), reader.GetString(1), situacao, reader.GetInt32(6));
        }

        return null;
    }

    private static string GetSituacao(DateOnly inicio, DateOnly fim, bool cancelado, DateOnly hoje)
    {
        if (cancelado)
        {
            return SituacaoDesafio.Cancelado;
        }

        if (hoje < inicio)
        {
            return SituacaoDesafio.Agendado;
        }

        return hoje > fim ? SituacaoDesafio.Encerrado : SituacaoDesafio.Ativo;
    }
}
