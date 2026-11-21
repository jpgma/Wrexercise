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
  /** O treino ao vivo deste membro (ADR 0078); `null` quando não treina agora. */
  treinoAoVivo: TreinoAoVivo | null;
};

export type TreinoAoVivo = {
  atividadeId: string;
  tipo: string;
  startedAt: string;
  /** Quantas Torcidas o treino recebeu. */
  torcidas: number;
  /** Quando quem vê torceu por último: o botão "Torcer" espera o intervalo. */
  torcidaPorMimEm: string | null;
  /** A distância até agora, quando o treino grava o Percurso (ADR 0084); `null` sem percurso. */
  distanciaMetros: number | null;
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
    // Sem WebSocket: a praça se atualiza a cada 30 segundos enquanto está aberta, para mostrar quem
    // começou ou terminou um treino ao vivo (ADR 0078).
    refetchInterval: 30_000,
    refetchIntervalInBackground: false,
    retry: shouldRetry,
  });
}
