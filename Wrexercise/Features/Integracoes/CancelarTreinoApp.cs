using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// A pessoa desistiu do treino no app antes de fazer qualquer coisa: o treino ao vivo sai, com as fotos
/// (ADR 0081). Depois de terminado, ele é da pessoa e o app recebe 409.
/// </summary>
internal static class CancelarTreinoApp
{
    public static async Task<IResult> HandleAsync(
        string externalId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        if (!AutorizacaoIntegracao.IsValidExternalId(externalId))
        {
            return ErrosIntegracao.InvalidData();
        }

        var app = context.GetAppAtual();
        var (status, arquivos) = await TreinoAoVivoApp.CancelarAsync(
            dataSource, app.UsuarioId, app.App, externalId, cancellationToken);
        switch (status)
        {
            case TreinoAppStatus.NaoEncontrado:
                return ErrosIntegracao.TreinoNaoEncontrado();
            case TreinoAppStatus.NaoEmAndamento:
                return ErrosIntegracao.TreinoNaoEmAndamento();
        }

        // Depois do commit: se a transação falhasse, as fotos continuariam valendo.
        TreinoAoVivoApp.ExcluirArquivos(armazenamento, arquivos);
        return Results.NoContent();
    }
}
