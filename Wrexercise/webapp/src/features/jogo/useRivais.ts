import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import { getJson, postJson, postJsonEmpty } from "@/services/api";

/** Sempre do ponto de vista de quem escolheu o Rival. */
export type SituacaoRival = "EmAndamento" | "Venceu" | "Perdeu" | "SemVencedor";

export type Rival = {
  id: string;
  escolhedorId: string;
  escolhedorNome: string;
  rivalId: string;
  rivalNome: string;
  semanaInicio: string;
  situacao: SituacaoRival;
  /** Quem consulta escolheu este Rival; senão, foi escolhido. */
  escolhido: boolean;
  /** Quem consulta ainda não viu a provocação (Rival) ou o resultado final (quem escolheu). */
  nova: boolean;
};

export type Rivais = {
  /** A segunda-feira da semana do desafio ativo, ou `null` sem ele. */
  semanaInicio: string | null;
  podeEscolher: boolean;
  /** Os colegas que quem consulta pode escolher agora. */
  candidatos: string[];
  /** Quantas vezes quem consulta venceu o Rival que escolheu neste grupo. */
  vitorias: number;
  rivais: Rival[];
};

const rivaisKey = (grupoId: string) => ["jogo", "rivais", grupoId] as const;

export function useRivais(grupoId: string) {
  return useQuery({
    queryKey: rivaisKey(grupoId),
    queryFn: ({ signal }) =>
      getJson<Rivais>(`/api/grupos/${grupoId}/rivais`, signal),
    retry: shouldRetry,
  });
}

export function useEscolherRival(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (rivalId: string) =>
      postJson<{ id: string; semanaInicio: string }>(
        `/api/grupos/${grupoId}/rivais`,
        { rivalId },
      ),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: rivaisKey(grupoId) }),
  });
}

/** Registra que a provocação (Rival) ou o resultado (quem escolheu) já apareceu na cena. */
export function useMarcarRivaisVistos(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) =>
      postJsonEmpty(`/api/grupos/${grupoId}/rivais/vistos`, { ids }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: rivaisKey(grupoId) }),
  });
}

/** Quem venceu, quando o resultado é final e tem vencedor. */
export function getVencedorId(rival: Rival): string | null {
  if (rival.situacao === "Venceu") return rival.escolhedorId;
  if (rival.situacao === "Perdeu") return rival.rivalId;
  return null;
}

/** Como quem consulta se liga a outro membro no Rival desta semana. */
export type RelacaoRival = "escolhido" | "escolheu";

/** `escolhido`: quem consulta escolheu o outro; `escolheu`: o outro escolheu quem consulta. */
export function getRelacaoRival(
  dados: Rivais | undefined,
  usuarioId: string,
  outroId: string,
): RelacaoRival | null {
  if (!dados?.semanaInicio) return null;
  const daSemana = dados.rivais.filter(
    (r) => r.semanaInicio === dados.semanaInicio,
  );
  if (
    daSemana.some((r) => r.escolhedorId === usuarioId && r.rivalId === outroId)
  )
    return "escolhido";
  if (
    daSemana.some((r) => r.escolhedorId === outroId && r.rivalId === usuarioId)
  )
    return "escolheu";
  return null;
}
