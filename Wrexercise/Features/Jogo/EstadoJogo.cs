using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Jogo;

internal sealed record CartaComprada(Guid Id, string Codigo, int Preco, DateTimeOffset CompradaEm);

/// <summary>
/// A Energia, as cartas e as Jogadas de uma pessoa, tudo calculado na leitura (docs/gamificacao.md §5.3).
/// Dentro de uma transação que travou a linha do usuário, o saldo fica estável até o fim dela.
/// </summary>
internal sealed record EstadoJogo(
    Guid UsuarioId,
    EnergiaGanha Ganha,
    IReadOnlyList<CartaComprada> Cartas,
    IReadOnlyList<JogadaCarregada> Jogadas)
{
    /// <summary>Usar uma carta não muda o saldo: o custo entrou na compra.</summary>
    public int Saldo => Ganha.Total - Cartas.Sum(c => c.Preco);

    /// <summary>As cartas não gastas, inclusive as que estão em Jogadas abertas (docs/gamificacao.md §6.2).</summary>
    public IReadOnlyList<CartaComprada> Mao => Cartas.Where(c => !IsGasta(c.Id)).ToList();

    /// <summary>
    /// Uma Provocação só é gasta quando a Jogada fica <c>Concluida</c>; uma Resposta, quando é usada
    /// (docs/gamificacao.md §6.2). A Jogada devolvida pelo DESAFIO DUPLO usa a Provocação original, mas não a gasta.
    /// </summary>
    public bool IsGasta(Guid cartaId) =>
        Jogadas.Any(j => j.CartaRespostaId == cartaId
            || (j.CartaId == cartaId && j.IsCartaDoRemetente && j.Situacao == SituacaoJogada.Concluida));

    public bool IsEmJogo(Guid cartaId) =>
        Jogadas.Any(j => j.CartaId == cartaId && j.IsCartaDoRemetente && SituacaoJogada.IsAberta(j.Situacao));

    /// <summary>
    /// Trava as linhas dos usuários até o fim da transação, sempre na mesma ordem, para evitar deadlock.
    /// Leia o estado em comandos seguintes, que já enxergam o que a transação anterior gravou.
    /// </summary>
    public static async Task LockUsuariosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<Guid> usuarioIds,
        CancellationToken cancellationToken)
    {
        foreach (var usuarioId in usuarioIds.Distinct().Order())
        {
            // NO KEY não bloqueia quem só referencia o usuário, como o registro de uma atividade.
            await using var command = new NpgsqlCommand(
                "SELECT 1 FROM usuarios WHERE id = $1 FOR NO KEY UPDATE",
                connection,
                transaction);
            command.Parameters.AddWithValue(usuarioId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task<EstadoJogo> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var atividades = await LoadAtividadesAsync(connection, transaction, usuarioId, cancellationToken);
        var desafios = await LoadDesafiosAsync(connection, transaction, usuarioId, cancellationToken);
        var jogadas = await Jogo.Jogadas.LoadAsync(connection, transaction, usuarioId, usuarioId, cancellationToken);
        var cartas = await LoadCartasAsync(connection, transaction, usuarioId, cancellationToken);

        var recompensas = jogadas
            .Where(j => j.Situacao == SituacaoJogada.Concluida)
            .Select(j => new RecompensaJogada(
                j.Preco,
                j.DestinatarioId == usuarioId,
                j.CodigoResposta == CodigoCarta.Aposta))
            .ToList();

        return new EstadoJogo(usuarioId, CalculoEnergia.Calcular(atividades, desafios, recompensas), cartas, jogadas);
    }

    private static async Task<List<CartaComprada>> LoadCartasAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, codigo, preco, created_at
            FROM cartas
            WHERE usuario_id = $1
            ORDER BY created_at, id
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);

        var cartas = new List<CartaComprada>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cartas.Add(new CartaComprada(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                TimestampLeitura.FromReader(reader, 3)));
        }

        return cartas;
    }

    /// <summary>
    /// As atividades não rejeitadas, inclusive de grupos que a pessoa deixou, em ordem de registro
    /// (decisão 0034): uma atividade retroativa não toma o bônus de outra já verificada.
    /// </summary>
    private static async Task<List<AtividadeEnergia>> LoadAtividadesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.started_at AT TIME ZONE g.fuso_horario, a.tipo, a.situacao_verificacao = 'Verificada'
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.usuario_id = $1 AND a.situacao_verificacao <> 'Rejeitada'
            ORDER BY a.created_at, a.id
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);

        var atividades = new List<AtividadeEnergia>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            atividades.Add(new AtividadeEnergia(reader.GetDateTime(0), reader.GetString(1), reader.GetBoolean(2)));
        }

        return atividades;
    }

    /// <summary>
    /// Os desafios já iniciados dos grupos de que a pessoa é ou foi membro, com as semanas em que
    /// cumpriu a meta, pelas mesmas regras de elegibilidade da classificação.
    /// </summary>
    private static async Task<List<DesafioEnergia>> LoadDesafiosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var desafios = new Dictionary<Guid, (DateOnly Inicio, DateOnly Fim, DateOnly Entrada, bool Encerrado)>();
        await using (var command = new NpgsqlCommand("""
            SELECT d.id, d.data_inicio, d.data_fim,
                (m.joined_at AT TIME ZONE g.fuso_horario)::date,
                (now() AT TIME ZONE g.fuso_horario)::date > d.data_fim
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = $1
            WHERE d.cancelled_at IS NULL
              AND d.data_inicio <= (now() AT TIME ZONE g.fuso_horario)::date
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                desafios[reader.GetGuid(0)] = (
                    reader.GetFieldValue<DateOnly>(1),
                    reader.GetFieldValue<DateOnly>(2),
                    reader.GetFieldValue<DateOnly>(3),
                    reader.GetBoolean(4));
            }
        }

        var cumpridas = desafios.Keys.ToDictionary(id => id, _ => new List<int>());
        await using (var command = new NpgsqlCommand($"""
            SELECT d.id, {ContagensAtividadeDesafio.IndiceSemanaSql}
            FROM atividades a
            JOIN desafios d ON d.grupo_id = a.grupo_id AND d.cancelled_at IS NULL
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = a.usuario_id
            WHERE a.usuario_id = $1
              AND a.situacao_verificacao = 'Verificada'
              AND a.duracao_segundos >= d.duracao_minima_segundos
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date >= d.data_inicio
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date <= d.data_fim
              AND d.data_inicio + {ContagensAtividadeDesafio.IndiceSemanaSql} * 7 >= (m.joined_at AT TIME ZONE g.fuso_horario)::date
            GROUP BY d.id, d.sessoes_por_semana, {ContagensAtividadeDesafio.IndiceSemanaSql}
            HAVING count(*) >= d.sessoes_por_semana
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (cumpridas.TryGetValue(reader.GetGuid(0), out var indices))
                {
                    indices.Add(reader.GetInt32(1));
                }
            }
        }

        return desafios
            .Select(d => new DesafioEnergia(d.Value.Inicio, d.Value.Fim, d.Value.Entrada, d.Value.Encerrado, cumpridas[d.Key]))
            .ToList();
    }
}
