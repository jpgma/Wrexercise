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
} as const satisfies Record<string, Animacao>;

export type NomeAnimacao = keyof typeof animacoes;

/** Variações ocasionais de quem está parado. */
export const variacoesDeParado = ["alongar", "olhar"] as const;
