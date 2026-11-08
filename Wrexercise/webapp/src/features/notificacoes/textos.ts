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
        ? `${primeiroNome(atorNome)} comentou em uma revisão`
        : `${total} novos comentários em uma revisão`;
    case "Disputa":
      return `O treino de ${primeiroNome(atorNome)} precisa de uma decisão`;
    case "SituacaoAlterada":
      if (situacao === "Verificada") return "Seu treino foi verificado";
      if (situacao === "Rejeitada") return "Seu treino foi rejeitado";
      return "Seu treino entrou em revisão";
    case "CartaRecebida":
      return `${primeiroNome(atorNome)} te mandou uma carta. Topa?`;
    case "CartaAceita":
      return `${primeiroNome(atorNome)} aceitou a sua carta`;
    case "CartaRecusada":
      return `${primeiroNome(atorNome)} recusou a sua carta. Ela voltou para a sua Mão`;
    case "JogadaConcluida":
      return atorNome
        ? `${primeiroNome(atorNome)} cumpriu a sua carta. Energia para os dois!`
        : "Você cumpriu uma carta. Energia para os dois!";
    case "RivalEscolhido":
      return `${primeiroNome(atorNome)} escolheu você como Rival da semana. Quem cumpre a meta primeiro?`;
  }
}

export function getDestinoNotificacao({
  tipo,
  grupoId,
  atividadeId,
}: Notificacao): string {
  // As cartas e o Rival aparecem na cena do grupo.
  if (atividadeId === null) return `/grupos/${grupoId}/inicio`;
  return tipo === "NovaAtividade"
    ? `/grupos/${grupoId}/revisar/${atividadeId}`
    : `/atividades/${atividadeId}`;
}
