using Npgsql;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Jogo;

/// <summary>SQL compartilhado das Convocações (decisão 0091): carrega os fatos e calcula as situações.</summary>
internal static class Convocacoes
{
    /// <summary>As Convocações em que a pessoa foi convidada (como anfitrião ou não), em qualquer grupo.</summary>
    public static Task<List<ConvocacaoCarregada>> LoadDoUsuarioAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken) =>
        LoadAsync(
            connection,
            transaction,
            "c.id IN (SELECT convocacao_id FROM participantes_convocacao WHERE usuario_id = $1)",
            [usuarioId],
            null,
            cancellationToken);

    /// <summary>As Convocações mais recentes do grupo, da mais nova para a mais antiga.</summary>
    public static Task<List<ConvocacaoCarregada>> LoadDoGrupoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid grupoId,
        int limite,
        CancellationToken cancellationToken) =>
        LoadAsync(connection, transaction, "c.grupo_id = $1", [grupoId], limite, cancellationToken);

    public static async Task<ConvocacaoCarregada?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid grupoId,
        Guid convocacaoId,
        CancellationToken cancellationToken) =>
        (await LoadAsync(
            connection, transaction, "c.grupo_id = $1 AND c.id = $2", [grupoId, convocacaoId], null, cancellationToken))
        .FirstOrDefault();

    /// <summary>Se o grupo tem uma Convocação dentro do prazo. A que só espera verificação não conta.</summary>
    public static async Task<bool> ExistsAtivaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM convocacoes WHERE grupo_id = $1 AND expira_em > now())",
            connection,
            transaction);
        command.Parameters.AddWithValue(grupoId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<List<ConvocacaoCarregada>> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string filtro,
        Guid[] parametros,
        int? limite,
        CancellationToken cancellationToken)
    {
        var alvo = $"""
            WITH alvo AS (
                SELECT c.id FROM convocacoes c
                WHERE {filtro}
                ORDER BY c.created_at DESC, c.id
                {(limite is { } l ? $"LIMIT {l}" : "")})
            """;

        var convocacoes = new List<(Guid Id, Guid GrupoId, Guid AnfitriaoId, Guid CartaId, string Codigo, int Preco, string Tipo,
            DateTimeOffset CriadaEm, DateTimeOffset ExpiraEm)>();
        DateTimeOffset agora;
        await using (var command = new NpgsqlCommand($"""
            {alvo}
            SELECT c.id, c.grupo_id, c.anfitriao_id, c.carta_id, ca.codigo, ca.preco, c.tipo_atividade, c.created_at,
                c.expira_em
            FROM convocacoes c
            JOIN cartas ca ON ca.id = c.carta_id
            WHERE c.id IN (SELECT id FROM alvo)
            ORDER BY c.created_at DESC, c.id
            """, connection, transaction))
        {
            AddParametros(command, parametros);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                convocacoes.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetInt32(5),
                    reader.GetString(6),
                    TimestampLeitura.FromReader(reader, 7),
                    TimestampLeitura.FromReader(reader, 8)));
            }
        }

        if (convocacoes.Count == 0)
        {
            return [];
        }

        await using (var command = new NpgsqlCommand("SELECT now()", connection, transaction))
        {
            agora = TimestampLeitura.FromScalar((await command.ExecuteScalarAsync(cancellationToken))!);
        }

        var participantes = new List<(Guid ConvocacaoId, ParticipanteConvocacao Participante)>();
        await using (var command = new NpgsqlCommand($"""
            {alvo}
            SELECT p.convocacao_id, p.usuario_id, u.nome_exibicao, p.decisao, p.decidida_em, m.removed_at
            FROM participantes_convocacao p
            JOIN convocacoes c ON c.id = p.convocacao_id
            JOIN usuarios u ON u.id = p.usuario_id
            LEFT JOIN membros_grupo m ON m.grupo_id = c.grupo_id AND m.usuario_id = p.usuario_id
            WHERE p.convocacao_id IN (SELECT id FROM alvo)
            ORDER BY p.convocacao_id, u.nome_exibicao, p.usuario_id
            """, connection, transaction))
        {
            AddParametros(command, parametros);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                participantes.Add((reader.GetGuid(0), new ParticipanteConvocacao(
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : TimestampLeitura.FromReader(reader, 4),
                    reader.IsDBNull(5) ? null : TimestampLeitura.FromReader(reader, 5))));
            }
        }

        var atividades = new List<(Guid ConvocacaoId, AtividadeConvocacao Atividade)>();
        await using (var command = new NpgsqlCommand($"""
            {alvo}
            SELECT p.convocacao_id, a.usuario_id, a.situacao_verificacao, a.verified_at,
                (SELECT max(h.created_at) FROM historico_revisao h
                 WHERE h.atividade_id = a.id AND h.decisao = 'Rejeitada')
            FROM participantes_convocacao p
            JOIN convocacoes c ON c.id = p.convocacao_id
            JOIN atividades a ON a.usuario_id = p.usuario_id AND a.grupo_id = c.grupo_id
                AND a.tipo = c.tipo_atividade AND a.started_at >= c.created_at AND a.started_at < c.expira_em
            WHERE p.convocacao_id IN (SELECT id FROM alvo)
            ORDER BY a.created_at, a.id
            """, connection, transaction))
        {
            AddParametros(command, parametros);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                atividades.Add((reader.GetGuid(0), new AtividadeConvocacao(
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : TimestampLeitura.FromReader(reader, 3),
                    reader.IsDBNull(4) ? null : TimestampLeitura.FromReader(reader, 4))));
            }
        }

        return convocacoes
            .Select(c => SituacaoConvocacao.Avaliar(
                new ConvocacaoFatos(
                    c.Id,
                    c.GrupoId,
                    c.AnfitriaoId,
                    c.CartaId,
                    c.Codigo,
                    c.Preco,
                    c.Tipo,
                    c.CriadaEm,
                    c.ExpiraEm,
                    participantes.Where(p => p.ConvocacaoId == c.Id).Select(p => p.Participante).ToList(),
                    atividades.Where(a => a.ConvocacaoId == c.Id).Select(a => a.Atividade).ToList()),
                agora))
            .ToList();
    }

    /// <summary>A Convocação do ponto de vista de quem consulta: a devolução só aparece para o anfitrião.</summary>
    public static ConvocacaoResponse ToResponse(ConvocacaoCarregada convocacao, Guid usuarioId)
    {
        var fatos = convocacao.Fatos;
        var anfitriao = usuarioId == fatos.AnfitriaoId;
        var minha = convocacao.Get(usuarioId);
        var outros = fatos.Participantes.Count - 1;
        return new ConvocacaoResponse(
            fatos.Id,
            fatos.GrupoId,
            fatos.Codigo,
            CatalogoCartas.Find(fatos.Codigo)?.Nome ?? fatos.Codigo,
            fatos.TipoAtividade,
            fatos.AnfitriaoId,
            convocacao.Get(fatos.AnfitriaoId)?.Participante.NomeExibicao ?? "",
            fatos.CriadaEm,
            fatos.ExpiraEm,
            convocacao.Situacao,
            convocacao.Concluidas,
            minha?.Situacao,
            convocacao.GetRecompensa(usuarioId),
            anfitriao ? convocacao.Devolucao : null,
            anfitriao && outros > 0 ? fatos.Preco / outros : null,
            !anfitriao && minha?.Situacao == SituacaoConvocacao.Participando,
            convocacao.Resultados
                .Select(r => new ParticipanteConvocacaoResponse(
                    r.UsuarioId, r.Participante.NomeExibicao, r.Situacao, r.UsuarioId == fatos.AnfitriaoId))
                .ToList());
    }

    private static void AddParametros(NpgsqlCommand command, Guid[] parametros)
    {
        foreach (var parametro in parametros)
        {
            command.Parameters.AddWithValue(parametro);
        }
    }
}
