import { useQuery } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import { getJson } from "@/services/api";

export type PoseCena = "parado" | "treinando" | "comemorando" | "dormindo";

export type MembroCena = {
  usuarioId: string;
  nomeExibicao: string;
  pose: PoseCena;
  /** Está no modo Jogo: só assim recebe cartas. */
  recebeCartas: boolean;
  /** Só com a pose `dormindo`: desde quando, em ISO 8601. */
  dormindoDesde: string | null;
  /** Só com a pose `dormindo`: as cutucadas recebidas nas últimas 24 horas. */
  cutucadas: { total: number; ultimoTipo: string } | null;
  /** Só com a pose `dormindo`: quando quem vê cutucou este membro nas últimas 24 horas. */
  cutucadoPorMimEm: string | null;
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
