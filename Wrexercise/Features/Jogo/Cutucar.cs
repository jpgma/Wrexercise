using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <param name="Texto">Só com o tipo <c>Livre</c>: até 80 caracteres, numa linha (decisão 0064).</param>
internal sealed record CutucarRequest(Guid? DestinatarioId, string? Tipo, string? Texto = null);

internal sealed record CutucadaResponse(Guid Id, string Tipo, DateTimeOffset EnviadaEm, string? Texto = null);

/// <summary>
/// Cutuca um colega que dorme no grupo, com uma mensagem pronta (decisão 0063). Grátis, uma por par a cada
/// 24 horas. Quem foi cutucado recebe uma notificação com push.
/// </summary>
internal static class Cutucar
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        CutucarRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        if (request.DestinatarioId is not { } destinatarioId
            || destinatarioId == usuarioId
            || GetTipoETexto(request) is not { } escolhida)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var (tipo, texto) = escolhida;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        // Trava o vínculo de quem recebe: dois pedidos ao mesmo tempo não passam juntos pelo limite.
        bool dormindo;
        await using (var alvo = new NpgsqlCommand($"""
            SELECT {MembrosDormindo.DormindoSql("m")}
            FROM membros_grupo m
            WHERE m.grupo_id = $1 AND m.usuario_id = $2 AND m.removed_at IS NULL
            FOR NO KEY UPDATE OF m
            """, connection, transaction))
        {
            alvo.Parameters.AddWithValue(grupoId);
            alvo.Parameters.AddWithValue(destinatarioId);
            if (await alvo.ExecuteScalarAsync(cancellationToken) is not bool valor)
            {
                return ErrosJogo.MembroNaoEncontrado();
            }

            dormindo = valor;
        }

        if (!dormindo)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "MEMBRO_ACORDADO",
                "Só dá para cutucar quem está dormindo.");
        }

        await using (var recente = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM cutucadas
                WHERE grupo_id = $1 AND autor_id = $2 AND destinatario_id = $3
                  AND created_at > now() - interval '24 hours')
            """, connection, transaction))
        {
            recente.Parameters.AddWithValue(grupoId);
            recente.Parameters.AddWithValue(usuarioId);
            recente.Parameters.AddWithValue(destinatarioId);
            if ((bool)(await recente.ExecuteScalarAsync(cancellationToken))!)
            {
                return ApiProblem.Create(
                    StatusCodes.Status409Conflict,
                    "CUTUCADA_RECENTE",
                    "Você já cutucou esta pessoa nas últimas 24 horas.");
            }
        }

        Guid id;
        DateTimeOffset enviadaEm;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO cutucadas (grupo_id, autor_id, destinatario_id, tipo, texto)
            VALUES ($1, $2, $3, $4, $5)
            RETURNING id, created_at
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(grupoId);
            insert.Parameters.AddWithValue(usuarioId);
            insert.Parameters.AddWithValue(destinatarioId);
            insert.Parameters.AddWithValue(tipo);
            insert.Parameters.AddWithValue((object?)texto ?? DBNull.Value);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            id = reader.GetGuid(0);
            enviadaEm = TimestampLeitura.FromReader(reader, 1);
        }

        await NotificacoesCutucada.NotificarAsync(connection, transaction, id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            new CutucadaResponse(id, tipo, enviadaEm, texto),
            AppJsonContext.Default.CutucadaResponse,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Uma mensagem pronta, sem texto, ou o tipo <c>Livre</c> com um texto válido. Qualquer outra combinação é inválida.
    /// </summary>
    private static (string Tipo, string? Texto)? GetTipoETexto(CutucarRequest request)
    {
        if (request.Tipo == TiposCutucada.Livre)
        {
            return ValidacaoCutucada.NormalizeTexto(request.Texto) is { } texto ? (TiposCutucada.Livre, texto) : null;
        }

        return TiposCutucada.Find(request.Tipo) is { } pronta && string.IsNullOrWhiteSpace(request.Texto)
            ? (pronta.Codigo, null)
            : null;
    }
}
