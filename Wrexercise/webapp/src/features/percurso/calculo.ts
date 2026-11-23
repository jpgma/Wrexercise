import type { TipoAtividade } from "@/features/atividades";

/**
 * As regras do Percurso (ADR 0084) que o aparelho usa para a estimativa ao vivo. O servidor é a referência: a
 * distância segue o algoritmo v1 de `Features/Atividades/MetricasPercurso.cs`, o mesmo do Movr (ADR 0085). A
 * estimativa só cobre os pontos que ainda não subiram; a cada lote, a distância volta a ser a do servidor.
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
/** No aparelho: um ponto novo só depois de andar isto (ou de passar o intervalo). */
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

const raioTerra = 6_371_008.8;

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

/** Os parâmetros de cada tipo no algoritmo v1 (os mesmos de `MetricasPercurso.cs` e do Movr). */
type Perfil = {
  velocidadeSalto: number;
  velocidadeMovimento: number;
  velocidadePonte: number;
  ruidoProcesso: number;
  cordaMinima: number;
};

const perfis: Record<"Caminhada" | "Corrida" | "Ciclismo", Perfil> = {
  Caminhada: {
    velocidadeSalto: 6,
    velocidadeMovimento: 0.5,
    velocidadePonte: 3,
    ruidoProcesso: 0.3,
    cordaMinima: 10,
  },
  Corrida: {
    velocidadeSalto: 10,
    velocidadeMovimento: 1,
    velocidadePonte: 7,
    ruidoProcesso: 0.8,
    cordaMinima: 10,
  },
  Ciclismo: {
    velocidadeSalto: 25,
    velocidadeMovimento: 1.5,
    velocidadePonte: 20,
    ruidoProcesso: 1.5,
    cordaMinima: 20,
  },
};

const perfilDe = (tipo: TipoAtividade): Perfil =>
  tipo === "Corrida"
    ? perfis.Corrida
    : tipo === "Ciclismo"
      ? perfis.Ciclismo
      : perfis.Caminhada;

const lacunaSegundos = 30;
const ponteSegundos = 120;
const janelaMovimentoSegundos = 15;
const descartesParaRecomecar = 5;
const varianciaVelocidadeInicial = 25;
const precisaoPorDesvio = 1.51;

/** Kalman de velocidade constante num eixo: estado (posição, velocidade). */
type Eixo = { pos: number; vel: number; p00: number; p01: number; p11: number };

const reiniciar = (e: Eixo, pos: number, variancia: number) => {
  e.pos = pos;
  e.vel = 0;
  e.p00 = variancia;
  e.p01 = 0;
  e.p11 = varianciaVelocidadeInicial;
};

function passo(
  e: Eixo,
  medida: number,
  varianciaMedida: number,
  dt: number,
  ruido: number,
) {
  e.pos += e.vel * dt;
  const q = ruido * ruido;
  const dt2 = dt * dt;
  const p00 = e.p00 + 2 * dt * e.p01 + dt2 * e.p11 + (q * dt2 * dt2) / 4;
  const p01 = e.p01 + dt * e.p11 + (q * dt2 * dt) / 2;
  const p11 = e.p11 + q * dt2;
  const s = p00 + varianciaMedida;
  const k0 = p00 / s;
  const k1 = p01 / s;
  const inovacao = medida - e.pos;
  e.pos += k0 * inovacao;
  e.vel += k1 * inovacao;
  e.p00 = (1 - k0) * p00;
  e.p01 = (1 - k0) * p01;
  e.p11 = p11 - k1 * p01;
}

const varianciaMedida = (p: PontoPercurso) => {
  const desvio = Math.max(1, p.precisao / precisaoPorDesvio);
  return desvio * desvio;
};

/**
 * A distância pelo algoritmo v1 (ADR 0085): precisão, saltos impossíveis (recomeça depois de 5 seguidos),
 * Kalman de velocidade constante, só em movimento, cordas mínimas sem o ruído da posição e lacunas de até
 * 2 min com velocidade plausível somadas em linha reta. Só a distância: o resto fica com o servidor.
 */
