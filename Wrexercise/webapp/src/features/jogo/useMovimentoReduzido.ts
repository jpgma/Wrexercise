import { useSyncExternalStore } from "react";

const consulta = "(prefers-reduced-motion: reduce)";

function subscribe(ouvinte: () => void): () => void {
  const lista = window.matchMedia(consulta);
  lista.addEventListener("change", ouvinte);
  return () => lista.removeEventListener("change", ouvinte);
}

/** A regra global de movimento reduzido deixaria os sprites num quadro qualquer: aqui eles ficam no primeiro. */
export function useMovimentoReduzido(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(consulta).matches,
    () => false,
  );
}
