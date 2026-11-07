using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ListarJogadas
{
    /// <summary>Jogadas encerradas continuam na lista por este tempo depois do prazo, com o carimbo final.</summary>
    private static readonly TimeSpan Recentes = TimeSpan.FromDays(3);

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        if (await GrupoAcesso.GetPapelAsync(dataSource, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var agora = DateTimeOffset.UtcNow;
        var jogadas = (await Jogadas.LoadAsync(connection, null, usuarioId, usuarioId, cancellationToken))
            .Where(j => j.GrupoId == grupoId)
            .Select(j => (Jogada: j, Nova: IsNova(j, usuarioId)))
            .Where(x => x.Nova
                || SituacaoJogada.IsAberta(x.Jogada.Situacao)
                || x.Jogada.Estado.ExpiraEm > agora - Recentes)
            .ToList();

        var nomes = await LoadNomesAsync(connection, grupoId, cancellationToken);

        // As trocas para +10 MIN e +20 MIN só valem com um desafio ativo no grupo.
        var temDesafioAtivo = jogadas.Any(x => x.Jogada.DestinatarioId == usuarioId
                && x.Jogada.Situacao == SituacaoJogada.Enviada)
            && await Jogadas.LoadDuracaoMinimaAtivaAsync(connection, grupoId, cancellationToken) is not null;

        var resposta = jogadas
            .Select(x => new JogadaResponse(
                x.Jogada.Id,
                x.Jogada.GrupoId,
                x.Jogada.RemetenteId,
                nomes.GetValueOrDefault(x.Jogada.RemetenteId, "Ex-membro"),
                x.Jogada.DestinatarioId,
                nomes.GetValueOrDefault(x.Jogada.DestinatarioId, "Ex-membro"),
                ErrosJogo.ToResponse(CatalogoCartas.Find(x.Jogada.CodigoCarta)! with { Preco = x.Jogada.Preco }),
                x.Jogada.Estado.Codigo,
                x.Jogada.CodigoResposta is { } codigoResposta
                    ? ErrosJogo.ToResponse(CatalogoCartas.Find(codigoResposta)!)
                    : null,
                x.Jogada.OrigemId is not null,
                x.Jogada.DestinatarioId == usuarioId && x.Jogada.Situacao == SituacaoJogada.Enviada
                    ? CatalogoCartas.GetEquivalentes(x.Jogada.Estado.Codigo)
                        .Where(c => temDesafioAtivo || !CatalogoCartas.IsDuracao(c))
                        .ToList()
                    : [],
                x.Jogada.Situacao,
                x.Jogada.Estado.EnviadaEm,
                x.Jogada.Estado.ExpiraEm,
                x.Jogada.Estado.DuracaoAlvoSegundos,
                x.Jogada.Estado.TipoEvitar,
                x.Jogada.DestinatarioId == usuarioId,
                x.Nova))
            .ToList();

        return Results.Json(new JogadasResponse(resposta), AppJsonContext.Default.JogadasResponse);
    }

    /// <summary>
    /// O destinatário ainda não viu a carta chegar, ou o remetente ainda não viu a resposta.
    /// </summary>
    public static bool IsNova(JogadaCarregada jogada, Guid usuarioId) =>
        jogada.DestinatarioId == usuarioId
            ? jogada.VistaEm is null
            : jogada.Estado.Resposta is not null && jogada.ResultadoVistoEm is null;

    private static async Task<Dictionary<Guid, string>> LoadNomesAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        // Inclui quem já saiu do grupo: uma Jogada antiga ainda mostra o nome de quem participou.
        await using var command = new NpgsqlCommand("""
            SELECT u.id, u.nome_exibicao
            FROM membros_grupo m
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $1
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        var nomes = new Dictionary<Guid, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nomes[reader.GetGuid(0)] = reader.GetString(1);
        }

        return nomes;
    }
}
