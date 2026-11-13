using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Jogo;

/// <summary>Uma Jogada lida do banco, com a situação já calculada.</summary>
/// <param name="CodigoCarta">A Provocação jogada. O objetivo em vigor fica em <c>Estado.Codigo</c>.</param>
/// <param name="CodigoResposta">A Resposta usada no aceite, se houver.</param>
/// <param name="OrigemId">A Jogada original, quando esta foi criada por um DESAFIO DUPLO.</param>
internal sealed record JogadaCarregada(
    Guid Id,
    Guid GrupoId,
    Guid RemetenteId,
    Guid DestinatarioId,
    Guid CartaId,
    string CodigoCarta,
    int Preco,
    Guid? CartaRespostaId,
    string? CodigoResposta,
    Guid? OrigemId,
    JogadaEstado Estado,
    DateTimeOffset? VistaEm,
    DateTimeOffset? ResultadoVistoEm,
    string Situacao)
{
    public JogadaLimite ToLimite() => new(RemetenteId, DestinatarioId, Estado.EnviadaEm, Situacao);

    /// <summary>
    /// Se a Provocação desta Jogada é de quem enviou. Na Jogada do DESAFIO DUPLO, a carta é a da Jogada original
    /// e continua sendo de quem a comprou.
    /// </summary>
    public bool IsCartaDoRemetente => OrigemId is null;
}

/// <summary>O que fica fixado no objetivo de uma Jogada: a duração-alvo e o tipo a evitar.</summary>
internal sealed record ParametrosObjetivo(int? DuracaoAlvoSegundos, string? TipoEvitar);

/// <param name="OrigemId">A Jogada original, na devolução do DESAFIO DUPLO.</param>
/// <param name="ExpiraLocal">O prazo no horário local do grupo.</param>
internal sealed record NovaJogada(
    Guid GrupoId,
    Guid RemetenteId,
    Guid DestinatarioId,
    Guid CartaId,
    Guid? OrigemId,
    DateTimeOffset EnviadaEm,
    DateTime ExpiraLocal,
    ParametrosObjetivo Parametros);

/// <summary>SQL compartilhado: as Jogadas e as atividades que podem cumpri-las.</summary>
internal static class Jogadas
{
    /// <summary>O objetivo em vigor: o trocado pela MUDANÇA DE PLANOS ou o da carta.</summary>
    public const string ObjetivoSql = "COALESCE(j.codigo_objetivo, c.codigo)";

