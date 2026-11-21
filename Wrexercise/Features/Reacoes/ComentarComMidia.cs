using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Reacoes;

/// <summary>
/// Comentário com uma foto ou um vídeo (ADR 0083): multipart com <c>arquivo</c>, a <c>miniatura</c> JPEG
/// gerada no navegador e o <c>texto</c> opcional. Os limites são os das evidências, mas a mídia não é
/// evidência: não passa pelos avisos nem conta para a verificação.
/// </summary>
internal static class ComentarComMidia
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return OperacaoReacao.InvalidData();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var arquivo = form.Files.GetFile("arquivo");
        var miniatura = form.Files.GetFile("miniatura");
        string? texto = form["texto"].ToString() is { Length: > 0 } escrito ? escrito : null;
        if (arquivo is not { Length: > 0 } || (texto is not null && !ValidacaoRevisao.IsValidTexto(texto)))
        {
            return OperacaoReacao.InvalidData();
        }

        var salvos = new List<string>();
        try
        {
            EvidenciaSalva salvo;
            await using (var stream = arquivo.OpenReadStream())
            {
                salvo = await armazenamento.SalvarAsync(
                    stream, aceitaVideo: true, ValidacaoMidiaEvidencia.MaxBytes, cancellationToken);
            }

            salvos.Add(salvo.CaminhoRelativo);

            string? caminhoMiniatura = null;
            if (miniatura is { Length: > 0 })
            {
                EvidenciaSalva salva;
                await using (var stream = miniatura.OpenReadStream())
                {
                    salva = await armazenamento.SalvarAsync(
                        stream, aceitaVideo: false, ValidacaoMidiaEvidencia.MaxBytesMiniatura, cancellationToken);
                }

                salvos.Add(salva.CaminhoRelativo);
                if (salva.TipoMime != ValidacaoMidiaEvidencia.TipoMimeMiniatura)
                {
                    throw new EvidenciaInvalidaException("Miniatura precisa ser JPEG.");
                }

                caminhoMiniatura = salva.CaminhoRelativo;
            }

            var criado = await Comentarios.InserirAsync(
                dataSource,
                id,
                context.GetSessaoAtual().UsuarioId,
                texto is null ? null : ValidacaoRevisao.NormalizeJustificativa(texto),
                new MidiaComentarioSalva(salvo, caminhoMiniatura),
                cancellationToken);
            if (criado is null)
            {
                ExcluirSalvos(armazenamento, salvos);
                return OperacaoReacao.ToResult(ReacaoOperacaoStatus.AtividadeNaoEncontrada);
            }

            return Results.Json(criado, AppJsonContext.Default.ComentarioResponse, statusCode: StatusCodes.Status201Created);
        }
        catch (EvidenciaInvalidaException ex)
        {
            ExcluirSalvos(armazenamento, salvos);
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                ex.Codigo ?? "MIDIA_INVALIDA",
                ex.Codigo is null
                    ? "Verifique a foto ou o vídeo enviado."
                    : "Não deu para usar este vídeo. Envie um MP4 de 2 segundos a 1 minuto.");
        }
        catch
        {
            ExcluirSalvos(armazenamento, salvos);
            throw;
        }
    }

    private static void ExcluirSalvos(ArmazenamentoEvidencias armazenamento, List<string> salvos)
    {
        foreach (var caminho in salvos)
        {
            armazenamento.ExcluirSeExistir(caminho);
        }
    }
}
