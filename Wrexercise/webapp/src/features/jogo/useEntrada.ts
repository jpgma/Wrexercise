import { useCallback, useEffect, useRef, useState } from "react";
import { getDirecao } from "./animacoes";
import type { Atuacao, Ponto } from "./useEncenacoes";

// O dia da última entrada fica num cookie comum, não em `localStorage`, que o ESLint proíbe.
const cookieEntrada = "wx_entrada_praca";
/** Velocidade da corrida, em px da praça por ms. */
const Velocidade = 0.3;
/** Intervalo entre a largada de um avatar e a do próximo. */
const Intervalo = 120;

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

/** Cada avatar sai da borda mais próxima; quem está mais perto dela entra primeiro. */
function getFila(posicoes: Map<string, Ponto>, largura: number) {
  return [...posicoes.entries()]
    .map(([id, p]) => {
      const daEsquerda = p.x < largura / 2;
      const dx = daEsquerda ? -(p.x + 40) : largura - p.x + 40;
      return { id, p, dx };
    })
    .sort((a, b) => Math.abs(a.dx) - Math.abs(b.dx));
}

/**
 * Na primeira visita do dia, cada avatar entra correndo pela borda mais próxima até o seu lugar.
 * A entrada não conta no limite de encenações: as encenações começam depois dela.
 */
export function useEntrada({
  posicoes,
  largura,
  reduzido,
}: {
  posicoes: Map<string, Ponto>;
  largura: number;
  reduzido: boolean;
}) {
  const [ativa, setAtiva] = useState(
    () =>
      !reduzido &&
      document.visibilityState === "visible" &&
      posicoes.size > 0 &&
      !jaEntrouHoje(),
  );
  // Todos começam fora da praça, sem transição.
  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(() =>
    ativa
      ? new Map(
          getFila(posicoes, largura).map(({ id, p, dx }) => [
            id,
            {
              animacao: "correr",
              dx,
              dy: 0,
              direcao: getDirecao({ x: p.x + dx, y: p.y }, p),
              passoMs: 0,
            },
          ]),
        )
      : new Map(),
  );
  const temporizadores = useRef<number[]>([]);

  const encerrar = useCallback(() => {
    temporizadores.current.forEach((t) => window.clearTimeout(t));
    temporizadores.current = [];
    setAtuacoes(new Map());
    setAtiva(false);
  }, []);

  useEffect(() => {
    if (!ativa) return;
    registrarEntrada();
    const depois = (ms: number, executar: () => void) =>
      temporizadores.current.push(window.setTimeout(executar, ms));

    const fila = getFila(posicoes, largura);

    let fim = 0;
    fila.forEach(({ id, p, dx }, i) => {
      const largada = 60 + i * Intervalo;
      const corrida = Math.round(Math.abs(dx) / Velocidade);
      depois(largada, () =>
        setAtuacoes((atual) =>
          new Map(atual).set(id, {
            animacao: "correr",
            dx: 0,
            dy: 0,
            direcao: getDirecao({ x: p.x + dx, y: p.y }, p),
            passoMs: corrida,
          }),
        ),
      );
      depois(largada + corrida, () =>
        setAtuacoes((atual) => {
          const proxima = new Map(atual);
          proxima.delete(id);
          return proxima;
        }),
      );
      fim = Math.max(fim, largada + corrida);
    });
    depois(fim + 50, encerrar);

    const ativos = temporizadores;
    return () => {
      ativos.current.forEach((t) => window.clearTimeout(t));
      ativos.current = [];
    };
    // Roda uma vez, com as posições do primeiro desenho da cena.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ativa]);

  return { ativa, atuacoes, pular: encerrar };
}
