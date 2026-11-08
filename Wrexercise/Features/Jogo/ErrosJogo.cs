using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Jogo;

/// <summary>Erros usados por duas ou mais operações do Jogo.</summary>
internal static class ErrosJogo
{
    public static IResult DadosInvalidos() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");

    public static IResult FolhaInvalida() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "FOLHA_INVALIDA",
        "A folha de sprites precisa ser um PNG RGBA de 256×384 px, com até 64 KB.");

    public static IResult AvatarNaoEncontrado() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "AVATAR_NAO_ENCONTRADO",
        "Avatar não encontrado.");

    public static IResult MembroNaoEncontrado() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "MEMBRO_NAO_ENCONTRADO",
        "Membro não encontrado.");

    public static IResult CartaInvalida() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "CARTA_INVALIDA",
        "Esta carta não está disponível.");

    public static IResult CartaNaoEncontrada() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "CARTA_NAO_ENCONTRADA",
        "Carta não encontrada.");

    public static IResult DestinatarioForaDoJogo() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "DESTINATARIO_FORA_DO_JOGO",
        "Esta pessoa não está no modo Jogo.");

    public static IResult JogadaNaoEncontrada() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "JOGADA_NAO_ENCONTRADA",
        "Jogada não encontrada.");

    public static IResult Limite(string codigo) => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        codigo,
        codigo switch
        {
            LimitesJogada.ErroPar => "Já existe uma Jogada aberta entre vocês dois.",
            LimitesJogada.ErroRecente => "Você já enviou uma carta para esta pessoa nas últimas 24 horas.",
            LimitesJogada.ErroEnviadas => "Você já tem 2 cartas enviadas esperando resposta ou atividade.",
            LimitesJogada.ErroRecebidas => "Esta pessoa já tem 2 cartas recebidas em aberto.",
            _ => "Não é possível jogar esta carta agora.",
        });

    public static CartaCatalogoResponse ToResponse(Carta carta) =>
        new(carta.Codigo, carta.Nome, carta.Tipo, carta.Preco, carta.Efeito);
}
