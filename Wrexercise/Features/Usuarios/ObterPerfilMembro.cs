using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Usuarios;

internal static class ObterPerfilMembro
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        Guid usuarioId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var quemConsulta = context.GetSessaoAtual().UsuarioId;
        if (await GrupoAcesso.GetPapelAsync(connection, null, id, quemConsulta, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        string nome;
        string papel;
        DateTimeOffset joinedAt;
        await using (var membro = new NpgsqlCommand("""
            SELECT u.nome_exibicao, m.papel, m.joined_at
            FROM membros_grupo m
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $1 AND m.usuario_id = $2 AND m.removed_at IS NULL
            """, connection))
        {
            membro.Parameters.AddWithValue(id);
            membro.Parameters.AddWithValue(usuarioId);
            await using var reader = await membro.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ApiProblem.Create(
                    StatusCodes.Status404NotFound,
                    "MEMBRO_NAO_ENCONTRADO",
                    "Essa pessoa não faz parte do grupo.");
            }

            nome = reader.GetString(0);
            papel = reader.GetString(1);
            joinedAt = TimestampLeitura.FromReader(reader, 2);
        }

        int totalTreinos;
        long totalSegundos;
        await using (var totais = new NpgsqlCommand("""
            SELECT count(*)::int, COALESCE(sum(duracao_segundos), 0)::bigint
            FROM atividades
            WHERE grupo_id = $1 AND usuario_id = $2 AND situacao_verificacao = 'Verificada'
            """, connection))
        {
            totais.Parameters.AddWithValue(id);
            totais.Parameters.AddWithValue(usuarioId);
            await using var reader = await totais.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalTreinos = reader.GetInt32(0);
            totalSegundos = reader.GetInt64(1);
        }

        var desafio = await LoadDesafioAsync(connection, id, usuarioId, cancellationToken);

        return Results.Json(
            new PerfilMembroResponse(usuarioId, nome, papel, joinedAt, totalTreinos, totalSegundos, desafio),
            AppJsonContext.Default.PerfilMembroResponse);
    }

    private static async Task<DesafioMembroResponse?> LoadDesafioAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        Guid desafioId;
        string desafioNome;
        string fuso;
        DateOnly inicio;
        DateOnly fim;
        DateOnly hoje;
        DateOnly entrada;
        int sessoesPorSemana;
        await using (var command = new NpgsqlCommand("""
            SELECT d.id, d.nome, g.fuso_horario, d.data_inicio, d.data_fim,
                (now() AT TIME ZONE g.fuso_horario)::date,
                (m.joined_at AT TIME ZONE g.fuso_horario)::date,
                d.sessoes_por_semana
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = $2
            WHERE d.grupo_id = $1 AND d.cancelled_at IS NULL
            ORDER BY
                d.data_fim < (now() AT TIME ZONE g.fuso_horario)::date,
                CASE WHEN d.data_fim >= (now() AT TIME ZONE g.fuso_horario)::date THEN d.data_inicio END,
                d.data_inicio DESC,
                d.id
            LIMIT 1
            """, connection))
        {
            command.Parameters.AddWithValue(grupoId);
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            desafioId = reader.GetGuid(0);
            desafioNome = reader.GetString(1);
            fuso = reader.GetString(2);
            inicio = reader.GetFieldValue<DateOnly>(3);
            fim = reader.GetFieldValue<DateOnly>(4);
            hoje = reader.GetFieldValue<DateOnly>(5);
            entrada = reader.GetFieldValue<DateOnly>(6);
            sessoesPorSemana = reader.GetInt32(7);
        }

        var contagens = new Dictionary<int, (int Verificadas, int Pendentes)>();
        await using (var matriz = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildAgregadoPorUsuarioESemana(),
            connection))
        {
            matriz.Parameters.AddWithValue(desafioId);
            await using var reader = await matriz.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetGuid(0) == usuarioId)
                {
                    contagens[reader.GetInt32(1)] = (reader.GetInt32(2), reader.GetInt32(3));
                }
            }
        }

        var totalSemanas = CalculoSemanaDesafio.CountSemanas(inicio, fim);
        var indiceRecente = CalculoSemanaDesafio.GetIndiceSemanaMaisRecente(inicio, fim, hoje);
        var semanas = new List<SemanaMembroResponse>(totalSemanas);
        var cumpridas = new bool[totalSemanas];
        var semanasDisputadas = 0;
        for (var indice = 0; indice < totalSemanas; indice++)
        {
            contagens.TryGetValue(indice, out var contagem);
            var progresso = CalculoSemanaDesafio.CalculateProgresso(
                contagem.Verificadas,
                contagem.Pendentes,
                sessoesPorSemana);
            var inicioSemana = inicio.AddDays(indice * 7);
            var participou = CalculoSemanaDesafio.ParticipaDaSemana(inicioSemana, entrada);
            cumpridas[indice] = progresso.MetaCumprida;
            if (participou && indice <= indiceRecente)
            {
                semanasDisputadas++;
            }

            semanas.Add(new SemanaMembroResponse(
                indice + 1,
                inicioSemana,
                progresso.Verificadas,
                progresso.Provisorias,
                participou));
        }

        var atual = CalculoSemanaDesafio.GetSemana(inicio, fim, hoje);
        var semanaAtual = atual is null ? null : semanas[atual.Numero - 1];

        return new DesafioMembroResponse(
            desafioId,
            desafioNome,
            fuso,
            sessoesPorSemana,
            semanaAtual?.SessoesVerificadas ?? 0,
            semanaAtual?.SessoesProvisorias ?? 0,
            atual is not null,
            semanaAtual?.Participou ?? false,
            cumpridas.Count(cumprida => cumprida),
            semanasDisputadas,
            CalculoSemanaDesafio.GetSequenciaAtual(cumpridas, indiceRecente, atual is not null),
            semanas);
    }
}
