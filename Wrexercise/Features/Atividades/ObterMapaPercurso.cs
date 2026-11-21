using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// Um tile do mapa do Percurso (ADR 0084). O tile precisa tocar o percurso que quem consulta vê (aparado para o
/// grupo), com um tile de margem, num zoom de 12 a 17 que não peça mais de 36 tiles.
/// </summary>
internal static class ObterMapaPercurso
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        int z,
        int x,
        int y,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CacheMapas cache,
        CancellationToken cancellationToken)
    {
        if (z is < MapaPercurso.ZoomMinimo or > MapaPercurso.ZoomMaximo || x < 0 || y < 0
            || x >= 1 << z || y >= 1 << z)
        {
            return OperacaoAtividade.AtividadeNaoEncontrada();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        BboxPercurso? bbox;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var percurso = await ObterPercurso.CarregarAsync(connection, id, usuarioId, cancellationToken);
            bbox = percurso is null ? null : PercursoAtividade.Bbox(percurso.Visiveis);
        }

        var tile = new TileMapa(z, x, y);
        return bbox is { } faixa && MapaPercurso.IsTilePermitido(faixa, tile)
            ? await MapaLocal.ResponderTileAsync(tile, cache, context, cancellationToken)
            : OperacaoAtividade.AtividadeNaoEncontrada();
    }
}
