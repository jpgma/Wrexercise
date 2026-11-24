using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ObterCena
{
    private sealed record MembroCena(
        Guid UsuarioId,
        string NomeExibicao,
        DateOnly EntradaLocal,
        bool ModoJogo,
        DateTimeOffset AcordadoEm);

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        if (await GrupoAcesso.GetPapelAsync(dataSource, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var membros = await LoadMembrosAsync(connection, grupoId, cancellationToken);
        var comPendentes = await LoadComAtividadePendenteAsync(connection, grupoId, cancellationToken);
        var comMeta = await LoadComMetaCumpridaAsync(connection, grupoId, membros, cancellationToken);
        var cutucadas = await LoadCutucadasAsync(connection, grupoId, usuarioId, cancellationToken);
        var aoVivo = await LoadTreinosAoVivoAsync(connection, grupoId, usuarioId, cancellationToken);
        var fadigas = await LoadFadigasAsync(connection, grupoId, membros, cancellationToken);

        var agora = DateTimeOffset.UtcNow;
        var resposta = membros
            .Select(m =>
            {
                var treino = aoVivo.GetValueOrDefault(m.UsuarioId);
                var pose = PoseAvatar.Calcular(
                    treino is not null || comPendentes.Contains(m.UsuarioId),
                    comMeta.Contains(m.UsuarioId),
                    PoseAvatar.IsDormindo(m.AcordadoEm, agora));
                var dorme = pose == PoseAvatar.Dormindo;
                var cutucado = dorme ? cutucadas.GetValueOrDefault(m.UsuarioId) : null;
                return new MembroCenaResponse(
                    m.UsuarioId,
                    m.NomeExibicao,
                    pose,
                    m.ModoJogo,
                    dorme ? m.AcordadoEm : null,
                    cutucado is null ? null : new CutucadasMembroResponse(cutucado.Total, cutucado.UltimoTipo),
                    cutucado?.PorMimEm,
                    treino,
                    fadigas.GetValueOrDefault(m.UsuarioId));
            })
            .ToList();

        return Results.Json(new CenaResponse(grupoId, resposta), AppJsonContext.Default.CenaResponse);
    }

    /// <summary>
    /// A FADIGA de cada membro no modo Jogo (decisão 0089): só quem joga recebe cartas. A FADIGA vale em todos os
    /// grupos da pessoa, mas a carta e quem a mandou só aparecem se a Jogada for deste grupo.
    /// </summary>
    private static async Task<Dictionary<Guid, FadigaMembroResponse>> LoadFadigasAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        IReadOnlyList<MembroCena> membros,
        CancellationToken cancellationToken)
    {
        var nomes = membros.ToDictionary(m => m.UsuarioId, m => m.NomeExibicao);
        var fadigas = new Dictionary<Guid, FadigaMembroResponse>();
        foreach (var membro in membros.Where(m => m.ModoJogo))
        {
            var jogadas = await Jogadas.LoadAsync(connection, null, membro.UsuarioId, membro.UsuarioId, cancellationToken);
            if (!jogadas.Any(j => Fadiga.GetGatilho(j, membro.UsuarioId) is not null))
            {
                continue;
            }

            var dias = await EstadoJogo.LoadDiasVerificadosAsync(connection, null, membro.UsuarioId, cancellationToken);
            var fadiga = Fadiga.Calcular(jogadas, dias, membro.UsuarioId);
            if (fadiga.Restantes == 0)
            {
                continue;
            }

            var jogada = jogadas.FirstOrDefault(j => j.Id == fadiga.Causa?.JogadaId && j.GrupoId == grupoId);
            fadigas[membro.UsuarioId] = new FadigaMembroResponse(
                fadiga.Restantes,
                fadiga.Causa?.Motivo.ToString(),
                jogada is null ? null : GetNomeCarta(jogada),
                jogada is null ? null : nomes.GetValueOrDefault(jogada.RemetenteId));
        }

        return fadigas;
    }

    private static string? GetNomeCarta(JogadaCarregada jogada)
    {
        var nome = CatalogoCartas.Find(jogada.CodigoCarta)?.Nome;
        var fusao = jogada.Estado.CodigoFusao is { } codigo ? CatalogoCartas.Find(codigo)?.Nome : null;
        return fusao is null ? nome : $"{nome} + {fusao}";
    }

    /// <summary>
    /// Os membros ativos. <c>AcordadoEm</c> é o início da última atividade deste grupo que não foi rejeitada
    /// nem anulada, ou a entrada no grupo, o que for mais recente (GREATEST ignora a falta de atividade).
    /// </summary>
    private static async Task<IReadOnlyList<MembroCena>> LoadMembrosAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT m.usuario_id, u.nome_exibicao, (m.joined_at AT TIME ZONE g.fuso_horario)::date,
                u.modo_inicio = 'Jogo',
                {MembrosDormindo.AcordadoEmSql("m")}
            FROM membros_grupo m
            JOIN grupos g ON g.id = m.grupo_id
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $1 AND m.removed_at IS NULL
            ORDER BY m.joined_at, m.usuario_id
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        var membros = new List<MembroCena>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            membros.Add(new MembroCena(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetFieldValue<DateOnly>(2),
                reader.GetBoolean(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return membros;
    }

    private sealed record CutucadasRecebidas(int Total, string UltimoTipo, DateTimeOffset? PorMimEm);

    /// <summary>As cutucadas das últimas 24 horas por quem recebeu (decisão 0063).</summary>
    private static async Task<Dictionary<Guid, CutucadasRecebidas>> LoadCutucadasAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT destinatario_id, count(*)::int, (array_agg(tipo ORDER BY created_at DESC, id))[1],
                max(created_at) FILTER (WHERE autor_id = $2)
            FROM cutucadas
            WHERE grupo_id = $1 AND created_at > now() - interval '24 hours'
            GROUP BY destinatario_id
            """, connection);
        command.Parameters.AddWithValue(grupoId);
        command.Parameters.AddWithValue(usuarioId);

        var cutucadas = new Dictionary<Guid, CutucadasRecebidas>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cutucadas[reader.GetGuid(0)] = new CutucadasRecebidas(
                reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : TimestampLeitura.FromReader(reader, 3));
        }

        return cutucadas;
    }

    /// <summary>Quem treina ao vivo agora neste grupo (ADR 0078): o Avatar treina e o grupo pode torcer.</summary>
    private static async Task<Dictionary<Guid, TreinoAoVivoResponse>> LoadTreinosAoVivoAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.usuario_id, a.id, a.tipo, a.started_at,
                (SELECT count(*)::int FROM torcidas t WHERE t.atividade_id = a.id),
                (SELECT max(t.created_at) FROM torcidas t WHERE t.atividade_id = a.id AND t.usuario_id = $2),
                (SELECT p.distancia_metros FROM percursos p WHERE p.atividade_id = a.id)
            FROM atividades a
            WHERE a.grupo_id = $1 AND a.situacao_verificacao = 'EmAndamento'
            """, connection);
        command.Parameters.AddWithValue(grupoId);
        command.Parameters.AddWithValue(usuarioId);

        var treinos = new Dictionary<Guid, TreinoAoVivoResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            treinos[reader.GetGuid(0)] = new TreinoAoVivoResponse(
                reader.GetGuid(1),
                reader.GetString(2),
                TimestampLeitura.FromReader(reader, 3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? null : TimestampLeitura.FromReader(reader, 5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6));
        }

        return treinos;
    }

    /// <summary>Quem tem atividade ainda sem veredito neste grupo, na semana corrente (segunda a domingo, fuso do grupo).</summary>
    private static async Task<HashSet<Guid>> LoadComAtividadePendenteAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT DISTINCT a.usuario_id
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.grupo_id = $1
              AND a.situacao_verificacao IN ('AguardandoFeedback', 'EmRevisao')
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date
                  >= date_trunc('week', now() AT TIME ZONE g.fuso_horario)::date
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date
                  < date_trunc('week', now() AT TIME ZONE g.fuso_horario)::date + 7
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        var ids = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>Quem já cumpriu a meta da semana do desafio em destaque. Sem desafio em curso, ninguém.</summary>
    private static async Task<HashSet<Guid>> LoadComMetaCumpridaAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        IReadOnlyList<MembroCena> membros,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        var destacado = await DesafioDestaqueGrupo.LoadAsync(connection, grupoId, cancellationToken);
        if (destacado is null)
        {
            return ids;
        }

        var semana = CalculoSemanaDesafio.GetSemana(destacado.DataInicio, destacado.DataFim, destacado.Hoje);
        if (semana is null)
        {
            return ids;
        }

        await using var command = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildAgregadoPorUsuarioSemanaAtual(),
            connection);
        command.Parameters.AddWithValue(destacado.Id);
        command.Parameters.AddWithValue(semana.Inicio);
        command.Parameters.AddWithValue(semana.Fim);

        var contagens = new Dictionary<Guid, (int Verificadas, int Pendentes)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                contagens[reader.GetGuid(0)] = (reader.GetInt32(1), reader.GetInt32(2));
            }
        }

        foreach (var membro in membros)
        {
            if (!CalculoSemanaDesafio.ParticipaDaSemana(semana.Inicio, membro.EntradaLocal)
                || !contagens.TryGetValue(membro.UsuarioId, out var contagem))
            {
                continue;
            }

            if (CalculoSemanaDesafio.CalculateProgresso(
                    contagem.Verificadas, contagem.Pendentes, destacado.SessoesPorSemana).MetaCumprida)
            {
                ids.Add(membro.UsuarioId);
            }
        }

        return ids;
    }
}
