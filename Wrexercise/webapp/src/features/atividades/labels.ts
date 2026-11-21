import type {
  Evidencia,
  OrigemDataCaptura,
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
  Futebol: "Futebol",
  Volei: "Vôlei",
  Luta: "Luta",
  Yoga: "Yoga",
  Calistenia: "Calistenia",
  Esporte: "Outro esporte",
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
  EmAndamento: "Ao vivo",
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

/** O cronômetro do treino ao vivo: "12:05" ou "1:02:05". */
export function formatarCronometro(segundos: number): string {
  const total = Math.max(0, Math.floor(segundos));
  const horas = Math.floor(total / 3600);
  const minutos = Math.floor((total % 3600) / 60);
  const resto = String(total % 60).padStart(2, "0");
  return horas > 0
    ? `${horas}:${String(minutos).padStart(2, "0")}:${resto}`
    : `${minutos}:${resto}`;
}

/** De onde veio a data da captura (ADR 0052, 0060, 0065). "Data do arquivo" é o sinal mais fraco. */
export function origemDataCapturaLabel(
  origem: OrigemDataCaptura,
  tipo: Evidencia["tipo"],
): string {
  switch (origem) {
    case "Exif":
      return tipo === "Video" ? "Dados do vídeo" : "Dados da foto";
    case "Arquivo":
      return "Data do arquivo";
    case "Camera":
      return "Câmera do app";
    case "Print":
      return "Nome do print";
    case "Integracao":
      return "Movr";
    case "Percurso":
      return "Percurso gravado no app";
  }
}

function formatarIntervalo(ms: number): string {
  const minutos = Math.round(ms / 60_000);
  if (minutos < 1) return "menos de 1 min";
  if (minutos < 60) return `${minutos} min`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) {
    const resto = minutos % 60;
    return resto > 0 ? `${horas}h ${resto}min` : `${horas}h`;
  }
  const dias = Math.round(horas / 24);
  return dias === 1 ? "1 dia" : `${dias} dias`;
}

/**
 * Quando a captura aconteceu em relação ao treino: "12 min antes do início", "Durante o treino" ou
 * "3h depois do fim". No treino ao vivo, o fim ainda não existe.
 */
export function descreverMomentoCaptura(
  capturadaEm: string,
  startedAt: string,
  duracaoSegundos: number,
  aoVivo = false,
): string {
  const captura = new Date(capturadaEm).getTime();
  const inicio = new Date(startedAt).getTime();
  if (captura < inicio) {
    return `${formatarIntervalo(inicio - captura)} antes do início`;
  }
  const fim = inicio + duracaoSegundos * 1000;
  if (aoVivo || captura <= fim) return "Durante o treino";
  return `${formatarIntervalo(captura - fim)} depois do fim`;
}
