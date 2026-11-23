using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// <c>Latitude</c> e <c>Longitude</c>: onde o treino começou, só quando a pessoa deixou o app mandar o local
/// (ADR 0080, 0081). Coordenadas inválidas não impedem o começo. <c>GravarPercurso</c>: na Corrida, na Caminhada
/// e no Ciclismo, o app grava o GPS e manda o Percurso (ADR 0085); aí o local de início não é guardado.
/// </summary>
internal sealed record ComecarTreinoAppRequest(
    Guid? GrupoId,
    string? Tipo,
    double? Latitude = null,
    double? Longitude = null,
    bool? GravarPercurso = null);

/// <summary>
/// O app começa o treino ao vivo no grupo escolhido (<c>PUT</c> pelo identificador do treino no app, ADR 0081).
/// Repetir devolve o mesmo treino (200); um treino novo responde 201 e avisa o grupo, como no navegador.
/// </summary>
internal static class ComecarTreinoApp
{
    public static async Task<IResult> HandleAsync(
        string externalId,
        ComecarTreinoAppRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!AutorizacaoIntegracao.IsValidExternalId(externalId)
            || request.GrupoId is not { } grupoId
            || !TipoAtividade.IsValid(request.Tipo))
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        var (erro, treino, criado) = await TreinoAoVivoApp.ComecarAsync(
            dataSource,
            app.UsuarioId,
            grupoId,
            request.Tipo!,
            request.Latitude,
            request.Longitude,
            app.App,
            externalId,
            cancellationToken,
            request.GravarPercurso == true);
        if (erro is not null)
        {
            return erro;
        }

        return Results.Json(
            RespostasIntegracao.BuildTreino(treino!),
            AppJsonContext.Default.TreinoAppResponse,
            statusCode: criado ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }
}
