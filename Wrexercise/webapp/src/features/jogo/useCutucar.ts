import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postJson } from "@/services/api";
import type { CodigoCutucada } from "./textosCutucada";

/** Cutuca um colega que dorme no grupo (decisão 0063). */
export function useCutucar(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dados: { destinatarioId: string; tipo: CodigoCutucada }) =>
      postJson<{ id: string; tipo: string; enviadaEm: string }>(
        `/api/grupos/${grupoId}/cutucadas`,
        dados,
      ),
    onSettled: () =>
      queryClient.invalidateQueries({ queryKey: ["jogo", "cena", grupoId] }),
  });
}
