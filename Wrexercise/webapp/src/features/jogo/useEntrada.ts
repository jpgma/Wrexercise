import { useCallback, useEffect, useRef, useState } from "react";
import { getDirecao } from "./animacoes";
import type { Atuacao, Ponto } from "./useEncenacoes";

// O dia da última entrada fica num cookie comum, não em `localStorage`, que o ESLint proíbe.
const cookieEntrada = "wx_entrada_praca";
/** Velocidade da corrida, em px da praça por ms. */
const Velocidade = 0.3;
/** Intervalo entre a largada de um avatar e a do próximo, pela borda de baixo. */
const Intervalo = 120;
/** Intervalo entre um avatar e o próximo na porta: um de cada vez. */
const IntervaloPorta = 420;
/** O passo para fora da porta, andando, antes de correr até o lugar. */
const PassoPorta = 24;
const PassoPortaMs = 220;
/** Até 3 pessoas, todos saem pela porta. */
const MaxSoPelaPorta = 3;
/** A faixa da borda de baixo por onde se entra: fora dela, ficam as floreiras dos cantos. */
const MargemDeBaixo = 90;

function hoje(): string {
  const d = new Date();
  return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`;
}

function jaEntrouHoje(): boolean {
  return document.cookie
    .split(";")
    .some((parte) => parte.trim() === `${cookieEntrada}=${hoje()}`);
}

function registrarEntrada() {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieEntrada}=${hoje()}; Max-Age=${2 * 24 * 60 * 60}; Path=/; SameSite=Lax${seguro}`;
}

type Chegada = {
  id: string;
  p: Ponto;
  /** Onde a corrida começa: na borda de baixo ou um passo à frente da porta. */
  de: Ponto;
  pelaPorta: boolean;
};

/**
 * Quem fica na metade de trás da elipse sai pela porta; quem fica na frente sobe pela borda de baixo.
 * Ninguém atravessa parede. Em grupos pequenos, todos usam a porta.
 */
function getChegadas(
  posicoes: Map<string, Ponto>,
  centroY: number,
  porta: Ponto,
  largura: number,
  altura: number,
): Chegada[] {
  const soPelaPorta = posicoes.size <= MaxSoPelaPorta;
  const fora = { x: porta.x, y: porta.y + PassoPorta };
  const chegadas = [...posicoes.entries()].map(([id, p]) => {
    const pelaPorta = soPelaPorta || p.y < centroY;
    const de = pelaPorta
      ? fora
      : {
          x: Math.min(Math.max(p.x, MargemDeBaixo), largura - MargemDeBaixo),
          y: altura + 60,
        };
    return { id, p, de, pelaPorta };
  });
  const distancia = (c: Chegada) => Math.hypot(c.p.x - c.de.x, c.p.y - c.de.y);
  return chegadas.sort((a, b) => distancia(a) - distancia(b));
}

/**
 * Na primeira visita do dia, cada avatar chega ao seu lugar: pela porta do fundo ou subindo pela
 * borda de baixo, correndo. A entrada não conta no limite de encenações: elas começam depois dela.
 */
export function useEntrada({
  posicoes,
  centroY,
  porta,
  largura,
  altura,
  reduzido,
}: {
  posicoes: Map<string, Ponto>;
  /** O centro da elipse: quem fica acima dele está na metade de trás. */
  centroY: number;
  /** Os pés de quem está no vão da porta. */
  porta: Ponto;
  largura: number;
  altura: number;
  reduzido: boolean;
}) {
  const [ativa, setAtiva] = useState(
    () =>
      !reduzido &&
      document.visibilityState === "visible" &&
      posicoes.size > 0 &&
      !jaEntrouHoje(),
  );
  // Todos começam fora da praça, sem transição: atrás da porta (escondidos) ou abaixo da borda.
  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(() =>
    ativa
      ? new Map(
          getChegadas(posicoes, centroY, porta, largura, altura).map(
            ({ id, p, de, pelaPorta }) => {
              const inicio = pelaPorta ? porta : de;
              return [
                id,
                {
                  animacao: "correr",
                  dx: inicio.x - p.x,
                  dy: inicio.y - p.y,
                  direcao: "sul",
                  passoMs: 0,
                  oculto: pelaPorta,
                },
              ];
            },
          ),
        )
      : new Map(),
  );
  const [portaAberta, setPortaAberta] = useState(false);
  const temporizadores = useRef<number[]>([]);

  const encerrar = useCallback(() => {
    temporizadores.current.forEach((t) => window.clearTimeout(t));
    temporizadores.current = [];
    setAtuacoes(new Map());
    setPortaAberta(false);
    setAtiva(false);
  }, []);

  useEffect(() => {
    if (!ativa) return;
    registrarEntrada();
    const depois = (ms: number, executar: () => void) =>
      temporizadores.current.push(window.setTimeout(executar, ms));
    const atuar = (id: string, atuacao: Atuacao | null) =>
      setAtuacoes((atual) => {
        const proxima = new Map(atual);
        if (atuacao) proxima.set(id, atuacao);
        else proxima.delete(id);
        return proxima;
      });

    const chegadas = getChegadas(posicoes, centroY, porta, largura, altura);
    const pelaPorta = chegadas.filter((c) => c.pelaPorta);
    const deBaixo = chegadas.filter((c) => !c.pelaPorta);

    let fim = 0;
    const correr = (c: Chegada, largada: number) => {
      const corrida = Math.round(
        Math.hypot(c.p.x - c.de.x, c.p.y - c.de.y) / Velocidade,
      );
      depois(largada, () =>
        atuar(c.id, {
          animacao: "correr",
          dx: 0,
          dy: 0,
          direcao: getDirecao(c.de, c.p),
          passoMs: corrida,
        }),
      );
      depois(largada + corrida, () => atuar(c.id, null));
      fim = Math.max(fim, largada + corrida);
    };

    // A porta abre, e cada um aparece no vão, dá um passo para fora e corre até o lugar.
    const abertura = 200;
    if (pelaPorta.length > 0) depois(60, () => setPortaAberta(true));
    pelaPorta.forEach((c, i) => {
      const saida = abertura + i * IntervaloPorta;
      depois(saida, () =>
        atuar(c.id, {
          animacao: "andar",
          dx: c.de.x - c.p.x,
          dy: c.de.y - c.p.y,
          direcao: "sul",
          passoMs: PassoPortaMs,
        }),
      );
      correr(c, saida + PassoPortaMs);
    });
    if (pelaPorta.length > 0) {
      const ultimo = abertura + (pelaPorta.length - 1) * IntervaloPorta;
      depois(ultimo + PassoPortaMs + 300, () => setPortaAberta(false));
    }
    deBaixo.forEach((c, i) => correr(c, 60 + i * Intervalo));
    depois(fim + 50, encerrar);

    const ativos = temporizadores;
    return () => {
      ativos.current.forEach((t) => window.clearTimeout(t));
      ativos.current = [];
    };
    // Roda uma vez, com as posições do primeiro desenho da cena.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ativa]);

  return { ativa, atuacoes, portaAberta, pular: encerrar };
}
