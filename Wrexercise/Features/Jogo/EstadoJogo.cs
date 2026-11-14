using Npgsql;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Jogo;

internal sealed record CartaComprada(Guid Id, string Codigo, int Preco, DateTimeOffset CompradaEm);

/// <summary>
/// A Energia, as cartas e as Jogadas de uma pessoa, tudo calculado na leitura (docs/gamificacao.md §5.3).
/// Dentro de uma transação que travou a linha do usuário, o saldo fica estável até o fim dela.
/// </summary>
/// <param name="FadigaRestante">Quantos dos próximos dias verificados ainda rendem metade da base (decisão 0059).</param>
internal sealed record EstadoJogo(
    Guid UsuarioId,
    EnergiaGanha Ganha,
    IReadOnlyList<CartaComprada> Cartas,
    IReadOnlyList<JogadaCarregada> Jogadas,
    int FadigaRestante = 0)
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
        var desafios = await DesafiosDaPessoa.LoadAsync(connection, transaction, usuarioId, cancellationToken);
        var jogadas = await Jogo.Jogadas.LoadAsync(connection, transaction, usuarioId, usuarioId, cancellationToken);
        var cartas = await LoadCartasAsync(connection, transaction, usuarioId, cancellationToken);
        var rivais = await Rivais.LoadAsync(connection, transaction, usuarioId, null, cancellationToken);
        var rivaisVencidos = rivais.Count(r => r.EscolhedorId == usuarioId && r.Situacao == ResultadoRival.Venceu);
        var dias = await LoadDiasVerificadosAsync(connection, transaction, usuarioId, cancellationToken);
        var fadiga = Fadiga.Calcular(
            jogadas.Select(j => Fadiga.GetGatilho(j, usuarioId)).OfType<DateTimeOffset>(),
            dias);

        var recompensas = jogadas
            .Where(j => j.Situacao == SituacaoJogada.Concluida)
            .Select(j => new RecompensaJogada(
                j.Preco,
                j.DestinatarioId == usuarioId,
                j.CodigoResposta == CodigoCarta.Aposta))
            .ToList();

        var ganha = CalculoEnergia.Calcular(atividades, desafios, recompensas, rivaisVencidos, fadiga.DiasComFadiga);
        return new EstadoJogo(usuarioId, ganha, cartas, jogadas, fadiga.Restantes);
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
    /// As atividades não rejeitadas nem anuladas, inclusive de grupos que a pessoa deixou, em ordem de registro
    /// (decisão 0034): uma atividade retroativa não toma o bônus de outra já verificada.
    /// </summary>
    private static async Task<List<AtividadeEnergia>> LoadAtividadesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.started_at AT TIME ZONE g.fuso_horario, a.tipo, a.situacao_verificacao = 'Verificada', a.grupo_id
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.usuario_id = $1 AND a.situacao_verificacao NOT IN ('Rejeitada', 'Anulada')
            ORDER BY a.created_at, a.id
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);

        var atividades = new List<AtividadeEnergia>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            atividades.Add(new AtividadeEnergia(
                reader.GetDateTime(0), reader.GetString(1), reader.GetBoolean(2), reader.GetGuid(3)));
        }

        return atividades;
    }

    /// <summary>
    /// Cada dia com atividade verificada, por grupo, no <c>verified_at</c> da primeira verificada. Inclui as
    /// anuladas depois de verificadas (o <c>verified_at</c> fica): o dia gasta a carga de FADIGA, mas só rende
    /// se ainda tiver uma atividade <c>Verificada</c>.
    /// </summary>
    private static async Task<List<DiaVerificado>> LoadDiasVerificadosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.grupo_id, (a.started_at AT TIME ZONE g.fuso_horario)::date, min(a.verified_at),
                bool_or(a.situacao_verificacao = 'Verificada')
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.usuario_id = $1
              AND a.verified_at IS NOT NULL
              AND a.situacao_verificacao IN ('Verificada', 'Anulada')
            GROUP BY 1, 2
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);

        var dias = new List<DiaVerificado>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dias.Add(new DiaVerificado(
                reader.GetGuid(0),
                reader.GetFieldValue<DateOnly>(1),
                TimestampLeitura.FromReader(reader, 2),
                reader.GetBoolean(3)));
        }

        return dias;
    }
}
