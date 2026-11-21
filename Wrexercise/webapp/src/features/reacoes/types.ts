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

/** Texto, uma foto ou um vídeo, ou os dois (ADR 0083): um deles sempre vem. */
export type Comentario = {
  id: string;
  usuarioId: string;
  nomeExibicao: string;
  texto: string | null;
  createdAt: string;
  midia: ComentarioMidia | null;
};

export type ComentarioMidia = {
  url: string;
  miniaturaUrl: string | null;
  tipo: "Foto" | "Video";
  /** Só no vídeo. */
  duracaoMs: number | null;
};

export type RevisaoAtividadeData = {
  decisao: "Verificada" | "Rejeitada" | "Anulada";
  justificativa: string;
};
