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
  } | null;
  classificacao: {
    posicao: number;
    usuarioId: string;
    nomeExibicao: string;
    semanasCumpridas: number;
  }[];
};
