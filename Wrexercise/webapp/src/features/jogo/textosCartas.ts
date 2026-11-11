import {
  formatarDuracao,
  tipoAtividadeLabels,
  type TipoAtividade,
} from "@/features/atividades";
import type { CodigoProvocacao, Jogada, SituacaoJogada } from "./useCartas";

/** O objetivo em poucas palavras, para escolher a troca da MUDANÇA DE PLANOS. */
export const objetivoCurto: Record<CodigoProvocacao, string> = {
  Mais10Min: "Dia com 10 min a mais",
  Mais20Min: "Dia com 20 min a mais",
  Variedade: "Treino de outro tipo",
  FimDeSemana: "Treino no fim de semana",
  AntesDas8: "Treino antes das 8h",
};

/** O objetivo de uma Jogada, já com o que foi fixado no envio ou no aceite. */
export function getObjetivo(jogada: Jogada): string {
  switch (jogada.objetivo) {
    case "Mais10Min":
    case "Mais20Min":
      return jogada.duracaoAlvoSegundos
        ? `Somar ${formatarDuracao(jogada.duracaoAlvoSegundos)} ou mais num mesmo dia`
        : objetivoCurto[jogada.objetivo];
    case "Variedade":
      return jogada.tipoEvitar
        ? `Um treino que não seja ${tipoAtividadeLabels[jogada.tipoEvitar as TipoAtividade] ?? jogada.tipoEvitar}`
        : "Um treino de qualquer tipo";
    case "FimDeSemana":
      return "Um treino no sábado ou no domingo";
    case "AntesDas8":
      return "Um treino que comece antes das 8h";
  }
}

const formatadorPrazo = new Intl.DateTimeFormat("pt-BR", {
  weekday: "short",
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

export function formatarPrazo(iso: string): string {
  return formatadorPrazo.format(new Date(iso));
}

export function primeiroNome(nome: string): string {
  return nome.trim().split(/\s+/)[0] ?? nome;
}

type Chip = { texto: string; classe: string };

/** O selo da situação, do ponto de vista de quem consulta. */
export function getSeloSituacao(
  situacao: SituacaoJogada,
  recebida: boolean,
): Chip {
  switch (situacao) {
    case "Enviada":
      return recebida
        ? { texto: "Responda", classe: "chip-game" }
        : { texto: "Aguardando resposta", classe: "" };
    case "Aceita":
      return { texto: recebida ? "Valendo" : "Aceita", classe: "chip-game" };
    case "AguardandoVerificacao":
      return { texto: "Em verificação", classe: "chip-warning" };
    case "Concluida":
      return { texto: "Concluída", classe: "chip-success" };
    case "Recusada":
      return { texto: "Recusada", classe: "" };
    case "Expirada":
      return { texto: "Expirada", classe: "" };
  }
}

/** Os limites de envio que a Mesa deixa conferir antes do arremesso (`LimitesJogada.cs`). */
const IntervaloMesmaPessoaMs = 24 * 60 * 60 * 1000;
const EnviadasAtivas = 2;
const ativa = (j: Jogada) =>
  j.situacao === "Enviada" || j.situacao === "Aceita";

/** O limite que vale para qualquer alvo: quem vê já tem 2 cartas enviadas em aberto. */
export function getLimiteEnvio(jogadas: Jogada[]): string | null {
  return jogadas.filter((j) => !j.recebida && ativa(j)).length >= EnviadasAtivas
    ? "Você já tem 2 cartas enviadas esperando resposta ou atividade."
    : null;
}

/**
 * Por que quem vê não pode jogar uma carta no alvo agora, pelas Jogadas deste grupo, ou `null`. O
 * servidor confere de novo com as Jogadas de todos os grupos: aqui só se evita um arremesso que já
 * se sabe recusado.
 */
export function getBloqueioJogada(
  jogadas: Jogada[],
  alvoId: string,
  nome: string,
  agora: number,
): string | null {
  if (
    jogadas.some(
      (j) =>
        ativa(j) && (j.remetenteId === alvoId || j.destinatarioId === alvoId),
    )
  )
    return `Já existe uma carta aberta entre você e ${nome}.`;
  if (
    jogadas.some(
      (j) =>
        !j.recebida &&
        j.destinatarioId === alvoId &&
        agora - Date.parse(j.enviadaEm) < IntervaloMesmaPessoaMs,
    )
  )
    return `Você já enviou uma carta para ${nome} nas últimas 24 horas.`;
  return getLimiteEnvio(jogadas);
}
