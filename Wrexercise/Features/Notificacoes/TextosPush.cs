using Wrexercise.Features.Jogo.Contracts;

namespace Wrexercise.Features.Notificacoes;

internal sealed record AvisoPush(
    string Tipo,
    Guid GrupoId,
    Guid? AtividadeId,
    string? AtorNome,
    string? Situacao,
    int Total,
    string? Cutucada = null,
    string? CutucadaTexto = null);

/// <summary>
/// Texto e destino de cada aviso no telefone. Espelha `webapp/src/features/notificacoes/textos.ts`:
/// mude os dois juntos. Os tipos que não estão aqui ficam só na lista do app (decisão 0049).
/// </summary>
internal static class TextosPush
{
    public static readonly string[] Tipos =
    [
        "NovaAtividade",
        "Reacao",
        "Comentario",
        "SituacaoAlterada",
        "Disputa",
        "CartaRecebida",
        "RivalEscolhido",
        "Cutucada",
        "ComecouTreinar",
        "TreinoLongo",
        "TreinoAppConcluido",
    ];

    public static string GetTexto(AvisoPush aviso)
    {
        var nome = PrimeiroNome(aviso.AtorNome);
        return aviso.Tipo switch
        {
            "NovaAtividade" => $"{nome} registrou um treino. Dá uma olhada!",
            "Reacao" when aviso.Total <= 1 => $"{nome} reagiu ao seu treino",
            "Reacao" => $"{nome} e mais {Pessoas(aviso.Total - 1)} reagiram ao seu treino",
            "Comentario" when aviso.Total <= 1 => $"{nome} comentou em uma atividade",
            "Comentario" => $"{aviso.Total} novos comentários em uma atividade",
            "Disputa" => $"O treino de {nome} precisa de uma decisão",
            "SituacaoAlterada" when aviso.Situacao == "Rejeitada" => "Seu treino foi rejeitado",
            "SituacaoAlterada" when aviso.Situacao == "Anulada" => "Seu treino foi anulado",
            "SituacaoAlterada" => "Seu treino entrou em revisão",
            "CartaRecebida" => $"{nome} te mandou uma carta. Topa?",
            "RivalEscolhido" => $"{nome} escolheu você como Rival da semana. Quem cumpre a meta primeiro?",
            "Cutucada" => TextoCutucada(nome, aviso.Cutucada, aviso.CutucadaTexto),
            "ComecouTreinar" => $"{nome} começou a treinar 🏋️ Bora torcer?",
            "Torcida" => $"{nome} está torcendo por você 📣",
            "TreinoLongo" => "Ainda treinando? Termine ou ajuste o seu treino",
            "TreinoAppConcluido" => "Seu treino do Movr acabou. Põe as fotos e termine aqui 📸",
            _ => "Você tem uma nova notificação",
        };
    }

    public static string GetDestino(AvisoPush aviso) => aviso.AtividadeId switch
    {
        // O treino ao vivo aparece na praça do grupo; o de quem treina, no cronômetro (decisão 0078).
        _ when aviso.Tipo == "ComecouTreinar" => $"/grupos/{aviso.GrupoId}/inicio",
        _ when aviso.Tipo is "TreinoLongo" or "TreinoAppConcluido" or "Torcida" => $"/grupos/{aviso.GrupoId}/treino",
        // As cartas, o Rival e a Cutucada aparecem na cena do grupo.
        null => $"/grupos/{aviso.GrupoId}/inicio",
        { } atividadeId when aviso.Tipo == "NovaAtividade" => $"/grupos/{aviso.GrupoId}/revisar/{atividadeId}",
        { } atividadeId => $"/atividades/{atividadeId}",
    };

    private static string TextoCutucada(string nome, string? codigo, string? texto) => codigo switch
    {
        TiposCutucada.Livre when !string.IsNullOrEmpty(texto) => $"{nome} te cutucou 👉 {TiposCutucada.EmojiLivre} \"{texto}\"",
        _ when TiposCutucada.Find(codigo) is { } tipo => $"{nome} te cutucou 👉 {tipo.Emoji} {tipo.Texto}",
        _ => $"{nome} te cutucou 👉",
    };

    private static string PrimeiroNome(string? nome) =>
        nome?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Alguém";

    private static string Pessoas(int total) => total == 1 ? "1 pessoa" : $"{total} pessoas";
}
