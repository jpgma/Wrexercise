import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useBlocker } from "react-router";
import { formatarCronometro } from "@/features/atividades";
import { formatarDistancia } from "./calculo";
import type { GravacaoPercurso } from "./useGravacaoPercurso";

const SEGURAR_MS = 2000;
const RAIO_ANEL = 26;
const VOLTA_ANEL = 2 * Math.PI * RAIO_ANEL;

/** Deslocamentos em px, um por minuto, para os números não marcarem a tela OLED. */
const DESLOCAMENTOS: [number, number][] = [
  [0, 0],
  [6, -4],
  [-5, 6],
  [4, 5],
  [-6, -5],
  [5, 0],
  [-3, -6],
  [0, 6],
];

const textoGps: Record<GravacaoPercurso["estado"], string> = {
  gravando: "GPS gravando",
  parado: "Gravando em outro aparelho",
  noApp: "Gravando no Movr",
  semSinal: "Sem sinal do GPS",
  semPermissao: "GPS sem permissão",
  indisponivel: "Sem GPS",
  cheio: "GPS parado: percurso cheio",
};

/**
 * Pede a tela cheia, onde o navegador deixa (no iPhone, não há). Precisa vir de um toque da pessoa. Na tela
 * cheia, trava a tela em pé, para o celular girando no bolso não mexer nos números.
 */
export function entrarTelaCheia() {
  const raiz = document.documentElement;
  if (document.fullscreenElement || !document.fullscreenEnabled) return;
  raiz
    .requestFullscreen({ navigationUI: "hide" })
    .then(() => screen.orientation?.lock?.("portrait"))
    .catch(() => {});
}

function sairTelaCheia() {
  if (!document.fullscreenElement) return;
  try {
    screen.orientation?.unlock?.();
  } catch {
    // Sem trava de orientação neste navegador.
  }
  document.exitFullscreen().catch(() => {});
}

/**
 * O modo ambiente do Percurso (ADR 0084), para levar o celular no bolso ou no suporte: tela preta, que no OLED
 * fica apagada, com os números em branco, como o modo ambiente de um relógio. Fica por cima de tudo (abas,
 * avisos, botão do treino) e segura todos os toques. Pede a tela cheia, e o botão Voltar não sai daqui. A tela
 * continua acesa (o GPS precisa dela). Sai segurando o botão por 2 segundos.
 */
export function ModoBolso({
  distancia,
  segundos,
  media,
  estado,
  onSair,
}: {
  distancia: number;
  segundos: number;
  media: string;
  estado: GravacaoPercurso["estado"];
  onSair: () => void;
}) {
  const [segurando, setSegurando] = useState(false);
  const temporizador = useRef<number | undefined>(undefined);

  // O Voltar do Android não sai do modo bolso nem do treino.
  const bloqueio = useBlocker(({ historyAction }) => historyAction === "POP");
  useEffect(() => {
    if (bloqueio.state === "blocked") bloqueio.reset();
  }, [bloqueio]);

  // Quem abre pede a tela cheia no próprio toque (entrarTelaCheia); ao sair, ela fecha.
  useEffect(() => {
    return () => {
      window.clearTimeout(temporizador.current);
      sairTelaCheia();
    };
  }, []);

  function comecarASegurar() {
    window.clearTimeout(temporizador.current);
    setSegurando(true);
    temporizador.current = window.setTimeout(onSair, SEGURAR_MS);
  }

  function soltar() {
    window.clearTimeout(temporizador.current);
    setSegurando(false);
  }

  const [dx, dy] = DESLOCAMENTOS[
    Math.floor(segundos / 60) % DESLOCAMENTOS.length
  ] ?? [0, 0];
  const hora = new Date().toLocaleTimeString("pt-BR", {
    hour: "2-digit",
    minute: "2-digit",
  });
  const gpsOk = estado === "gravando";

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Modo bolso"
      className="fixed inset-0 z-[100] flex touch-none flex-col items-center justify-center overflow-hidden overscroll-none bg-ambiente text-on-ambiente select-none"
      // Se o Voltar tirou a tela cheia, o próximo toque a pede de novo.
      onClick={entrarTelaCheia}
      onContextMenu={(e) => e.preventDefault()}
    >
      <div
        className="flex flex-col items-center gap-6 pb-[env(safe-area-inset-bottom)]"
        style={{ transform: `translate(${dx}px, ${dy}px)` }}
      >
        <p className="text-lg tabular-nums opacity-50">{hora}</p>
        <p className="font-score text-7xl tabular-nums opacity-85">
          {formatarDistancia(distancia)}
        </p>
        <p className="font-score text-5xl tabular-nums opacity-75">
          {formatarCronometro(segundos)}
        </p>
        <p className="text-xl tabular-nums opacity-65">{media}</p>
        <p
          role="status"
          className={`flex items-center gap-2 text-sm ${gpsOk ? "opacity-45" : "opacity-85"}`}
        >
          <span
            aria-hidden="true"
            className={`size-2 rounded-full border border-on-ambiente ${gpsOk ? "bg-on-ambiente" : "motion-safe:animate-pulse"}`}
          />
          {textoGps[estado]}
        </p>

        <button
          type="button"
          autoFocus
          className="mt-8 flex min-h-11 flex-col items-center gap-2 rounded-full px-6 text-sm opacity-60"
          onPointerDown={comecarASegurar}
          onPointerUp={soltar}
          onPointerLeave={soltar}
          onPointerCancel={soltar}
          onContextMenu={(e) => e.preventDefault()}
          // Pelo teclado (sem toque), Enter, Espaço ou Esc saem de uma vez.
          onClick={(e) => {
            if (e.detail === 0) onSair();
          }}
          onKeyDown={(e) => {
            if (e.key === "Escape") onSair();
          }}
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 60 60"
            className="size-14 -rotate-90"
          >
            <circle
              cx="30"
              cy="30"
              r={RAIO_ANEL}
              fill="none"
              strokeWidth="3"
              className="stroke-on-ambiente opacity-30"
            />
            <circle
              cx="30"
              cy="30"
              r={RAIO_ANEL}
              fill="none"
              strokeWidth="3"
              strokeLinecap="round"
              className="stroke-on-ambiente"
              strokeDasharray={VOLTA_ANEL}
              style={{
                strokeDashoffset: segurando ? 0 : VOLTA_ANEL,
                transition: segurando
                  ? `stroke-dashoffset ${SEGURAR_MS}ms linear`
                  : "none",
              }}
            />
          </svg>
          Segure para sair
        </button>
      </div>
    </div>,
    document.body,
  );
}
