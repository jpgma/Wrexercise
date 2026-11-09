export type TipoReacao = "Kudos" | "Suspeito" | "Rejeitar";

export type ReacaoItem = {
  usuarioId: string;
  nomeExibicao: string;
  tipo: TipoReacao;
  updatedAt: string;
};

export type ReacoesDetalhe = {
  totalRevisores: number;
  rejeicoesNecessarias: number;
  kudos: number;
  suspeito: number;
  rejeitar: number;
  suaReacao: TipoReacao | null;
  voceERevisor: boolean;
  janelaAberta: boolean;
  revisaoEncerraEm: string;
  reacoes: ReacaoItem[];
};

export type Comentario = {
  id: string;
  usuarioId: string;
  nomeExibicao: string;
  texto: string;
  createdAt: string;
};

export type RevisaoAtividadeData = {
  decisao: "Verificada" | "Rejeitada";
  justificativa: string;
};
