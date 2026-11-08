using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// O <c>Proprietario</c> e os <c>Administrador</c> de um grupo voltam ao padrão o avatar de um membro
/// desse grupo, como medida contra imagens impróprias (docs/gamificacao.md §4.3).
/// </summary>
internal static class RedefinirAvatarMembro
{
    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        Guid usuarioId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoAvatares armazenamento,
        CancellationToken cancellationToken)
    {
        var atorId = context.GetSessaoAtual().UsuarioId;
        var papel = await GrupoAcesso.GetPapelAsync(dataSource, grupoId, atorId, cancellationToken);
        if (papel is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        if (papel is not (PapelMembro.Proprietario or PapelMembro.Administrador))
        {
            return GrupoAcesso.AcessoNegado();
        }

        if (await GrupoAcesso.GetPapelAsync(dataSource, grupoId, usuarioId, cancellationToken) is null)
        {
            return ErrosJogo.MembroNaoEncontrado();
        }

        await Avatares.RestaurarAsync(dataSource, armazenamento, usuarioId, cancellationToken);
        return Results.NoContent();
    }
}
