using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// A prévia do TODO MUNDO (decisão 0091): os tipos, quantos colegas serão convidados, as recompensas e o motivo,
/// se houver, que impede convocar agora. Usa a mesma conferência de <see cref="Convocar"/> e não grava nada.
/// </summary>
internal static class PreverConvocacao
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        Guid? cartaId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (cartaId is not { } carta)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var (convidados, erro, bloqueio) =
            await Convocar.ValidarAsync(connection, transaction, grupoId, usuarioId, carta, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var preco = CatalogoCartas.Find(CodigoCarta.TodoMundo)!.Preco;
        return Results.Json(
            new PreviaConvocacaoResponse(
                SituacaoConvocacao.Tipos,
                convidados,
                SituacaoConvocacao.RecompensaParticipante,
                convidados == 0 ? 0 : preco / convidados,
                (int)SituacaoConvocacao.Duracao.TotalDays,
                bloqueio?.Codigo,
                bloqueio?.Mensagem),
            AppJsonContext.Default.PreviaConvocacaoResponse);
    }
}
