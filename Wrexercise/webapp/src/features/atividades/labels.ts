import type {
  SituacaoVerificacao,
  TipoAtividade,
  TipoComprovacao,
} from "./types";

export const tipoAtividadeLabels: Record<TipoAtividade, string> = {
  Musculacao: "Musculação",
  Corrida: "Corrida",
  Caminhada: "Caminhada",
  Ciclismo: "Ciclismo",
  Natacao: "Natação",
  Esporte: "Esporte",
  Yoga: "Yoga",
  Calistenia: "Calistenia",
  Outro: "Outro",
};

export const tiposAtividade = Object.keys(
  tipoAtividadeLabels,
) as TipoAtividade[];

export const tipoComprovacaoLabels: Record<TipoComprovacao, string> = {
  RegistroManual: "Registro manual",
  Fotografia: "Fotografia",
  CapturaDeTela: "Captura de tela",
};

export const situacaoVerificacaoLabels: Record<SituacaoVerificacao, string> = {
  AguardandoFeedback: "Aguardando avaliação",
  Verificada: "Verificada",
  EmRevisao: "Em revisão",
  Contestada: "Contestada",
  Rejeitada: "Rejeitada",
  Anulada: "Anulada",
};

export function formatarDuracao(segundos: number): string {
  const horas = Math.floor(segundos / 3600);
  const minutos = Math.floor((segundos % 3600) / 60);
  if (horas > 0 && minutos > 0) return `${horas}h ${minutos}min`;
  if (horas > 0) return `${horas}h`;
  return `${minutos} min`;
}

/** "Hoje, 15:30" e "Ontem, 07:10" para o que é recente; a data por extenso para o resto. */
export function formatarDataRelativa(iso: string, agora = new Date()): string {
  const data = new Date(iso);
  const hora = data.toLocaleTimeString("pt-BR", {
    hour: "2-digit",
    minute: "2-digit",
  });
  const inicioDoDia = (d: Date) =>
    new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  const dias = Math.round(
    (inicioDoDia(agora) - inicioDoDia(data)) / (24 * 60 * 60 * 1000),
  );
  if (dias === 0) return `Hoje, ${hora}`;
  if (dias === 1) return `Ontem, ${hora}`;
  return new Intl.DateTimeFormat("pt-BR", {
    weekday: "short",
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  })
    .format(data)
    .replaceAll(".", "");
}

export function formatarDataHora(iso: string): string {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(iso));
}
