/**
 * As animações do Jogo, ligadas às linhas da folha em 8 direções (docs/avatar-formato.md §6, ADR 0039).
 * Os valores seguem `pixel-art/jpgma/saida/jpgma.json`. Nunca mude os tempos: todos veem a mesma animação.
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
  andar: { linha: 1, quadros: 8, ms: 200 },
  correr: { linha: 9, quadros: 8, ms: 200 },
  arremessar: { linha: 17, quadros: 7, ms: 200 },
  comemorar: { linha: 25, quadros: 9, ms: 200 },
  receber: { linha: 33, quadros: 6, ms: 200 },
  parado: { linha: 41, quadros: 4, ms: 200 },
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

// `alongar`, `olhar`, `aceitar`, `recusar` e `provocar` ainda não têm arte: usam `parado`
// (ADR 0039, Andamento). `treinando` usa `correr` e `comemorando` usa `comemorar`.
export const animacoes = {
  parado: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
  treinando: { ...linhasFolha.correr, repete: true, quadroEstatico: 2 },
  comemorando: { ...linhasFolha.comemorar, repete: true, quadroEstatico: 4 },
  alongar: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
  olhar: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
  andar: { ...linhasFolha.andar, repete: true, quadroEstatico: 0 },
  arremessar: { ...linhasFolha.arremessar, repete: false, quadroEstatico: 3 },
  receber: { ...linhasFolha.receber, repete: false, quadroEstatico: 2 },
  aceitar: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
  recusar: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
  provocar: { ...linhasFolha.parado, repete: true, quadroEstatico: 0 },
} as const satisfies Record<string, Animacao>;

export type NomeAnimacao = keyof typeof animacoes;

/** A carta sai da mão no quarto quadro do arremesso (índice 3). */
export const quadroSoltura = 3;
/** A carta atinge o peito no terceiro quadro do `receber` (índice 2). */
export const quadroImpacto = 2;

/** Duração total de uma animação que toca uma vez. */
export function getDuracao(animacao: NomeAnimacao): number {
  const { quadros, ms } = animacoes[animacao];
  return quadros * ms;
}

/**
 * Variações ocasionais de quem está parado. Vazia enquanto `alongar` e `olhar` não têm arte:
 * tocar o próprio `parado` de novo só reiniciaria a respiração.
 */
export const variacoesDeParado: readonly NomeAnimacao[] = [];

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
