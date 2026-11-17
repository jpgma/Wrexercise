import {
  formatarDuracao,
  tipoAtividadeLabels,
  type TipoAtividade,
} from "@/features/atividades";
import type {
  Carta,
  CodigoCarta,
  CodigoProvocacao,
  Jogada,
  SituacaoJogada,
} from "./useCartas";

/** O objetivo em poucas palavras, para escolher a troca da MUDANÇA DE PLANOS. */
export const objetivoCurto: Record<CodigoProvocacao, string> = {
  Mais10Min: "Dia com 10 min a mais",
  Mais20Min: "Dia com 20 min a mais",
  Variedade: "Treino de outro tipo",
  FimDeSemana: "Treino no fim de semana",
  AntesDas8: "Treino antes das 8h",
};

/** O bônus de quem cumpre uma Fusão, em % da soma dos preços (`CatalogoCartas.BonusFusaoPorCento`). */
const BonusFusaoPorCento = 20;

/** Duas Provocações diferentes podem ser jogadas juntas, numa Fusão (decisão 0070). */
export function podeFundir(a: Carta, b: Carta): boolean {
  return (
    a.tipo === "Provocacao" && b.tipo === "Provocacao" && a.codigo !== b.codigo
  );
}

/** O que quem cumpre uma Fusão ganha: a soma das duas cartas mais o bônus. O servidor confirma. */
export function getPremioFusao(a: Carta, b: Carta): number {
  const soma = a.preco + b.preco;
  return soma + Math.floor((soma * BonusFusaoPorCento) / 100);
}

const minutosExtras: Partial<Record<CodigoCarta, number>> = {
  Mais10Min: 10,
  Mais20Min: 20,
};

/** As partes do objetivo de uma Fusão, sempre na mesma ordem: duração, tipo, dia e hora. */
function partesDaFusao(
  codigos: CodigoCarta[],
  textos: { duracao: string | null; tipo: string; dia: string; hora: string },
): string[] {
  const partes: string[] = [];
  if (textos.duracao) partes.push(textos.duracao);
  if (codigos.includes("Variedade")) partes.push(textos.tipo);
  if (codigos.includes("FimDeSemana")) partes.push(textos.dia);
  if (codigos.includes("AntesDas8")) partes.push(textos.hora);
  return partes;
}

/** O efeito de fundir duas cartas da Mão, antes de jogar: o objetivo ainda não foi fixado. */
export function getEfeitoFusao(a: Carta, b: Carta): string {
  const minutos =
    (minutosExtras[a.codigo] ?? 0) + (minutosExtras[b.codigo] ?? 0);
  // Curto: a linha da Mão tem só duas linhas, e o prêmio vem primeiro.
  const partes = partesDaFusao([a.codigo, b.codigo], {
    duracao: minutos > 0 ? `com +${minutos} min` : null,
    tipo: "de outro tipo",
    dia: "no fim de semana",
    hora: "antes das 8h",
  });
  return `FUSÃO · ${getPremioFusao(a, b)} EN: um mesmo treino ${partes.join(" e ")}.`;
}

/** O objetivo de uma Jogada, já com o que foi fixado no envio ou no aceite. */
export function getObjetivo(jogada: Jogada): string {
  if (jogada.cartaFusao) {
    const partes = partesDaFusao([jogada.objetivo, jogada.cartaFusao.codigo], {
      duracao: jogada.duracaoAlvoSegundos
        ? `de ${formatarDuracao(jogada.duracaoAlvoSegundos)} ou mais`
        : null,
      tipo: jogada.tipoEvitar
        ? `que não seja ${tipoAtividadeLabels[jogada.tipoEvitar as TipoAtividade] ?? jogada.tipoEvitar}`
        : "de qualquer tipo",
      dia: "no sábado ou no domingo",
      hora: "que comece antes das 8h",
    });
    return `Um mesmo treino ${partes.join(" e ")}`;
  }
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

/** O nome da carta de uma Jogada; na Fusão, o das duas. */
export function getNomeJogada(jogada: Jogada): string {
  return jogada.cartaFusao
    ? `${jogada.carta.nome} + ${jogada.cartaFusao.nome}`
    : jogada.carta.nome;
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
  // Por sentido: uma carta aberta do alvo para quem vê não impede o revide (decisão 0069).
  if (jogadas.some((j) => ativa(j) && j.destinatarioId === alvoId))
    return `Você já tem uma carta aberta com ${nome}.`;
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
