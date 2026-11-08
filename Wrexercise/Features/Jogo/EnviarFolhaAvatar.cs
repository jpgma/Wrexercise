using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>Importa a folha de sprites da pessoa. O corpo cabe no limite padrão de 1 MB da API.</summary>
internal static class EnviarFolhaAvatar
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoAvatares armazenamento,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var arquivo = form.Files.GetFile("arquivo");
        if (arquivo is null || arquivo.Length is 0 or > ValidacaoFolhaAvatar.MaxBytes)
        {
            return ErrosJogo.FolhaInvalida();
        }

        string? nome;
        await using (var stream = arquivo.OpenReadStream())
        {
            nome = await armazenamento.SalvarAsync(stream, cancellationToken);
        }

        if (nome is null)
        {
            return ErrosJogo.FolhaInvalida();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        string? anterior;
        try
        {
            anterior = await Avatares.TrocarFolhaAsync(dataSource, usuarioId, nome, cancellationToken);
        }
        catch
        {
            armazenamento.ExcluirSeExistir(nome);
            throw;
        }

        if (anterior is not null)
        {
            armazenamento.ExcluirSeExistir(anterior);
        }

        return Results.Json(new FolhaEnviadaResponse(Avatares.GetUrl(nome)), AppJsonContext.Default.FolhaEnviadaResponse);
    }
}