    /// <summary>
    /// As Jogadas em que alguma das duas pessoas participa, em qualquer grupo. Para uma pessoa só,
    /// passe o mesmo identificador duas vezes.
    /// </summary>
    public static async Task<List<JogadaCarregada>> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioA,
        Guid usuarioB,
        CancellationToken cancellationToken)
    {
        var linhas = new List<(Guid Id, Guid GrupoId, Guid RemetenteId, Guid DestinatarioId, Guid CartaId,
            string CodigoCarta, int Preco, Guid? CartaRespostaId, string? CodigoResposta, Guid? OrigemId,
            JogadaEstado Estado, DateTimeOffset? VistaEm, DateTimeOffset? ResultadoVistoEm)>();
        DateTimeOffset agora;

        await using (var command = new NpgsqlCommand($"""
            SELECT j.id, j.grupo_id, j.remetente_id, j.destinatario_id, j.carta_id, c.preco, {ObjetivoSql},
                j.enviada_em, j.expira_em, j.resposta, j.respondida_em, j.duracao_alvo_segundos, j.tipo_evitar,
                j.vista_em, j.resultado_visto_em, now(), c.codigo, j.carta_resposta_id, cr.codigo, j.origem_id
            FROM jogadas j
            JOIN cartas c ON c.id = j.carta_id
            LEFT JOIN cartas cr ON cr.id = j.carta_resposta_id
            WHERE $1 IN (j.remetente_id, j.destinatario_id) OR $2 IN (j.remetente_id, j.destinatario_id)
            ORDER BY j.enviada_em DESC, j.id
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(usuarioA);
            command.Parameters.AddWithValue(usuarioB);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            agora = DateTimeOffset.UtcNow;
            while (await reader.ReadAsync(cancellationToken))
            {
                agora = TimestampLeitura.FromReader(reader, 15);
                linhas.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetGuid(3),
                    reader.GetGuid(4),
                    reader.GetString(16),
                    reader.GetInt32(5),
                    reader.IsDBNull(17) ? null : reader.GetGuid(17),
                    reader.IsDBNull(18) ? null : reader.GetString(18),
                    reader.IsDBNull(19) ? null : reader.GetGuid(19),
                    new JogadaEstado(
                        reader.GetString(6),
                        TimestampLeitura.FromReader(reader, 7),
                        TimestampLeitura.FromReader(reader, 8),
                        reader.IsDBNull(9) ? null : reader.GetString(9),
                        reader.IsDBNull(10) ? null : TimestampLeitura.FromReader(reader, 10),
                        reader.IsDBNull(11) ? null : reader.GetInt32(11),
                        reader.IsDBNull(12) ? null : reader.GetString(12)),
                    reader.IsDBNull(13) ? null : TimestampLeitura.FromReader(reader, 13),
                    reader.IsDBNull(14) ? null : TimestampLeitura.FromReader(reader, 14)));
            }
        }

        var atividades = await LoadAtividadesAsync(connection, transaction, usuarioA, usuarioB, cancellationToken);

        return linhas
            .Select(l => new JogadaCarregada(
                l.Id,
                l.GrupoId,
                l.RemetenteId,
                l.DestinatarioId,
                l.CartaId,
                l.CodigoCarta,
                l.Preco,
                l.CartaRespostaId,
                l.CodigoResposta,
                l.OrigemId,
                l.Estado,
                l.VistaEm,
                l.ResultadoVistoEm,
                SituacaoJogada.Calcular(
                    l.Estado,
                    atividades.TryGetValue(l.Id, out var candidatas) ? candidatas : [],
                    agora)))
            .ToList();
    }

    /// <summary>
    /// As atividades que podem cumprir cada Jogada aceita: não rejeitadas nem anuladas, do destinatário, no grupo da
    /// Jogada e com início entre o envio e o prazo. O objetivo da carta é conferido em <see cref="ObjetivoCarta"/>.
    /// </summary>
    public static async Task<Dictionary<Guid, List<AtividadeJogada>>> LoadAtividadesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioA,
        Guid usuarioB,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT j.id, a.started_at AT TIME ZONE g.fuso_horario, a.tipo, a.duracao_segundos,
                a.situacao_verificacao = 'Verificada'
            FROM jogadas j
            JOIN grupos g ON g.id = j.grupo_id
            JOIN atividades a ON a.usuario_id = j.destinatario_id AND a.grupo_id = j.grupo_id
            WHERE ($1 IN (j.remetente_id, j.destinatario_id) OR $2 IN (j.remetente_id, j.destinatario_id))
              AND j.resposta = 'Aceita'
              AND a.situacao_verificacao NOT IN ('Rejeitada', 'Anulada')
              AND a.started_at >= j.enviada_em
              AND a.started_at <= j.expira_em
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioA);
        command.Parameters.AddWithValue(usuarioB);

        var atividades = new Dictionary<Guid, List<AtividadeJogada>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var jogadaId = reader.GetGuid(0);
            if (!atividades.TryGetValue(jogadaId, out var lista))
            {
                atividades[jogadaId] = lista = [];
            }

            lista.Add(new AtividadeJogada(reader.GetDateTime(1), reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4)));
        }

        return atividades;
    }

    /// <summary>O instante atual, também no horário local do grupo, de onde saem os prazos.</summary>
    public static async Task<(DateTimeOffset Utc, DateTime Local)> LoadAgoraAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT now(), now() AT TIME ZONE fuso_horario FROM grupos WHERE id = $1",
            connection,
            transaction);
        command.Parameters.AddWithValue(grupoId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (TimestampLeitura.FromReader(reader, 0), reader.GetDateTime(1));
    }

    /// <summary>
    /// Fixa o objetivo para quem vai cumpri-lo (decisão 0035): +10 MIN e +20 MIN guardam a duração mínima do
    /// desafio <c>Ativo</c> mais os minutos da carta; a VARIEDADE guarda o tipo da última atividade verificada
    /// do destinatário neste grupo.
    /// </summary>
    /// <returns>Os parâmetros, ou o erro <c>SEM_DESAFIO_ATIVO</c> numa carta de duração.</returns>
    public static async Task<(ParametrosObjetivo? Parametros, IResult? Erro)> FixarObjetivoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string codigo,
        Guid grupoId,
        Guid destinatarioId,
        CancellationToken cancellationToken)
    {
        int? duracaoAlvo = null;
        if (CatalogoCartas.IsDuracao(codigo))
        {
            duracaoAlvo = await LoadDuracaoMinimaAtivaAsync(connection, grupoId, cancellationToken)
                + (CatalogoCartas.GetMinutosExtras(codigo) * 60);
            if (duracaoAlvo is null)
            {
                return (null, SemDesafioAtivo());
            }
        }

        var tipoEvitar = codigo == CodigoCarta.Variedade
            ? await LoadUltimoTipoVerificadoAsync(connection, transaction, grupoId, destinatarioId, cancellationToken)
            : null;

        return (new ParametrosObjetivo(duracaoAlvo, tipoEvitar), null);
    }

    /// <summary>A duração mínima do desafio em destaque, se ele estiver <c>Ativo</c>.</summary>
    public static async Task<int?> LoadDuracaoMinimaAtivaAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        var desafio = await DesafioDestaqueGrupo.LoadAsync(connection, grupoId, cancellationToken);
        return desafio is not null && desafio.Situacao == SituacaoDesafio.Ativo ? desafio.DuracaoMinimaSegundos : null;
    }

    public static IResult SemDesafioAtivo() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "SEM_DESAFIO_ATIVO",
        "Esta carta só pode ser jogada com um desafio ativo no grupo.");

    /// <summary>Se cada uma das duas pessoas está no modo Jogo: só assim cartas circulam entre elas.</summary>
    public static async Task<(bool A, bool B)> LoadModoJogoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioA,
        Guid usuarioB,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT
                coalesce(bool_or(id = $1 AND modo_inicio = 'Jogo'), false),
                coalesce(bool_or(id = $2 AND modo_inicio = 'Jogo'), false)
            FROM usuarios
            WHERE id IN ($1, $2)
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioA);
        command.Parameters.AddWithValue(usuarioB);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetBoolean(0), reader.GetBoolean(1));
    }

    /// <summary>Grava uma Jogada nova: um envio ou a devolução do DESAFIO DUPLO.</summary>
    public static async Task<(Guid Id, DateTimeOffset ExpiraEm)> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        NovaJogada jogada,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand("""
            INSERT INTO jogadas (
                grupo_id, remetente_id, destinatario_id, carta_id, origem_id, enviada_em, expira_em,
                duracao_alvo_segundos, tipo_evitar)
            SELECT g.id, $2, $3, $4, $5, $6, $7 AT TIME ZONE g.fuso_horario, $8, $9
            FROM grupos g
            WHERE g.id = $1
            RETURNING id, expira_em
            """, connection, transaction);
        insert.Parameters.AddWithValue(jogada.GrupoId);
        insert.Parameters.AddWithValue(jogada.RemetenteId);
        insert.Parameters.AddWithValue(jogada.DestinatarioId);
        insert.Parameters.AddWithValue(jogada.CartaId);
        insert.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)jogada.OrigemId ?? DBNull.Value);
        insert.Parameters.AddWithValue(jogada.EnviadaEm);
        insert.Parameters.AddWithValue(DateTime.SpecifyKind(jogada.ExpiraLocal, DateTimeKind.Unspecified));
        insert.Parameters.AddWithValue(
            NpgsqlDbType.Integer, (object?)jogada.Parametros.DuracaoAlvoSegundos ?? DBNull.Value);
        insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)jogada.Parametros.TipoEvitar ?? DBNull.Value);
        await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetGuid(0), TimestampLeitura.FromReader(reader, 1));
    }

    /// <summary>O tipo da última atividade verificada do destinatário neste grupo, fixado na VARIEDADE.</summary>
    private static async Task<string?> LoadUltimoTipoVerificadoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        Guid destinatarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT tipo
            FROM atividades
            WHERE grupo_id = $1 AND usuario_id = $2 AND situacao_verificacao = 'Verificada'
            ORDER BY started_at DESC, created_at DESC, id
            LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue(grupoId);
        command.Parameters.AddWithValue(destinatarioId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }
}
