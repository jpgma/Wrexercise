using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Atividades;

internal enum AtividadeOperacaoStatus
{
    Concluida,
    GrupoNaoEncontrado,
    AtividadeNaoEncontrada,
    EvidenciaNaoEncontrada,
    AcessoNegado,
    DadosInvalidos,
    ComprovacaoNaoAplicavel,
    LimiteEvidencias,
    EvidenciaInvalida,
    AtividadeEncerrada,
    AvaliacoesPendentes,
    AtividadeMuitoAntiga,
    EvidenciaDuplicada,
    EvidenciaJaAnexada,
    ChaveIdempotenciaReutilizada,
    ExclusaoNaoPermitida,
}

internal static class OperacaoAtividade
{
    public static IResult ToResult(AtividadeOperacaoStatus status) => status switch
    {
        AtividadeOperacaoStatus.Concluida => Results.NoContent(),
        AtividadeOperacaoStatus.GrupoNaoEncontrado => GrupoAcesso.GrupoNaoEncontrado(),
        AtividadeOperacaoStatus.AtividadeNaoEncontrada => AtividadeNaoEncontrada(),
        AtividadeOperacaoStatus.EvidenciaNaoEncontrada => EvidenciaNaoEncontrada(),
        AtividadeOperacaoStatus.AcessoNegado => GrupoAcesso.AcessoNegado(),
        AtividadeOperacaoStatus.DadosInvalidos => InvalidData(),
        AtividadeOperacaoStatus.ComprovacaoNaoAplicavel => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "COMPROVACAO_NAO_APLICAVEL",
            "Esta atividade não aceita evidências."),
        AtividadeOperacaoStatus.LimiteEvidencias => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "LIMITE_EVIDENCIAS",
            "Esta atividade já atingiu o número máximo de evidências."),
        AtividadeOperacaoStatus.EvidenciaInvalida => ApiProblem.Create(
            StatusCodes.Status400BadRequest,
            "EVIDENCIA_INVALIDA",
            "Verifique o arquivo enviado."),
        AtividadeOperacaoStatus.AtividadeEncerrada => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "ATIVIDADE_ENCERRADA",
            "Não é possível alterar evidências desta atividade."),
        AtividadeOperacaoStatus.AvaliacoesPendentes => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "AVALIACOES_PENDENTES",
            "Avalie as atividades do grupo antes de registrar a sua."),
        AtividadeOperacaoStatus.AtividadeMuitoAntiga => ApiProblem.Create(
            StatusCodes.Status400BadRequest,
            "ATIVIDADE_MUITO_ANTIGA",
            "Registre atividades das últimas 48 horas."),
        AtividadeOperacaoStatus.EvidenciaDuplicada => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "EVIDENCIA_DUPLICADA",
            "Esta foto já foi usada em outra atividade."),
        AtividadeOperacaoStatus.EvidenciaJaAnexada => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "EVIDENCIA_JA_ANEXADA",
            "Esta foto já está nesta atividade."),
        AtividadeOperacaoStatus.ChaveIdempotenciaReutilizada => ApiProblem.Create(
            StatusCodes.Status422UnprocessableEntity,
            "CHAVE_IDEMPOTENCIA_REUTILIZADA",
            "Este envio já foi usado em outro registro. Tente de novo."),
        AtividadeOperacaoStatus.ExclusaoNaoPermitida => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "EXCLUSAO_NAO_PERMITIDA",
            "Só dá para apagar nos primeiros 15 minutos, antes de alguém contestar."),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static IResult AtividadeNaoEncontrada() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "ATIVIDADE_NAO_ENCONTRADA",
        "Atividade não encontrada.");

    public static IResult EvidenciaNaoEncontrada() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "EVIDENCIA_NAO_ENCONTRADA",
        "Evidência não encontrada.");

    public static IResult InvalidData() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");
}
