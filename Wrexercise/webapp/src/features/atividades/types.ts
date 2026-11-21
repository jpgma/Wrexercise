export type TipoAtividade =
  | "Musculacao"
  | "Corrida"
  | "Caminhada"
  | "Ciclismo"
  | "Natacao"
  | "Futebol"
  | "Volei"
  | "Luta"
  | "Esporte"
  | "Yoga"
  | "Calistenia"
  | "Outro";

export type TipoComprovacao = "RegistroManual" | "Fotografia" | "CapturaDeTela";

export type SituacaoVerificacao =
  /** Treino ao vivo ainda não terminado (ADR 0078): não conta para nada e ninguém avalia. */
  | "EmAndamento"
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
  /** Captura de tela, pelo nome do arquivo ou pelo tamanho da tela (ADR 0060). Sinal do navegador, não prova. */
  print: boolean;
  /** Só quando o autor escolheu mostrar a localização ao grupo (ADR 0061). */
  latitude: number | null;
  longitude: number | null;
  /** Quando a foto ou o vídeo foi feito, e de onde veio essa data (ADR 0052, 0080). Sinal, não prova. */
  capturadaEm: string | null;
  origemDataCaptura: OrigemDataCaptura | null;
  /** Chegou depois do prazo da verificação e não contou para ela (ADR 0083). */
  adicionadaDepois: boolean;
};

export type OrigemDataCaptura =
  "Exif" | "Arquivo" | "Camera" | "Print" | "Integracao";

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
  /** Se quem vê pode corrigir a duração (ADR 0068): administrador, na atividade de outra pessoa. */
  correcaoPermitida: boolean;
  correcoes: CorrecaoDuracao[];
  /** Onde o treino ao vivo começou, só quando a pessoa escolheu mostrar (ADR 0080). */
  latitude: number | null;
  longitude: number | null;
  /** O autor ainda manda fotos e vídeos por aqui; depois do prazo, com o selo "Adicionada depois" (ADR 0083). */
  anexoPermitido: boolean;
};

/** Uma correção da duração feita por um administrador (ADR 0068). */
export type CorrecaoDuracao = {
  responsavelNome: string;
  duracaoAnteriorSegundos: number;
  duracaoNovaSegundos: number;
  justificativa: string;
  criadaEm: string;
};

export type CriarAtividadeData = {
  tipo: TipoAtividade;
  duracaoSegundos: number;
  startedAt: string;
  descricao: string;
  tipoComprovacao: TipoComprovacao;
  /** ADR 0065: o rascunho de um app conectado, cuja imagem vira a primeira evidência. */
  rascunhoId?: string;
};

/** Uma Torcida recebida no treino ao vivo (ADR 0078). */
export type Torcida = {
  usuarioId: string;
  nomeExibicao: string;
  criadaEm: string;
};

/** O treino ao vivo de quem usa o aplicativo, em qualquer grupo (ADR 0078). */
export type TreinoAtual = {
  atividadeId: string;
  grupoId: string;
  grupoNome: string;
  tipo: TipoAtividade;
  startedAt: string;
  totalEvidencias: number;
  /** As mais recentes primeiro. */
  torcidas: Torcida[];
  /** O app conectado que começou o treino (ADR 0081), ou `null` quando começou aqui. */
  app: "Movr" | null;
  /** Tempo ativo informado pelo app ao acabar, sem pausas: só uma sugestão. */
  appDuracaoSegundos: number | null;
  /** Quando o treino acabou no app; `null` enquanto ele continua lá. */
  appEndedAt: string | null;
};

export type TerminarTreinoData = {
  duracaoSegundos: number;
  descricao: string;
  tipoComprovacao: TipoComprovacao;
};
