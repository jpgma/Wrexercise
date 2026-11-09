using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

/// <summary>O que ainda espera a reação de quem consulta. A janela de 48 horas limita o tamanho da fila.</summary>
internal static class ListarParaAvaliar
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, null, grupoId, usuarioId, cancellationToken) is null)
        {
            return OperacaoAtividade.ToResult(AtividadeOperacaoStatus.GrupoNaoEncontrado);
        }

        await using var command = new NpgsqlCommand($"""
            {ConsultasAtividade.SelectResumo}
            WHERE {ConsultasAtividade.FiltroParaAvaliar}
            ORDER BY a.submitted_at, a.id
            """, connection);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(grupoId);

        var atividades = new List<AtividadeResumoResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            atividades.Add(ConsultasAtividade.ReadResumo(reader));
        }

        return Results.Json(new FilaAvaliacaoResponse(atividades), AppJsonContext.Default.FilaAvaliacaoResponse);
    }
}
