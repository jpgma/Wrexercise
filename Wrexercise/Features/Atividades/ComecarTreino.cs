using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// <c>Latitude</c> e <c>Longitude</c>: onde o treino começou, só quando a pessoa escolheu mostrar o local ao grupo
/// (ADR 0080). Sem essa escolha, o navegador nem as envia. <c>GravarPercurso</c>: na Corrida, na Caminhada e no
/// Ciclismo, a pessoa escolheu gravar o Percurso e mostrá-lo ao grupo (ADR 0084); aí o ponto de início não é
/// guardado, porque mostraria o que o percurso esconde.
/// </summary>
internal sealed record ComecarTreinoRequest(
    string? Tipo,
    double? Latitude = null,
    double? Longitude = null,
    bool? GravarPercurso = null);

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

        var gravarPercurso = request.GravarPercurso == true && PercursoAtividade.IsTipoComPercurso(request.Tipo);

        // Coordenadas malformadas não impedem o treino: ele só começa sem local.
        var local = !gravarPercurso && IsLocalValido(request.Latitude, request.Longitude)
            ? (request.Latitude!.Value, request.Longitude!.Value)
            : ((double, double)?)null;

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var (status, atividadeId, repetida) = await ExecutarAsync(
            dataSource, grupoId, usuarioId, request.Tipo!, local, null, cancellationToken, gravarPercurso);
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

    /// <summary>
    /// Começa o treino. Com <paramref name="vinculo"/>, quem começa é um app conectado (ADR 0081): o mesmo
    /// identificador devolve o mesmo treino, e o treino aberto pelo navegador no mesmo grupo passa a ser do app.
    /// <paramref name="gravarPercurso"/>: cria o Percurso vazio (ADR 0084); no treino do app, o Percurso que o app
    /// grava e manda (ADR 0085), também no treino do navegador que o app adota.
    /// </summary>
    internal static async Task<(AtividadeOperacaoStatus Status, Guid Id, bool Repetida)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid grupoId,
        Guid usuarioId,
        string tipo,
        (double Latitude, double Longitude)? local,
        (string App, string ExternalId)? vinculo,
        CancellationToken cancellationToken,
        bool gravarPercurso = false)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return (AtividadeOperacaoStatus.GrupoNaoEncontrado, default, false);
        }

        // O app repete o começo quando não recebeu a resposta: o treino dele já existe, aberto ou terminado.
        if (vinculo is { } doApp
            && await BuscarDoAppAsync(connection, transaction, usuarioId, doApp, cancellationToken) is { } existente)
        {
            return existente.Situacao == SituacaoVerificacao.EmAndamento
                ? (AtividadeOperacaoStatus.Concluida, existente.Id, true)
                : (AtividadeOperacaoStatus.TreinoNaoEmAndamento, default, false);
        }

        // Um toque repetido no mesmo grupo devolve o treino já aberto, como a chave de idempotência do registro.
        if (await BuscarEmAndamentoAsync(connection, transaction, usuarioId, cancellationToken) is { } aberto)
        {
            if (aberto.GrupoId != grupoId)
            {
                return (AtividadeOperacaoStatus.TreinoEmAndamento, default, false);
            }

            if (vinculo is not { } adotar)
            {
                return (AtividadeOperacaoStatus.Concluida, aberto.Id, true);
            }

            // O treino de outro treino do app continua dele; o aberto pelo navegador passa ao app.
            if (aberto.App is not null)
            {
                return (AtividadeOperacaoStatus.TreinoEmAndamento, default, false);
            }

            await using (var vincular = new NpgsqlCommand(
                "UPDATE atividades SET app = $2, app_external_id = $3 WHERE id = $1",
                connection,
                transaction))
            {
                vincular.Parameters.AddWithValue(aberto.Id);
                vincular.Parameters.AddWithValue(adotar.App);
                vincular.Parameters.AddWithValue(adotar.ExternalId);
                await vincular.ExecuteNonQueryAsync(cancellationToken);
            }

            if (gravarPercurso)
            {
                await CriarPercursoAsync(connection, transaction, aberto.Id, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return (AtividadeOperacaoStatus.Concluida, aberto.Id, true);
        }

        // A mesma trava do registro: a fila de avaliação. A carta que espera resposta não impede começar (decisão
        // 0086: "Responder depois do treino"): o treino começa antes do aceite, então nunca faz a carta falhar.
        if (await ConsultasAtividade.ExistemParaAvaliarAsync(connection, transaction, grupoId, usuarioId, cancellationToken))
        {
            return (AtividadeOperacaoStatus.AvaliacoesPendentes, default, false);
        }

        // A comprovação provisória aceita fotos durante o treino; a pessoa escolhe a final ao terminar.
        await using var insert = new NpgsqlCommand("""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, ao_vivo, latitude, longitude, app, app_external_id)
            VALUES ($1, $2, $3, $4, now(), $5, 'EmAndamento', true, $6, $7, $8, $9)
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
        insert.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)vinculo?.App ?? DBNull.Value);
        insert.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)vinculo?.ExternalId ?? DBNull.Value);
        if (await insert.ExecuteScalarAsync(cancellationToken) is not Guid id)
        {
            // Outro toque ao mesmo tempo abriu o treino primeiro.
            return (AtividadeOperacaoStatus.TreinoEmAndamento, default, false);
        }

        if (gravarPercurso)
        {
            await CriarPercursoAsync(connection, transaction, id, cancellationToken);
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

    private static async Task<(Guid Id, Guid GrupoId, string? App)?> BuscarEmAndamentoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, grupo_id, app FROM atividades WHERE usuario_id = $1 AND situacao_verificacao = 'EmAndamento'
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    private static async Task<(Guid Id, string Situacao)?> BuscarDoAppAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        (string App, string ExternalId) vinculo,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, situacao_verificacao FROM atividades
            WHERE usuario_id = $1 AND app = $2 AND app_external_id = $3
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(vinculo.App);
        command.Parameters.AddWithValue(vinculo.ExternalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetGuid(0), reader.GetString(1)) : null;
    }

    /// <summary>O Percurso vazio do treino; o que já existe (o navegador gravando) fica como está.</summary>
    private static async Task CriarPercursoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var percurso = new NpgsqlCommand(
            "INSERT INTO percursos (atividade_id) VALUES ($1) ON CONFLICT (atividade_id) DO NOTHING",
            connection,
            transaction);
        percurso.Parameters.AddWithValue(atividadeId);
        await percurso.ExecuteNonQueryAsync(cancellationToken);
    }
}
