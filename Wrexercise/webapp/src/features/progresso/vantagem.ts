import { formatarDuracao } from "@/features/atividades";
import type { ClassificacaoParticipante } from "./types";

type Estatisticas = Pick<
  ClassificacaoParticipante,
  | "posicao"
  | "semanasCumpridas"
  | "sessoesVerificadas"
  | "sequenciaSemanas"
  | "segundosVerificados"
  | "atividadesVerificadas"
>;

const plural = (n: number, singular: string, varios: string) =>
  `${n} ${n === 1 ? singular : varios}`;

/**
 * Por que uma pessoa está à frente da seguinte, pelo critério que decidiu: semanas cumpridas, dias
 * verificados, sequência, tempo verificado e atividades verificadas (mesma ordem de
 * `ClassificacaoDesafio.CalcularRanking`, ADR 0090). Quem empata em tudo divide a posição.
 * `nomeDeBaixo` já vem pronto: o primeiro nome ou "você".
 */
export function explicarVantagem(
  acima: Estatisticas,
  abaixo: Estatisticas,
  nomeDeBaixo: string,
): string {
  if (acima.posicao === abaixo.posicao) {
    return `Empate em tudo com ${nomeDeBaixo}: mesma posição`;
  }
  const semanas = acima.semanasCumpridas - abaixo.semanasCumpridas;
  if (semanas > 0) {
    return `${plural(semanas, "semana", "semanas")} à frente de ${nomeDeBaixo}`;
  }
  const dias = acima.sessoesVerificadas - abaixo.sessoesVerificadas;
  if (dias > 0) {
    return `Empate em semanas, ${plural(dias, "dia", "dias")} a mais que ${nomeDeBaixo}`;
  }
  if (acima.sequenciaSemanas > abaixo.sequenciaSemanas) {
    return `Empate em semanas e dias, sequência maior que ${nomeDeBaixo}`;
  }
  const segundos = acima.segundosVerificados - abaixo.segundosVerificados;
  if (segundos > 0) {
    const tempo = segundos < 60 ? "menos de 1 min" : formatarDuracao(segundos);
    return `Empate em semanas, dias e sequência, ${tempo} a mais de treino que ${nomeDeBaixo}`;
  }
  const atividades = acima.atividadesVerificadas - abaixo.atividadesVerificadas;
  return `Empate até no tempo, ${plural(atividades, "atividade", "atividades")} a mais que ${nomeDeBaixo}`;
}
