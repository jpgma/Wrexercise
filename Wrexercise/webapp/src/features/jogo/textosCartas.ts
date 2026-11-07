import {
  formatarDuracao,
  tipoAtividadeLabels,
  type TipoAtividade,
} from "@/features/atividades";
import type { CodigoProvocacao, Jogada, SituacaoJogada } from "./useCartas";

/** O objetivo em poucas palavras, para escolher a troca da MUDANÇA DE PLANOS. */
export const objetivoCurto: Record<CodigoProvocacao, string> = {
  Mais10Min: "Treino 10 min mais longo",
  Mais20Min: "Treino 20 min mais longo",
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
        ? `Um treino de ${formatarDuracao(jogada.duracaoAlvoSegundos)} ou mais`
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
