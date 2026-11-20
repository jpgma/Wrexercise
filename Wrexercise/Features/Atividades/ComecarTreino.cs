using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// <c>Latitude</c> e <c>Longitude</c>: onde o treino começou, só quando a pessoa escolheu mostrar o local ao grupo
/// (ADR 0080). Sem essa escolha, o navegador nem as envia.
/// </summary>
internal sealed record ComecarTreinoRequest(string? Tipo, double? Latitude = null, double? Longitude = null);

/// <summary>
/// "Começar treino" (ADR 0078): cria a atividade <c>EmAndamento</c>, com o início agora. Ela não conta para
/// nada, não tem revisores e o grupo a vê ao vivo até a pessoa terminar ou cancelar.
/// </summary>
internal static class ComecarTreino
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        ComecarTreinoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!TipoAtividade.IsValid(request.Tipo))
        {
            return OperacaoAtividade.InvalidData();
        }

        // Coordenadas malformadas não impedem o treino: ele só começa sem local.
        var local = IsLocalValido(request.Latitude, request.Longitude)
            ? (request.Latitude!.Value, request.Longitude!.Value)
            : ((double, double)?)null;

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var (status, atividadeId, repetida) = await ExecutarAsync(
            dataSource, grupoId, usuarioId, request.Tipo!, local, cancellationToken);
        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        var atividade = await ConsultasAtividade.GetDetalheAsync(dataSource, atividadeId, usuarioId, cancellationToken);
        return atividade is null
            ? OperacaoAtividade.AtividadeNaoEncontrada()
            : Results.Json(
                atividade,
                AppJsonContext.Default.AtividadeResponse,
                statusCode: repetida ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static async Task<(AtividadeOperacaoStatus Status, Guid Id, bool Repetida)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid grupoId,
        Guid usuarioId,
        string tipo,
        (double Latitude, double Longitude)? local,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return (AtividadeOperacaoStatus.GrupoNaoEncontrado, default, false);
        }

        // Um toque repetido no mesmo grupo devolve o treino já aberto, como a chave de idempotência do registro.
        if (await BuscarEmAndamentoAsync(connection, transaction, usuarioId, cancellationToken) is { } aberto)
        {
            return aberto.GrupoId == grupoId
                ? (AtividadeOperacaoStatus.Concluida, aberto.Id, true)
                : (AtividadeOperacaoStatus.TreinoEmAndamento, default, false);
        }

        // As mesmas travas do registro: a fila de avaliação e a carta que espera resposta (decisão 0076).
        if (await ConsultasAtividade.ExistemParaAvaliarAsync(connection, transaction, grupoId, usuarioId, cancellationToken))
        {
            return (AtividadeOperacaoStatus.AvaliacoesPendentes, default, false);
        }

        if (await CartasSemResposta.ExistsAsync(connection, transaction, usuarioId, cancellationToken))
        {
            return (AtividadeOperacaoStatus.CartaSemResposta, default, false);
        }

        // A comprovação provisória aceita fotos durante o treino; a pessoa escolhe a final ao terminar.
        await using var insert = new NpgsqlCommand("""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, ao_vivo, latitude, longitude)
            VALUES ($1, $2, $3, $4, now(), $5, 'EmAndamento', true, $6, $7)
            ON CONFLICT (usuario_id) WHERE situacao_verificacao = 'EmAndamento' DO NOTHING
            RETURNING id
            """, connection, transaction);
        insert.Parameters.AddWithValue(grupoId);
        insert.Parameters.AddWithValue(usuarioId);
        insert.Parameters.AddWithValue(tipo);
        insert.Parameters.AddWithValue(TreinoAoVivo.DuracaoProvisoriaSegundos);
        insert.Parameters.AddWithValue(TipoComprovacao.Fotografia);
        insert.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Double, (object?)local?.Latitude ?? DBNull.Value);
        insert.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Double, (object?)local?.Longitude ?? DBNull.Value);
        if (await insert.ExecuteScalarAsync(cancellationToken) is not Guid id)
        {
            // Outro toque ao mesmo tempo abriu o treino primeiro.
            return (AtividadeOperacaoStatus.TreinoEmAndamento, default, false);
        }

        await NotificacoesTreino.NotificarComecouTreinarAsync(connection, transaction, id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return (AtividadeOperacaoStatus.Concluida, id, false);
    }

    /// <summary>As duas coordenadas ou nenhuma, dentro dos limites; (0, 0) é o GPS sem sinal.</summary>
    public static bool IsLocalValido(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90
        && longitude is >= -180 and <= 180
        && !(latitude == 0 && longitude == 0);

    private static async Task<(Guid Id, Guid GrupoId)?> BuscarEmAndamentoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, grupo_id FROM atividades WHERE usuario_id = $1 AND situacao_verificacao = 'EmAndamento'
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetGuid(0), reader.GetGuid(1)) : null;
    }
}
