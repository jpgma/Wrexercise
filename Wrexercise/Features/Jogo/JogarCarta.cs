using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal sealed record JogadaCriadaResponse(Guid Id, DateTimeOffset ExpiraEm);

internal static class JogarCarta
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        JogarCartaRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.CartaId is not { } cartaId || request.DestinatarioId is not { } destinatarioId)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var remetenteId = context.GetSessaoAtual().UsuarioId;
        if (destinatarioId == remetenteId)
        {
            return DestinatarioInvalido();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Trava os dois: assim nem o limite de enviadas nem o de recebidas passa do máximo.
        await EstadoJogo.LockUsuariosAsync(connection, transaction, [remetenteId, destinatarioId], cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, remetenteId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, destinatarioId, cancellationToken) is null)
        {
            return DestinatarioInvalido();
        }

        var (remetenteNoJogo, destinatarioNoJogo) =
            await Jogadas.LoadModoJogoAsync(connection, transaction, remetenteId, destinatarioId, cancellationToken);
        if (!remetenteNoJogo)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "MODO_JOGO_NECESSARIO",
                "Ative o modo Jogo para jogar cartas.");
        }

        if (!destinatarioNoJogo)
        {
            return ErrosJogo.DestinatarioForaDoJogo();
        }

        var estado = await EstadoJogo.LoadAsync(connection, transaction, remetenteId, cancellationToken);
        var (carta, fusao, _, erroCartas) = ResolverCartas(estado, cartaId, request.CartaFusaoId);
        if (erroCartas is not null)
        {
            return erroCartas;
        }

        // Decisão 0089: quem está com FADIGA não joga Provocação, nem Fusão. Respostas continuam liberadas.
        if (estado.FadigaRestante > 0)
        {
            return ErrosJogo.FadigaAtiva(estado.FadigaRestante);
        }

        var jogadas = await Jogadas.LoadAsync(connection, transaction, remetenteId, destinatarioId, cancellationToken);
        var agora = await Jogadas.LoadAgoraAsync(connection, transaction, grupoId, cancellationToken);
        if (LimitesJogada.Verificar(remetenteId, destinatarioId, jogadas.Select(j => j.ToLimite()).ToList(), agora.Utc)
            is { } limite)
        {
            return ErrosJogo.Limite(limite);
        }

        var (parametros, erro) = await Jogadas.FixarObjetivoAsync(
            connection, transaction, carta!.Codigo, grupoId, destinatarioId, remetenteId, cancellationToken, fusao?.Codigo);
        if (erro is not null)
        {
            return erro;
        }

        var (jogadaId, expiraEm) = await Jogadas.InsertAsync(
            connection,
            transaction,
            new NovaJogada(grupoId, remetenteId, destinatarioId, cartaId, null, agora.Utc,
                PrazoJogada.Calcular(carta.Codigo, fusao?.Codigo, agora.Local), parametros!,
                fusao is null ? null : request.CartaFusaoId),
            cancellationToken);

        await NotificacoesJogada.NotificarAsync(
            connection,
            transaction,
            jogadaId,
            destinatarioId,
            remetenteId,
            TipoNotificacaoJogada.CartaRecebida,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(
            new JogadaCriadaResponse(jogadaId, expiraEm),
            AppJsonContext.Default.JogadaCriadaResponse,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// As Provocações de um arremesso, conferidas na Mão de quem joga: a carta e, numa Fusão (decisão 0070), a
    /// segunda. A prévia (<see cref="PreverJogada"/>) usa a mesma conferência.
    /// </summary>
    /// <returns>As cartas e a soma dos preços pagos, ou o erro.</returns>
    public static (Carta? Carta, Carta? Fusao, int Preco, IResult? Erro) ResolverCartas(
        EstadoJogo estado,
        Guid cartaId,
        Guid? cartaFusaoId)
    {
        var comprada = estado.Cartas.FirstOrDefault(c => c.Id == cartaId);
        if (comprada is null || estado.IsGasta(comprada.Id))
        {
            return (null, null, 0, ErrosJogo.CartaNaoEncontrada());
        }

        var carta = CatalogoCartas.Find(comprada.Codigo)!;
        if (carta.Tipo != TipoCarta.Provocacao)
        {
            return (null, null, 0, ErrosJogo.CartaInvalida());
        }

        if (cartaFusaoId is not { } fusaoId)
        {
            return (carta, null, comprada.Preco, null);
        }

        var segunda = estado.Cartas.FirstOrDefault(c => c.Id == fusaoId);
        if (segunda is null || estado.IsGasta(segunda.Id))
        {
            return (null, null, 0, ErrosJogo.CartaNaoEncontrada());
        }

        var fusao = CatalogoCartas.Find(segunda.Codigo)!;
        if (!CatalogoCartas.PodeFundir(carta, fusao))
        {
            return (null, null, 0, ErrosJogo.FusaoInvalida());
        }

        return (carta, fusao, comprada.Preco + segunda.Preco, null);
    }

    public static IResult DestinatarioInvalido() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DESTINATARIO_INVALIDO",
        "Escolha outro membro do grupo para receber a carta.");
}
