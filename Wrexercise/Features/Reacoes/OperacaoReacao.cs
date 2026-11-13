using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Reacoes;

internal enum ReacaoOperacaoStatus
{
    Concluida,
    AtividadeNaoEncontrada,
    DadosInvalidos,
    AutorNaoReage,
    NaoRevisor,
    JanelaEncerrada,
    ReacaoEncerrada,
    SemPermissaoRevisao,
    RevisaoNaoPendente,
    RevisaoPropria,
}

internal static class OperacaoReacao
{
    public static IResult ToResult(ReacaoOperacaoStatus status) => status switch
    {
        ReacaoOperacaoStatus.Concluida => Results.NoContent(),
        ReacaoOperacaoStatus.AtividadeNaoEncontrada => ApiProblem.Create(
            StatusCodes.Status404NotFound,
            "ATIVIDADE_NAO_ENCONTRADA",
            "Atividade não encontrada."),
        ReacaoOperacaoStatus.DadosInvalidos => InvalidData(),
        ReacaoOperacaoStatus.AutorNaoReage => ApiProblem.Create(
            StatusCodes.Status403Forbidden,
            "AUTOR_NAO_REAGE",
            "O autor da atividade não pode registrar reações."),
        ReacaoOperacaoStatus.NaoRevisor => ApiProblem.Create(
            StatusCodes.Status403Forbidden,
            "NAO_REVISOR",
            "Você não pode reagir a esta atividade."),
        ReacaoOperacaoStatus.JanelaEncerrada => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "JANELA_ENCERRADA",
            "O prazo para reações desta atividade já encerrou."),
        ReacaoOperacaoStatus.ReacaoEncerrada => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "REACAO_ENCERRADA",
            "Esta atividade não aceita mais reações da comunidade."),
        ReacaoOperacaoStatus.SemPermissaoRevisao => GrupoAcesso.AcessoNegado(),
        ReacaoOperacaoStatus.RevisaoNaoPendente => ApiProblem.Create(
            StatusCodes.Status409Conflict,
            "REVISAO_NAO_PENDENTE",
            "Esta atividade não está aguardando revisão administrativa."),
        ReacaoOperacaoStatus.RevisaoPropria => ApiProblem.Create(
            StatusCodes.Status403Forbidden,
            "REVISAO_PROPRIA_NAO_PERMITIDA",
            "Outro administrador do grupo precisa decidir sobre a sua atividade."),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static IResult InvalidData() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");
}
