using Microsoft.Net.Http.Headers;
using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O rascunho que preenche o formulário de registro. <c>GrupoId</c> só existe depois de postado.
/// </summary>
internal sealed record RascunhoResponse(
    Guid Id,
    string App,
    string Tipo,
    int DuracaoSegundos,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Situacao,
    Guid? AtividadeId,
    Guid? GrupoId,
    string ImagemUrl);

/// <summary>Leitura do rascunho pela própria pessoa, com a sessão do navegador.</summary>
internal static class ObterRascunho
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var command = dataSource.CreateCommand("""
            SELECT r.app, r.tipo, r.duracao_segundos, r.started_at, r.ended_at, r.expires_at, r.atividade_id, a.grupo_id
            FROM rascunhos_atividade r
            LEFT JOIN atividades a ON a.id = r.atividade_id
            WHERE r.id = $1 AND r.usuario_id = $2
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(usuarioId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ErrosIntegracao.RascunhoNaoEncontrado();
        }

        Guid? atividadeId = reader.IsDBNull(6) ? null : reader.GetGuid(6);
        var resposta = new RascunhoResponse(
            id,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            SituacaoRascunho.Calcular(atividadeId, reader.GetFieldValue<DateTimeOffset>(5), DateTimeOffset.UtcNow),
            atividadeId,
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            $"/api/rascunhos/{id}/imagem");
        return Results.Json(resposta, AppJsonContext.Default.RascunhoResponse);
    }
}

/// <summary>A imagem do resumo do treino, para o formulário mostrar na primeira evidência.</summary>
internal static class ObterImagemRascunho
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var command = dataSource.CreateCommand(
            "SELECT caminho_relativo, tipo_mime FROM rascunhos_atividade WHERE id = $1 AND usuario_id = $2");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(usuarioId);

        string caminho;
        string tipoMime;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ErrosIntegracao.RascunhoNaoEncontrado();
            }

            caminho = reader.GetString(0);
            tipoMime = reader.GetString(1);
        }

        string caminhoCompleto;
        try
        {
            caminhoCompleto = armazenamento.ResolverCaminhoCompleto(caminho);
        }
        catch (EvidenciaInvalidaException)
        {
            return ErrosIntegracao.RascunhoNaoEncontrado();
        }

        if (!File.Exists(caminhoCompleto))
        {
            return ErrosIntegracao.RascunhoNaoEncontrado();
        }

        // O app pode trocar a imagem enquanto o rascunho está pendente: nada de cache longo.
        context.Response.Headers[HeaderNames.CacheControl] = "private, no-cache";
        return Results.File(caminhoCompleto, tipoMime);
    }
}
