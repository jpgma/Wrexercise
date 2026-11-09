export type TipoAtividade =
  | "Musculacao"
  | "Corrida"
  | "Caminhada"
  | "Ciclismo"
  | "Natacao"
  | "Esporte"
  | "Yoga"
  | "Calistenia"
  | "Outro";

export type TipoComprovacao = "RegistroManual" | "Fotografia" | "CapturaDeTela";

export type SituacaoVerificacao =
  | "AguardandoFeedback"
  | "Verificada"
  | "EmRevisao"
  | "Contestada"
  | "Rejeitada";

export type Evidencia = {
  id: string;
  ordem: number;
  url: string;
};

export type AtividadeResumo = {
  id: string;
  grupoId: string;
  grupoNome: string;
  usuarioId: string;
  nomeExibicao: string;
  tipo: TipoAtividade;
  duracaoSegundos: number;
  startedAt: string;
  tipoComprovacao: TipoComprovacao;
  situacaoVerificacao: SituacaoVerificacao;
  totalEvidencias: number;
  submittedAt: string;
  descricao: string | null;
  kudos: number;
  suspeito: number;
  rejeitar: number;
  miniaturaUrl: string | null;
  comentarios: number;
};

export type FiltroAtividades =
  "Minhas" | "Pendentes" | "Verificadas" | "EmRevisao";

export type Atividade = Omit<
  AtividadeResumo,
  | "kudos"
  | "suspeito"
  | "rejeitar"
  | "totalEvidencias"
  | "miniaturaUrl"
  | "comentarios"
> & {
  voce: boolean;
  evidencias: Evidencia[];
};

export type CriarAtividadeData = {
  tipo: TipoAtividade;
  duracaoSegundos: number;
  startedAt: string;
  descricao: string;
  tipoComprovacao: TipoComprovacao;
};
