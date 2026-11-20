// As datas de um desafio são dias do calendário ("2026-11-02"), sem hora nem fuso.
// As contas usam UTC para que o fuso do aparelho não desloque o dia.

const diaEmMs = 24 * 60 * 60 * 1000;

function toUtc(data: string): Date {
  return new Date(`${data}T00:00:00Z`);
}

function fromUtc(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function addDias(data: string, dias: number): string {
  return fromUtc(new Date(toUtc(data).getTime() + dias * diaEmMs));
}

/** O dia de hoje no fuso do grupo, que é o que o backend usa para validar as datas. */
export function getHojeNoFuso(fusoHorario: string): string {
  const partes = new Intl.DateTimeFormat("en-US", {
    timeZone: fusoHorario,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const parte = (tipo: Intl.DateTimeFormatPartTypes) =>
    partes.find(({ type }) => type === tipo)?.value ?? "";
  return `${parte("year")}-${parte("month")}-${parte("day")}`;
}

/** As próximas segundas-feiras, incluindo hoje se hoje for segunda. */
export function getProximasSegundas(hoje: string, total: number): string[] {
  const diaDaSemana = toUtc(hoje).getUTCDay();
  const primeira = addDias(hoje, (8 - diaDaSemana) % 7);
  return Array.from({ length: total }, (_, indice) =>
    addDias(primeira, indice * 7),
  );
}

/** A semana termina no domingo: a última de `semanas` semanas, contando a partir da segunda. */
export function getDataFim(dataInicio: string, semanas: number): string {
  return addDias(dataInicio, semanas * 7 - 1);
}

/** As semanas de um desafio, que sempre vai de uma segunda a um domingo. */
export function getTotalSemanas(dataInicio: string, dataFim: string): number {
  return Math.round(
    (toUtc(dataFim).getTime() - toUtc(dataInicio).getTime() + diaEmMs) /
      (7 * diaEmMs),
  );
}

/** Quanto falta para o fim do domingo no fuso do grupo, como "3 dias e 8h". */
export function formatarTempoAteFim(fim: string, fusoHorario: string): string {
  const hoje = getHojeNoFuso(fusoHorario);
  const dias = Math.max(
    0,
    Math.round((toUtc(fim).getTime() - toUtc(hoje).getTime()) / diaEmMs),
  );
  const hora = Number(
    new Intl.DateTimeFormat("en-US", {
      timeZone: fusoHorario,
      hour: "2-digit",
      hourCycle: "h23",
    }).format(new Date()),
  );
  const horas = 24 - hora;
  return dias > 0
    ? `${dias} ${dias === 1 ? "dia" : "dias"} e ${horas}h`
    : `${horas}h`;
}

export function formatarData(data: string): string {
  return toUtc(data).toLocaleDateString("pt-BR", {
    timeZone: "UTC",
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

export function formatarDiaEMes(data: string): string {
  return toUtc(data).toLocaleDateString("pt-BR", {
    timeZone: "UTC",
    day: "numeric",
    month: "short",
  });
}

/** "14/09": cabe nas colunas estreitas dos gráficos. */
export function formatarSemanaCurta(data: string): string {
  return `${data.slice(8)}/${data.slice(5, 7)}`;
}

export function formatarDuracao(segundos: number): string {
  return `${Math.round(segundos / 60)} min`;
}