export function distanciaV1(
  pontos: PontoPercurso[],
  tipo: TipoAtividade,
): number {
  const perfil = perfilDe(tipo);
  const x: Eixo = { pos: 0, vel: 0, p00: 0, p01: 0, p11: 0 };
  const y: Eixo = { pos: 0, vel: 0, p00: 0, p01: 0, p11: 0 };
  let ancora: PontoPercurso | null = null;
  let ancoraMs = 0;
  let origemLat = 0;
  let origemLon = 0;
  let cosOrigem = 1;
  let temOrigem = false;
  let contadoX = 0;
  let contadoY = 0;
  let descartes = 0;
  let total = 0;
  let janela: { ms: number; x: number; y: number }[] = [];
  const rad = Math.PI / 180;

  const projetar = (p: PontoPercurso): [number, number] => [
    (p.longitude - origemLon) * rad * raioTerra * cosOrigem,
    (p.latitude - origemLat) * rad * raioTerra,
  ];

  const reancorar = (p: PontoPercurso, ms: number) => {
    if (!temOrigem) {
      origemLat = p.latitude;
      origemLon = p.longitude;
      cosOrigem = Math.cos(p.latitude * rad);
      temOrigem = true;
    }
    const [px, py] = projetar(p);
    const v = varianciaMedida(p);
    reiniciar(x, px, v);
    reiniciar(y, py, v);
    janela = [{ ms, x: px, y: py }];
    contadoX = px;
    contadoY = py;
    descartes = 0;
    ancora = p;
    ancoraMs = ms;
  };

  const saiuDoLugar = (ms: number) => {
    janela.push({ ms, x: x.pos, y: y.pos });
    while (
      janela.length > 2 &&
      (ms - (janela[1]?.ms ?? ms)) / 1000 >= janelaMovimentoSegundos
    )
      janela.shift();
    const inicio = janela[0];
    if (!inicio) return true;
    const duracao = (ms - inicio.ms) / 1000;
    if (duracao < janelaMovimentoSegundos) return true;
    return (
      Math.hypot(x.pos - inicio.x, y.pos - inicio.y) >=
      perfil.velocidadeMovimento * duracao
    );
  };

  for (const p of pontos) {
    if (!Number.isFinite(p.precisao) || p.precisao > precisaoMaximaMetros)
      continue;
    const ms = Date.parse(p.em);
    const atual: PontoPercurso | null = ancora;
    if (!atual || p.segmento !== atual.segmento) {
      reancorar(p, ms);
      continue;
    }
    const dt = (ms - ancoraMs) / 1000;
    if (dt <= 0) continue;
    const reta = haversine(
      atual.latitude,
      atual.longitude,
      p.latitude,
      p.longitude,
    );
    if (dt > lacunaSegundos) {
      if (dt <= ponteSegundos && reta / dt <= perfil.velocidadePonte)
        total += reta;
      reancorar(p, ms);
      continue;
    }
    if (reta / dt > perfil.velocidadeSalto) {
      descartes++;
      if (descartes >= descartesParaRecomecar) reancorar(p, ms);
      continue;
    }
    descartes = 0;
    const [px, py] = projetar(p);
    const v = varianciaMedida(p);
    passo(x, px, v, dt, perfil.ruidoProcesso);
    passo(y, py, v, dt, perfil.ruidoProcesso);
    const movendo =
      Math.hypot(x.vel, y.vel) >= perfil.velocidadeMovimento && saiuDoLugar(ms);
    const corda = Math.hypot(x.pos - contadoX, y.pos - contadoY);
    if (!movendo) {
      contadoX = x.pos;
      contadoY = y.pos;
    } else if (corda >= Math.max(perfil.cordaMinima, p.precisao)) {
      const ruido = 0.5 * (x.p00 + y.p00);
      total += Math.sqrt(Math.max(0, corda * corda - ruido));
      contadoX = x.pos;
      contadoY = y.pos;
    }
    ancora = p;
    ancoraMs = ms;
  }
  return total;
}

/**
 * A distância dos pontos ainda não enviados, a partir de `anterior` (o último que o servidor já tem), pelo
 * algoritmo v1. É uma estimativa: o filtro recomeça em `anterior`, e o servidor refaz tudo a cada lote.
 */
export function distanciaLocal(
  pontos: PontoPercurso[],
  tipo: TipoAtividade,
  anterior: PontoPercurso | null,
): number {
  return distanciaV1(anterior ? [anterior, ...pontos] : pontos, tipo);
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
