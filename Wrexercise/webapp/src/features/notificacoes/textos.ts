import { getTipoCutucada } from "@/features/jogo";
import type { Notificacao } from "./useNotificacoes";

function primeiroNome(nome: string | null): string {
  return nome?.trim().split(/\s+/)[0] ?? "Alguém";
}

function comOutros(
  nome: string | null,
  total: number,
  um: string,
  varios: string,
) {
  if (total <= 1) return `${primeiroNome(nome)} ${um}`;
  const outros = total - 1;
  return `${primeiroNome(nome)} e mais ${outros} ${outros === 1 ? "pessoa" : "pessoas"} ${varios}`;
}

export function getTextoNotificacao({
  tipo,
  atorNome,
  situacao,
  total,
  cutucada,
  cutucadaTexto,
}: Notificacao): string {
  switch (tipo) {
    case "NovaAtividade":
      return `${primeiroNome(atorNome)} registrou um treino. Dá uma olhada!`;
    case "Reacao":
      return comOutros(
        atorNome,
        total,
        "reagiu ao seu treino",
        "reagiram ao seu treino",
      );
    case "Comentario":
      return total <= 1
        ? `${primeiroNome(atorNome)} comentou em uma atividade`
        : `${total} novos comentários em uma atividade`;
    case "Disputa":
      return `O treino de ${primeiroNome(atorNome)} precisa de uma decisão`;
    case "SituacaoAlterada":
      if (situacao === "Rejeitada") return "Seu treino foi rejeitado";
      if (situacao === "Anulada") return "Seu treino foi anulado";
      return "Seu treino entrou em revisão";
    case "CartaRecebida":
      return `${primeiroNome(atorNome)} te mandou uma carta. Topa?`;
    case "CartaAceita":
      return `${primeiroNome(atorNome)} aceitou a sua carta`;
    case "CartaRecusada":
      return `${primeiroNome(atorNome)} recusou a sua carta. Ela voltou para a sua Mão`;
    case "JogadaConcluida":
      return `${primeiroNome(atorNome)} cumpriu a sua carta. Energia para os dois!`;
    case "RivalEscolhido":
      return `${primeiroNome(atorNome)} escolheu você como Rival da semana. Quem cumpre a meta primeiro?`;
    case "Cutucada": {
      const tipo = getTipoCutucada(cutucada);
      if (tipo?.codigo === "Livre" && cutucadaTexto)
        return `${primeiroNome(atorNome)} te cutucou 👉 ${tipo.emoji} "${cutucadaTexto}"`;
      return tipo && tipo.texto
        ? `${primeiroNome(atorNome)} te cutucou 👉 ${tipo.emoji} ${tipo.texto}`
        : `${primeiroNome(atorNome)} te cutucou 👉`;
    }
    case "ComecouTreinar":
      return `${primeiroNome(atorNome)} começou a treinar 🏋️ Bora torcer?`;
    case "Torcida":
      return `${primeiroNome(atorNome)} está torcendo por você 📣`;
    case "TreinoLongo":
      return "Ainda treinando? Termine ou ajuste o seu treino";
  }
}

export function getDestinoNotificacao({
  tipo,
  grupoId,
  atividadeId,
}: Notificacao): string {
  // O treino ao vivo aparece na praça do grupo; o de quem treina, no cronômetro (decisão 0078).
  if (tipo === "ComecouTreinar") return `/grupos/${grupoId}/inicio`;
  if (tipo === "TreinoLongo" || tipo === "Torcida")
    return `/grupos/${grupoId}/treino`;
  // As cartas, o Rival e a Cutucada aparecem na cena do grupo.
  if (atividadeId === null) return `/grupos/${grupoId}/inicio`;
  return tipo === "NovaAtividade"
    ? `/grupos/${grupoId}/revisar/${atividadeId}`
    : `/atividades/${atividadeId}`;
}
