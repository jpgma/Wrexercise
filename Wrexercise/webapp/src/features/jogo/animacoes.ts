/** Linhas, quadros e tempos da folha de avatar (docs/avatar-formato.md §3). Nunca mude os tempos. */
export type Animacao = {
  linha: number;
  quadros: number;
  /** Duração de cada quadro, em ms. */
  ms: number;
  repete: boolean;
  /** Quadro mostrado quando o movimento é reduzido: o que melhor identifica a pose. */
  quadroEstatico: number;
};

export const animacoes = {
  parado: { linha: 1, quadros: 4, ms: 200, repete: true, quadroEstatico: 0 },
  treinando: { linha: 3, quadros: 6, ms: 90, repete: true, quadroEstatico: 3 },
  comemorando: {
    linha: 4,
    quadros: 6,
    ms: 120,
    repete: true,
    quadroEstatico: 1,
  },
  alongar: { linha: 5, quadros: 4, ms: 150, repete: false, quadroEstatico: 0 },
  olhar: { linha: 6, quadros: 4, ms: 200, repete: false, quadroEstatico: 0 },
  andar: { linha: 2, quadros: 8, ms: 100, repete: true, quadroEstatico: 0 },
  /** A carta sai da mão no terceiro quadro. */
  arremessar: {
    linha: 7,
    quadros: 5,
    ms: 80,
    repete: false,
    quadroEstatico: 2,
  },
  receber: { linha: 8, quadros: 4, ms: 100, repete: false, quadroEstatico: 2 },
  aceitar: { linha: 9, quadros: 4, ms: 120, repete: false, quadroEstatico: 3 },
  recusar: { linha: 10, quadros: 3, ms: 150, repete: false, quadroEstatico: 2 },
} as const satisfies Record<string, Animacao>;

/** Duração total de uma animação que toca uma vez. */
export function getDuracao(animacao: NomeAnimacao): number {
  const { quadros, ms } = animacoes[animacao];
  return quadros * ms;
}

export type NomeAnimacao = keyof typeof animacoes;

/** Variações ocasionais de quem está parado. */
export const variacoesDeParado = ["alongar", "olhar"] as const;
