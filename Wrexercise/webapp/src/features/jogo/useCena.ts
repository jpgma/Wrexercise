import { useQuery } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import { getJson } from "@/services/api";

export type PoseCena = "parado" | "treinando" | "comemorando";

export type MembroCena = {
  usuarioId: string;
  nomeExibicao: string;
  pose: PoseCena;
  /** Está no modo Jogo: só assim recebe cartas. */
  recebeCartas: boolean;
};

export type Cena = {
  grupoId: string;
  membros: MembroCena[];
};

export function useCena(grupoId: string) {
  return useQuery({
    queryKey: ["jogo", "cena", grupoId],
    queryFn: ({ signal }) =>
      getJson<Cena>(`/api/grupos/${grupoId}/cena`, signal),
    retry: shouldRetry,
  });
}
