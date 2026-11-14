import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postJson } from "@/services/api";
import type { CodigoCutucada } from "./textosCutucada";

/** Cutuca um colega que dorme no grupo (decisão 0063). */
export function useCutucar(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dados: {
      destinatarioId: string;
      tipo: CodigoCutucada;
      texto?: string;
    }) =>
      postJson<{
        id: string;
        tipo: string;
        enviadaEm: string;
        texto: string | null;
      }>(`/api/grupos/${grupoId}/cutucadas`, dados),
    onSettled: () =>
      queryClient.invalidateQueries({ queryKey: ["jogo", "cena", grupoId] }),
  });
}
