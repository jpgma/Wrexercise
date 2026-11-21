import type { TipoAtividade } from "@/features/atividades";

/**
 * As regras do Percurso (ADR 0084) que o aparelho usa para a estimativa ao vivo. O servidor é a referência:
 * as mesmas constantes estão em `Features/Atividades/PercursoAtividade.cs`. A estimativa só cobre os pontos que
 * ainda não subiram; a cada lote, a distância volta a ser a do servidor.
 */

export type PontoPercurso = {
  seq: number;
  /** ISO 8601. */
  em: string;
  latitude: number;
  longitude: number;
  precisao: number;
  altitude: number | null;
  segmento: number;
};

/** Acima disso, o ponto é ruído. */
export const precisaoMaximaMetros = 30;
/** Mais tempo que isso sem ponto vira lacuna. */
export const lacunaMs = 30_000;
/** O tremor do GPS parado. */
export const deslocamentoMinimoMetros = 5;
/** No aparelho: um ponto a cada 3 s ou a cada 5 m, o que vier primeiro. */
export const intervaloMinimoMs = 3000;

export const tiposComPercurso: readonly TipoAtividade[] = [
  "Corrida",
  "Caminhada",
  "Ciclismo",
];

export const isTipoComPercurso = (tipo: TipoAtividade) =>
  tiposComPercurso.includes(tipo);

/** A velocidade acima da qual um salto entre dois pontos é erro do GPS, em m/s. */
export const velocidadeLimite = (tipo: TipoAtividade) =>
  tipo === "Caminhada" ? 6 : tipo === "Corrida" ? 10 : 25;

const raioTerra = 6_371_000;

export function haversine(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number,
): number {
  const rad = Math.PI / 180;
  const dLat = (lat2 - lat1) * rad;
  const dLon = (lon2 - lon1) * rad;
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(lat1 * rad) * Math.cos(lat2 * rad) * Math.sin(dLon / 2) ** 2;
  return 2 * raioTerra * Math.asin(Math.min(1, Math.sqrt(a)));
}

/**
 * A distância dos pontos ainda não enviados, a partir de `anterior` (o último que o servidor já tem). Mesmo
 * filtro do servidor, simplificado: sem precisão ruim, sem lacunas, sem saltos impossíveis e sem o tremor.
 */
export function distanciaLocal(
  pontos: PontoPercurso[],
  tipo: TipoAtividade,
  anterior: PontoPercurso | null,
): number {
  const limite = velocidadeLimite(tipo);
  let ultimo = anterior;
  let total = 0;
  for (const p of pontos) {
    if (p.precisao > precisaoMaximaMetros) continue;
    if (
      !ultimo ||
      p.segmento !== ultimo.segmento ||
      Date.parse(p.em) - Date.parse(ultimo.em) > lacunaMs
    ) {
      ultimo = p;
      continue;
    }
    const metros = haversine(
      ultimo.latitude,
      ultimo.longitude,
      p.latitude,
      p.longitude,
    );
    const segundos = (Date.parse(p.em) - Date.parse(ultimo.em)) / 1000;
    if (segundos <= 0 || metros / segundos > limite) continue;
    // O mesmo limiar do servidor: 5 m ou a precisão média dos dois pontos, o que for maior.
    if (
      metros <
      Math.max(deslocamentoMinimoMetros, (ultimo.precisao + p.precisao) / 2)
    )
      continue;
    total += metros;
    ultimo = p;
  }
  return total;
}

const numero = (valor: number, casas: number) =>
  valor.toLocaleString("pt-BR", {
    minimumFractionDigits: casas,
    maximumFractionDigits: casas,
  });

/** "850 m", "3,42 km", "42,2 km". */
export function formatarDistancia(metros: number): string {
  if (metros < 1000) return `${Math.round(metros)} m`;
  const km = metros / 1000;
  return `${numero(km, km < 10 ? 2 : 1)} km`;
}

/** O ritmo em min/km, a partir da velocidade em m/s: "5:42 /km". */
export function formatarRitmo(velocidade: number | null): string {
  if (!velocidade || velocidade <= 0) return "—";
  const segundosPorKm = Math.round(1000 / velocidade);
  if (segundosPorKm >= 60 * 60) return "—";
  const min = Math.floor(segundosPorKm / 60);
  const seg = segundosPorKm % 60;
  return `${min}:${String(seg).padStart(2, "0")} /km`;
}

/** A velocidade em km/h, a partir de m/s: "24,3 km/h". */
export function formatarVelocidade(velocidade: number | null): string {
  if (velocidade === null || velocidade <= 0) return "—";
  return `${numero(velocidade * 3.6, 1)} km/h`;
}

/** Ciclismo mostra a velocidade; corrida e caminhada, o ritmo. */
export const usaVelocidade = (tipo: TipoAtividade) => tipo === "Ciclismo";

export function formatarMedia(
  tipo: TipoAtividade,
  velocidade: number | null,
): string {
  return usaVelocidade(tipo)
    ? formatarVelocidade(velocidade)
    : formatarRitmo(velocidade);
}

/** "5:42" para uma parcial (min:seg), ou "1:02:10" acima de uma hora. */
export function formatarTempoParcial(segundos: number): string {
  const h = Math.floor(segundos / 3600);
  const m = Math.floor((segundos % 3600) / 60);
  const s = Math.round(segundos % 60);
  const mm = h > 0 ? String(m).padStart(2, "0") : String(m);
  return `${h > 0 ? `${h}:` : ""}${mm}:${String(s).padStart(2, "0")}`;
}
