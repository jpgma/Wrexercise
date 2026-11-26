using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// A resposta de quem foi convidado (decisão 0091): "Tô dentro!", "Dessa vez não" ou, depois de aceitar, "Sair".
/// Como numa Provocação recebida, a resposta é final, e recusar ou sair dá FADIGA. O anfitrião não sai.
/// </summary>
internal static class ResponderConvocacao
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        Guid convocacaoId,
        ResponderConvocacaoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.Decisao is not (SituacaoConvocacao.DecisaoAceita
            or SituacaoConvocacao.DecisaoRecusada
            or SituacaoConvocacao.DecisaoDesistiu))
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GrupoAcesso.GetPapelAsync(connection, transaction, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        // Trava o convite: dois toques ao mesmo tempo não gravam duas decisões.
        string? atual;
        bool ativa;
        Guid anfitriaoId;
        await using (var command = new NpgsqlCommand("""
            SELECT p.decisao, c.expira_em > now(), c.anfitriao_id
            FROM participantes_convocacao p
            JOIN convocacoes c ON c.id = p.convocacao_id
            WHERE p.convocacao_id = $1 AND p.usuario_id = $2 AND c.grupo_id = $3
            FOR UPDATE OF p
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(convocacaoId);
            command.Parameters.AddWithValue(usuarioId);
            command.Parameters.AddWithValue(grupoId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ErrosJogo.ConvocacaoNaoEncontrada();
            }

            atual = reader.IsDBNull(0) ? null : reader.GetString(0);
            ativa = reader.GetBoolean(1);
            anfitriaoId = reader.GetGuid(2);
        }

        if (!ativa)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "CONVOCACAO_ENCERRADA",
                "O prazo desta Convocação já acabou.");
        }

        if (!IsPermitida(atual, request.Decisao, usuarioId == anfitriaoId))
        {
            return DecisaoInvalida();
        }

        if (request.Decisao == SituacaoConvocacao.DecisaoDesistiu)
        {
            // Com um treino que conta ou ainda pode contar, não dá mais para sair.
            var convocacao = (await Convocacoes.LoadAsync(connection, transaction, grupoId, convocacaoId, cancellationToken))!;
            if (convocacao.Fatos.Atividades.Any(a => a.UsuarioId == usuarioId && SituacaoConvocacao.IsViva(a)))
            {
                return ApiProblem.Create(
                    StatusCodes.Status409Conflict,
                    "TREINO_JA_CONTA",
                    "Você já tem um treino que conta nesta Convocação.");
            }
        }

        await using (var update = new NpgsqlCommand("""
            UPDATE participantes_convocacao
            SET decisao = $3, decidida_em = now()
            WHERE convocacao_id = $1 AND usuario_id = $2
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(convocacaoId);
            update.Parameters.AddWithValue(usuarioId);
            update.Parameters.AddWithValue(request.Decisao);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await NotificacoesConvocacao.MarcarLidaAsync(connection, transaction, convocacaoId, usuarioId, cancellationToken);

        var resultado = (await Convocacoes.LoadAsync(connection, transaction, grupoId, convocacaoId, cancellationToken))!;
        await transaction.CommitAsync(cancellationToken);
        return Results.Json(Convocacoes.ToResponse(resultado, usuarioId), AppJsonContext.Default.ConvocacaoResponse);
    }

    /// <summary>
    /// Sem resposta, aceita ou recusa. Depois de aceitar, só sai, e o anfitrião nem isso. Recusar e sair são finais.
    /// </summary>
    public static bool IsPermitida(string? atual, string nova, bool anfitriao) => (atual, nova) switch
    {
        (null, SituacaoConvocacao.DecisaoAceita or SituacaoConvocacao.DecisaoRecusada) => !anfitriao,
        (SituacaoConvocacao.DecisaoAceita, SituacaoConvocacao.DecisaoDesistiu) => !anfitriao,
        _ => false,
    };

    private static IResult DecisaoInvalida() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "DECISAO_INVALIDA",
        "Você já respondeu esta Convocação.");
}
