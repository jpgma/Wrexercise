import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { formatarCronometro } from "./labels";
import type { TreinoAtual } from "./types";

/** Os segundos desde o início, atualizados a cada `intervaloMs`. */
export function useSegundosDesde(inicio: string, intervaloMs = 1000): number {
  const [agora, setAgora] = useState(() => Date.now());
  useEffect(() => {
    const intervalo = window.setInterval(
      () => setAgora(Date.now()),
      intervaloMs,
    );
    return () => window.clearInterval(intervalo);
  }, [intervaloMs]);
  return Math.max(0, Math.floor((agora - new Date(inicio).getTime()) / 1000));
}

export const getRotaTreino = (treino: Pick<TreinoAtual, "grupoId">) =>
  `/grupos/${treino.grupoId}/treino`;

const isTelaDoTreino = (caminho: string) =>
  /^\/grupos\/[^/]+\/treino$/.test(caminho);

/**
 * O portão do treino ao vivo (ADR 0078): ao abrir o aplicativo com um treino aberto, a pessoa cai no
 * cronômetro, em qualquer modo. Depois disso, ela navega à vontade, e um selo "AO VIVO" com o tempo
 * fica à vista em todas as telas e leva de volta ao cronômetro.
 */
export function PortaoTreino({
  treino,
}: {
  /** `undefined` enquanto carrega: o portão ainda não decide. */
  treino: TreinoAtual | null | undefined;
}) {
  const location = useLocation();
  const navigate = useNavigate();
  const conferido = useRef(false);
  const naTela = isTelaDoTreino(location.pathname);
  // A raiz ainda vai levar ao grupo: decidir antes faria o redirecionamento dela vencer o do cronômetro.
  const naRaiz = location.pathname === "/";

  useEffect(() => {
    if (conferido.current || treino === undefined || naRaiz) return;
    conferido.current = true;
    if (treino && !naTela) {
      void navigate(getRotaTreino(treino), { replace: true });
    }
  }, [treino, naTela, naRaiz, navigate]);

  if (!treino || naTela) return null;
  return <SeloAoVivo treino={treino} />;
}

function SeloAoVivo({ treino }: { treino: TreinoAtual }) {
  const segundos = useSegundosDesde(treino.startedAt);
  return (
    <Link
      to={getRotaTreino(treino)}
      aria-label={`Treino ao vivo, ${formatarCronometro(segundos)}. Abrir o cronômetro`}
      className="fixed right-4 bottom-[calc(5.25rem+env(safe-area-inset-bottom))] z-50 flex min-h-11 items-center gap-2 rounded-full border-2 border-outline bg-surface px-3 text-sm font-bold shadow-sticker press lg:right-8 lg:bottom-8"
    >
      <span
        aria-hidden="true"
        className="size-2.5 rounded-full bg-danger motion-safe:animate-pulse"
      />
      AO VIVO
      <span className="font-score text-base tabular-nums">
        {formatarCronometro(segundos)}
      </span>
    </Link>
  );
}
