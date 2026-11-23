using Microsoft.Net.Http.Headers;
using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

internal sealed record ParcialResponse(int Metros, int Segundos);

/// <summary>
/// Os números do Percurso. Velocidades em m/s; o navegador mostra o ritmo (min/km) ou a velocidade (km/h) conforme o
/// tipo. <c>GanhoElevacaoMetros</c> é aproximado e falta quando o GPS não mandou altitude.
/// </summary>
internal sealed record ResumoPercursoResponse(
    int DistanciaMetros,
    int DuracaoSegundos,
    double? VelocidadeMedia,
    double? VelocidadeMaxima,
    int? GanhoElevacaoMetros,
    IReadOnlyList<ParcialResponse> Parciais,
    string? Recorde);

internal sealed record BboxResponse(double LatMin, double LonMin, double LatMax, double LonMax);

/// <summary>Quem treinou junto, com o percurso dele, aparado como o de todos.</summary>
internal sealed record ParceiroPercursoResponse(Guid UsuarioId, string NomeExibicao, double[][][] Segmentos);

/// <summary>
/// O Percurso de uma atividade (ADR 0084). <c>Segmentos</c>: trechos de pontos <c>[latitude, longitude]</c>, já
/// simplificados; entre dois trechos houve uma lacuna. O autor vê tudo; o grupo vê sem os primeiros e os últimos 200
/// metros, e vazio quando o percurso inteiro fica perto do começo ou do fim.
/// </summary>
internal sealed record PercursoResponse(
    Guid AtividadeId,
    string Tipo,
    bool AoVivo,
    bool Voce,
    ResumoPercursoResponse Resumo,
    double[][][] Segmentos,
    BboxResponse? Bbox,
    ParceiroPercursoResponse? Parceiro,
    int Torcidas);

