/**
 * As animações do Jogo, ligadas às linhas da folha em 8 direções (docs/avatar-formato.md §6, ADR 0039).
 * Os valores seguem `pixel-art/base-masculino/saida/base-masculino.json`. Nunca mude os tempos: todos veem a mesma animação.
 */
export { formatoFolha as folha } from "@/components/folhaAvatar";

/** Ordem das direções nas linhas da folha. `sul` olha para quem vê a tela. */
export const direcoes = [
  "sul",
  "sudeste",
  "leste",
  "nordeste",
  "norte",
  "noroeste",
  "oeste",
  "sudoeste",
] as const;

export type Direcao = (typeof direcoes)[number];

/**
 * As animações da folha: cada uma ocupa 8 linhas, uma por direção. A linha 0 é a `rotacao`, com uma
 * direção por coluna. Também desenha o guia de edição (guiaFolha.ts).
 */
export const linhasFolha = {
  andar: { linha: 1, quadros: 8, ms: 100 },
  correr: { linha: 9, quadros: 8, ms: 100 },
  arremessar: { linha: 17, quadros: 6, ms: 80 },
  comemorar: { linha: 25, quadros: 9, ms: 120 },
  receber: { linha: 33, quadros: 6, ms: 100 },
  parado: { linha: 41, quadros: 4, ms: 200 },
  treinando: { linha: 49, quadros: 6, ms: 90 },
  alongar: { linha: 57, quadros: 4, ms: 150 },
  olhar: { linha: 65, quadros: 4, ms: 200 },
  aceitar: { linha: 73, quadros: 4, ms: 120 },
  recusar: { linha: 81, quadros: 3, ms: 150 },
  provocar: { linha: 89, quadros: 4, ms: 120 },
  // As 4 de dormir: só na folha de 129 linhas (formatoFolha.linhasSemDormir).
  dormindo: { linha: 97, quadros: 4, ms: 350 },
  adormecer: { linha: 105, quadros: 8, ms: 140 },
  acordar: { linha: 113, quadros: 5, ms: 110 },
  levantar: { linha: 121, quadros: 8, ms: 120 },
} as const;

export type Animacao = {
  /** Primeira linha (direção `sul`) da animação na folha. */
  linha: number;
  quadros: number;
  /** Duração de cada quadro, em ms. */
  ms: number;
  repete: boolean;
  /** Quadro mostrado quando o movimento é reduzido: o que melhor identifica a pose. */
  quadroEstatico: number;
};

// `comemorando` usa a linha `comemorar`. `correr` é a entrada na praça (docs/gamificacao.md §3.4).
export const animacoes = {
  correr: { ...linhasFolha.correr, repete: true, quadroEstatico: 0 },
  parado: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
  treinando: { ...linhasFolha.treinando, repete: true, quadroEstatico: 2 },
  comemorando: { ...linhasFolha.comemorar, repete: true, quadroEstatico: 4 },
  alongar: { ...linhasFolha.alongar, repete: false, quadroEstatico: 2 },
  olhar: { ...linhasFolha.olhar, repete: false, quadroEstatico: 1 },
  andar: { ...linhasFolha.andar, repete: true, quadroEstatico: 0 },
  arremessar: { ...linhasFolha.arremessar, repete: false, quadroEstatico: 3 },
  receber: { ...linhasFolha.receber, repete: false, quadroEstatico: 2 },
  aceitar: { ...linhasFolha.aceitar, repete: false, quadroEstatico: 3 },
  recusar: { ...linhasFolha.recusar, repete: false, quadroEstatico: 2 },
  provocar: { ...linhasFolha.provocar, repete: false, quadroEstatico: 2 },
  dormindo: { ...linhasFolha.dormindo, repete: true, quadroEstatico: 0 },
  adormecer: { ...linhasFolha.adormecer, repete: false, quadroEstatico: 7 },
  acordar: { ...linhasFolha.acordar, repete: false, quadroEstatico: 1 },
  levantar: { ...linhasFolha.levantar, repete: false, quadroEstatico: 7 },
} as const satisfies Record<string, Animacao>;

export type NomeAnimacao = keyof typeof animacoes;

/** A carta sai da mão no quarto quadro do arremesso (índice 3). */
export const quadroSoltura = 3;
/** A carta atinge o peito no terceiro quadro do `receber` (índice 2). */
export const quadroImpacto = 2;
/** Quem dorme e é atingido pula de susto no segundo quadro do `acordar` (índice 1). */
export const quadroSusto = 1;

/** Duração total de uma animação que toca uma vez. */
export function getDuracao(animacao: NomeAnimacao): number {
  const { quadros, ms } = animacoes[animacao];
  return quadros * ms;
}

/** Variações ocasionais de quem está parado. */
export const variacoesDeParado: readonly NomeAnimacao[] = ["alongar", "olhar"];

/** A direção, das 8, mais próxima do vetor de `de` para `para` na tela (y para baixo). */
export function getDirecao(
  de: { x: number; y: number },
  para: { x: number; y: number },
): Direcao {
  const dx = para.x - de.x;
  const dy = para.y - de.y;
  if (Math.abs(dx) < 1 && Math.abs(dy) < 1) return "sul";
  const graus = (Math.atan2(dy, dx) * 180) / Math.PI;
  const indice = (((Math.round((90 - graus) / 45) % 8) + 8) % 8) as number;
  return direcoes[indice] ?? "sul";
}

/** O caminho mais curto de uma direção a outra, passando pelas vizinhas, sem a de partida. */
export function getGiro(de: Direcao, para: Direcao): Direcao[] {
  const inicio = direcoes.indexOf(de);
  const fim = direcoes.indexOf(para);
  const voltas = (fim - inicio + 8) % 8;
  const sentido = voltas <= 4 ? 1 : -1;
  const passos = sentido === 1 ? voltas : 8 - voltas;
  return Array.from(
    { length: passos },
    (_, i) => direcoes[(inicio + sentido * (i + 1) + 8) % 8] ?? para,
  );
}
