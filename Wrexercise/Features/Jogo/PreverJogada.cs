using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>O que uma carta da Mão vai pedir a um colega, antes do arremesso.</summary>
/// <param name="DuracaoAlvoSegundos">A duração-alvo de +10 MIN e +20 MIN.</param>
/// <param name="TipoEvitar">O tipo a evitar da VARIEDADE; nulo quando qualquer tipo vale.</param>
internal sealed record PreviaJogadaResponse(
    int? DuracaoAlvoSegundos,
    string? TipoEvitar,
    DateTimeOffset ExpiraEm,
    int RecompensaDestinatario,
    int RecompensaRemetente);

/// <summary>
/// A prévia de uma Jogada: o objetivo, o prazo e as recompensas que o arremesso fixaria agora. Usa as mesmas
/// funções de <see cref="JogarCarta"/>, para que a prévia e a Jogada não divirjam. Não grava nada e não confere
/// os limites de envio nem o modo Jogo: o arremesso continua sendo quem decide.
/// </summary>
internal static class PreverJogada
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        Guid? destinatarioId,
        Guid? cartaId,
        Guid? cartaFusaoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (cartaId is not { } carta || destinatarioId is not { } destinatario)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var remetenteId = context.GetSessaoAtual().UsuarioId;
        if (destinatario == remetenteId)
        {
            return JogarCarta.DestinatarioInvalido();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, remetenteId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, destinatario, cancellationToken) is null)
        {
            return JogarCarta.DestinatarioInvalido();
        }

        var estado = await EstadoJogo.LoadAsync(connection, transaction, remetenteId, cancellationToken);
        var (provocacao, fusao, preco, erroCartas) = JogarCarta.ResolverCartas(estado, carta, cartaFusaoId);
        if (erroCartas is not null)
        {
            return erroCartas;
        }

        var (parametros, erro) = await Jogadas.FixarObjetivoAsync(
            connection, transaction, provocacao!.Codigo, grupoId, destinatario, remetenteId, cancellationToken, fusao?.Codigo);
        if (erro is not null)
        {
            return erro;
        }

        var agora = await Jogadas.LoadAgoraAsync(connection, transaction, grupoId, cancellationToken);
        var expiraEm = await LoadInstanteAsync(
            connection,
            transaction,
            grupoId,
            PrazoJogada.Calcular(provocacao.Codigo, fusao?.Codigo, agora.Local),
            cancellationToken);

        return Results.Json(
            new PreviaJogadaResponse(
                parametros!.DuracaoAlvoSegundos,
                parametros.TipoEvitar,
                expiraEm,
                CalculoEnergia.GetRecompensa(new RecompensaJogada(preco, ComoDestinatario: true, Fusao: fusao is not null)),
                CalculoEnergia.GetRecompensa(new RecompensaJogada(preco, ComoDestinatario: false, Fusao: fusao is not null))),
            AppJsonContext.Default.PreviaJogadaResponse);
    }

    /// <summary>O horário local do grupo como instante, com a mesma conversão que grava o prazo da Jogada.</summary>
    private static async Task<DateTimeOffset> LoadInstanteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grupoId,
        DateTime local,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT $2 AT TIME ZONE fuso_horario FROM grupos WHERE id = $1",
            connection,
            transaction);
        command.Parameters.AddWithValue(grupoId);
        command.Parameters.AddWithValue(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return TimestampLeitura.FromReader(reader, 0);
    }
}
