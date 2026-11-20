import type { ClassificacaoParticipante } from "./types";

type Estatisticas = Pick<
  ClassificacaoParticipante,
  | "nomeExibicao"
  | "semanasCumpridas"
  | "sessoesVerificadas"
  | "sequenciaSemanas"
>;

const plural = (n: number, singular: string, varios: string) =>
  `${n} ${n === 1 ? singular : varios}`;

/**
 * Por que uma pessoa está à frente da seguinte, pelo critério que decidiu: semanas cumpridas, dias
 * verificados, sequência e, por fim, o nome (mesma ordem de `ClassificacaoDesafio.CalcularRanking`).
 * `nomeDeBaixo` já vem pronto: o primeiro nome ou "você".
 */
export function explicarVantagem(
  acima: Estatisticas,
  abaixo: Estatisticas,
  nomeDeBaixo: string,
): string {
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
  return `Empate com ${nomeDeBaixo}`;
}