internal static class ObterPercurso
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        // A imagem do percurso é vista pelo grupo: o autor pede o percurso aparado, como o grupo o vê.
        var aparado = context.Request.Query["aparado"] == "true";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await CarregarAsync(connection, id, usuarioId, cancellationToken, aparado) is not { } percurso)
        {
            return OperacaoAtividade.ToResult(AtividadeOperacaoStatus.PercursoNaoEncontrado);
        }

        var visiveis = percurso.Visiveis;
        var parceiro = await BuscarParceiroAsync(connection, percurso, cancellationToken);
        var torcidas = await ContarTorcidasAsync(connection, id, cancellationToken);
        var calculo = percurso.Calculo;
        var duracao = percurso.DuracaoSegundos;

        var resposta = new PercursoResponse(
            id,
            percurso.Tipo,
            percurso.AoVivo,
            percurso.Voce,
            new ResumoPercursoResponse(
                calculo.DistanciaMetros,
                duracao,
                duracao > 0 && calculo.DistanciaMetros > 0 ? (double)calculo.DistanciaMetros / duracao : null,
                calculo.VelocidadeMaxima,
                calculo.GanhoElevacaoMetros,
                calculo.Parciais
                    .Select(p => new ParcialResponse(p.Metros, p.Segundos))
                    .ToList(),
                percurso.Recorde),
            ToCoordenadas(PercursoAtividade.Simplificar(visiveis)),
            PercursoAtividade.Bbox(visiveis) is { } b ? new BboxResponse(b.LatMin, b.LonMin, b.LatMax, b.LonMax) : null,
            parceiro,
            torcidas);

        // Ao vivo, o percurso muda a cada lote; depois, quase nunca (só numa correção da duração).
        context.Response.Headers[HeaderNames.CacheControl] = percurso.AoVivo ? "no-store" : "private, max-age=60";
        return Results.Json(resposta, AppJsonContext.Default.PercursoResponse);
    }

    /// <summary>O percurso como quem consulta o vê. Sem acesso à atividade ou sem percurso, <c>null</c>.</summary>
    internal sealed record PercursoCarregado(
        Guid AtividadeId,
        Guid GrupoId,
        Guid AutorId,
        string Tipo,
        bool AoVivo,
        bool Voce,
        DateTimeOffset StartedAt,
        int DuracaoSegundos,
        CalculoPercurso Calculo,
        IReadOnlyList<SegmentoPercurso> Visiveis,
        string? Recorde);

    internal static async Task<PercursoCarregado?> CarregarAsync(
        NpgsqlConnection connection,
        Guid atividadeId,
        Guid usuarioId,
        CancellationToken cancellationToken,
        bool aparado = false)
    {
        Guid grupoId;
        Guid autorId;
        string tipo;
        string situacao;
        DateTimeOffset startedAt;
        int duracao;
        PercursoResumoResponse? resumo;
        await using (var command = new NpgsqlCommand($"""
            SELECT a.grupo_id, a.usuario_id, a.tipo, a.situacao_verificacao, a.started_at, a.duracao_segundos,
                {ConsultasPercurso.ColunasResumo}
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            {ConsultasPercurso.JoinResumo}
            WHERE a.id = $1 AND pc.atividade_id IS NOT NULL
            """, connection))
        {
            command.Parameters.AddWithValue(atividadeId);
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            grupoId = reader.GetGuid(0);
            autorId = reader.GetGuid(1);
            tipo = reader.GetString(2);
            situacao = reader.GetString(3);
            startedAt = reader.GetFieldValue<DateTimeOffset>(4);
            duracao = reader.GetInt32(5);
            resumo = ConsultasPercurso.ReadResumo(reader, 6, tipo, duracao, situacao);
        }

        var agora = DateTimeOffset.UtcNow;
        var aoVivo = situacao == SituacaoVerificacao.EmAndamento;
        if (aoVivo)
        {
            duracao = TreinoAoVivo.Decorridos(startedAt, agora);
        }

        var pontos = await ConsultasPercurso.CarregarPontosAsync(connection, null, atividadeId, cancellationToken);
        var (inicio, fim) = ConsultasPercurso.Periodo(situacao, startedAt, duracao, agora);
        var calculo = PercursoAtividade.Calcular(pontos, tipo, inicio, fim);
        var voce = autorId == usuarioId;
        return new PercursoCarregado(
            atividadeId,
            grupoId,
            autorId,
            tipo,
            aoVivo,
            voce,
            startedAt,
            duracao,
            calculo,
            voce && !aparado ? calculo.Segmentos : PercursoAtividade.Aparar(calculo.Segmentos),
            resumo?.Recorde);
    }

    /// <summary>
    /// O colega do mesmo grupo que treinou ao vivo com percurso ao mesmo tempo, por 15 minutos ou mais, e andou
    /// junto (<see cref="ParceriaPercurso.TreinaramJuntos"/>). Entre vários, o de maior sobreposição.
    /// </summary>
    private static async Task<ParceiroPercursoResponse?> BuscarParceiroAsync(
        NpgsqlConnection connection,
        PercursoCarregado percurso,
        CancellationToken cancellationToken)
    {
        var fimMeu = percurso.StartedAt.AddSeconds(percurso.DuracaoSegundos);
        Guid parceiroAtividadeId;
        Guid parceiroId;
        string nome;
        string situacao;
        DateTimeOffset startedAt;
        int duracao;
        string tipo;
        await using (var command = new NpgsqlCommand("""
            WITH candidatas AS (
                SELECT a.id, a.usuario_id, u.nome_exibicao, a.situacao_verificacao, a.started_at, a.duracao_segundos,
                    a.tipo,
                    CASE WHEN a.situacao_verificacao = 'EmAndamento' THEN now()
                         ELSE a.started_at + make_interval(secs => a.duracao_segundos) END AS fim
                FROM atividades a
                JOIN percursos p ON p.atividade_id = a.id
                JOIN usuarios u ON u.id = a.usuario_id
                JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = a.usuario_id AND m.removed_at IS NULL
                WHERE a.grupo_id = $1 AND a.usuario_id <> $2 AND a.ao_vivo
                  AND a.situacao_verificacao NOT IN ('Rejeitada', 'Anulada')
                  AND a.started_at < $4
                  AND a.started_at > $3 - interval '1 day'
            )
            SELECT id, usuario_id, nome_exibicao, situacao_verificacao, started_at, duracao_segundos, tipo
            FROM candidatas
            WHERE least(fim, $4) - greatest(started_at, $3) >= interval '15 minutes'
            ORDER BY least(fim, $4) - greatest(started_at, $3) DESC
            LIMIT 1
            """, connection))
        {
            command.Parameters.AddWithValue(percurso.GrupoId);
            command.Parameters.AddWithValue(percurso.AutorId);
            command.Parameters.AddWithValue(percurso.StartedAt);
            command.Parameters.AddWithValue(fimMeu);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            parceiroAtividadeId = reader.GetGuid(0);
            parceiroId = reader.GetGuid(1);
            nome = reader.GetString(2);
            situacao = reader.GetString(3);
            startedAt = reader.GetFieldValue<DateTimeOffset>(4);
            duracao = reader.GetInt32(5);
            tipo = reader.GetString(6);
        }

        var pontos = await ConsultasPercurso.CarregarPontosAsync(connection, null, parceiroAtividadeId, cancellationToken);
        var (inicio, fim) = ConsultasPercurso.Periodo(situacao, startedAt, duracao, DateTimeOffset.UtcNow);
        var deles = PercursoAtividade.Filtrar(pontos, tipo, inicio, fim);
        if (!ParceriaPercurso.TreinaramJuntos(percurso.Calculo.Segmentos, deles))
        {
            return null;
        }

        return new ParceiroPercursoResponse(
            parceiroId,
            nome,
            ToCoordenadas(PercursoAtividade.Simplificar(PercursoAtividade.Aparar(deles))));
    }

    private static async Task<int> ContarTorcidasAsync(
        NpgsqlConnection connection,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*)::int FROM torcidas WHERE atividade_id = $1", connection);
        command.Parameters.AddWithValue(atividadeId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>Coordenadas com 6 casas (uns 10 cm): o bastante para o mapa, e a resposta fica menor.</summary>
    private static double[][][] ToCoordenadas(IReadOnlyList<SegmentoPercurso> segmentos) =>
        segmentos
            .Select(s => s.Pontos
                .Select(p => new[] { Math.Round(p.Latitude, 6), Math.Round(p.Longitude, 6) })
                .ToArray())
            .ToArray();
}
