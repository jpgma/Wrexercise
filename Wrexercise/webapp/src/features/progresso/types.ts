export type ClassificacaoParticipante = {
  posicao: number;
  usuarioId: string;
  nomeExibicao: string;
  semanasCumpridas: number;
  sessoesVerificadas: number;
  sequenciaSemanas: number;
};

export type Classificacao = {
  sessoesPorSemana: number;
  totalSemanas: number;
  participantes: ClassificacaoParticipante[];
};

export type HistoricoSemana = {
  numero: number;
  inicio: string;
  fim: string;
  sessoesVerificadas: number;
  sessoesProvisorias: number;
  metaCumprida: boolean;
  participou: boolean;
};

export type HistoricoSemanal = {
  sessoesPorSemana: number;
  totalSemanas: number;
  semanas: HistoricoSemana[];
};

export type ResumoGrupo = {
  grupoId: string;
  desafio: {
    id: string;
    nome: string;
    situacao: string;
    sessoesPorSemana: number;
    duracaoMinimaSegundos: number;
  } | null;
  sessoesPorSemana: number | null;
  meuProgresso: {
    sessoesVerificadas: number;
    sessoesProvisorias: number;
    metaCumprida: boolean;
    semanaNumero: number | null;
    contaNestaSemana: boolean;
    /** Dias até a semana fechar, contando hoje: 7 na segunda, 1 no domingo. */
    diasRestantes: number;
  } | null;
  /** Todos os participantes, na ordem da classificação (ADR 0066). */
  classificacao: ClassificacaoResumoItem[];
  /** A chamada de disputa de quem vê: só com o desafio `Ativo`. */
  proximo: ProximoClassificacao | null;
};

export type ClassificacaoResumoItem = {
  posicao: number;
  usuarioId: string;
  nomeExibicao: string;
  semanasCumpridas: number;
  sessoesVerificadas: number;
  sequenciaSemanas: number;
  /** A posição no fim da semana anterior; `null` na primeira semana ou fora de `Ativo`. */
  posicaoInicioSemana: number | null;
};

export type ProximoClassificacao = {
  tipo: "Lider" | "Treinos" | "Meta" | "Semanas";
  nomeExibicao: string;
  quantidade: number;
};
