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
  | "Rejeitada"
  | "Anulada";

export type Evidencia = {
  id: string;
  ordem: number;
  url: string;
  /** A câmera do aplicativo fez a captura (ADR 0058). Sinal do navegador, não prova. */
  gravadaNoApp: boolean;
  tipo: "Foto" | "Video";
  /** Só no vídeo. */
  duracaoMs: number | null;
  /** Captura de tela com a data no nome do arquivo (ADR 0060). Sinal do navegador, não prova. */
  print: boolean;
};

/** Avisos sobre a atividade (ADR 0052), vistos por todo o grupo. */
export type TipoAviso =
  | "FotoForaDoPeriodo"
  | "FotoSemData"
  | "FotoParecida"
  | "RegistroTardio"
  | "VideoParado";

export type Aviso = {
  tipo: TipoAviso;
  evidenciaId: string | null;
  /** Só quando quem vê também pode ver a atividade da foto parecida. */
  atividadeRelacionadaId: string | null;
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
  /** Quando a primeira evidência é um vídeo: a miniatura ganha o símbolo de play e a duração. */
  miniaturaDuracaoMs: number | null;
  comentarios: number;
  avisos: TipoAviso[];
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
  | "miniaturaDuracaoMs"
  | "comentarios"
  | "avisos"
> & {
  voce: boolean;
  evidencias: Evidencia[];
  avisos: Aviso[];
  /** Até quando o autor pode apagar (ADR 0054); `null` quando não pode. */
  exclusaoPermitidaAte: string | null;
};

export type CriarAtividadeData = {
  tipo: TipoAtividade;
  duracaoSegundos: number;
  startedAt: string;
  descricao: string;
  tipoComprovacao: TipoComprovacao;
};
