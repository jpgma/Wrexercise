import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import {
  ApiError,
  deleteEmpty,
  getJsonOpcional,
  postEmpty,
  postJson,
} from "@/services/api";
import type {
  Atividade,
  TerminarTreinoData,
  TipoAtividade,
  TreinoAtual,
} from "./types";
import type { LocalCaptura } from "./CameraEvidencia";

/** Fora de `["atividades"]`: invalidar o feed não precisa refazer o treino atual. */
export const treinoAtualKey = ["treino-ao-vivo"] as const;

/**
 * O treino ao vivo de quem usa o aplicativo (ADR 0078), ou `null`. `acompanhando`: na tela do cronômetro,
 * a consulta se repete a cada 15 segundos para trazer as Torcidas; nas outras telas, só ao voltar o foco.
 */
export function useTreinoAtual(acompanhando = false) {
  return useQuery({
    queryKey: treinoAtualKey,
    queryFn: ({ signal }) =>
      getJsonOpcional<TreinoAtual>("/api/atividades/em-andamento", signal),
    refetchInterval: acompanhando ? 15_000 : false,
    refetchIntervalInBackground: false,
    retry: shouldRetry,
  });
}

/** O que muda no aplicativo quando um treino começa, termina ou é cancelado. */
function invalidarTudo(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: treinoAtualKey });
  void queryClient.invalidateQueries({ queryKey: ["atividades"] });
  void queryClient.invalidateQueries({ queryKey: ["grupos"] });
  void queryClient.invalidateQueries({ queryKey: ["desafios"] });
  // A cena do Jogo mostra quem treina ao vivo.
  void queryClient.invalidateQueries({ queryKey: ["jogo"] });
}

export function useComecarTreino(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    // O local só vai quando a pessoa escolheu mostrá-lo ao grupo (ADR 0080).
    mutationFn: ({
      tipo,
      local,
    }: {
      tipo: TipoAtividade;
      local: LocalCaptura | null;
    }) =>
      postJson<Atividade>(`/api/grupos/${grupoId}/atividades/em-andamento`, {
        tipo,
        ...(local ?? {}),
      }),
    onSuccess: () => invalidarTudo(queryClient),
    onError: (error) => {
      // As mesmas travas do registro: a fila de avaliação e a carta sem resposta (decisão 0076).
      if (error instanceof ApiError && error.code === "AVALIACOES_PENDENTES") {
        void queryClient.invalidateQueries({ queryKey: ["grupos"] });
      }
      if (error instanceof ApiError && error.code === "CARTA_SEM_RESPOSTA") {
        void queryClient.invalidateQueries({ queryKey: ["jogo", "pendentes"] });
      }
      if (error instanceof ApiError && error.code === "TREINO_EM_ANDAMENTO") {
        void queryClient.invalidateQueries({ queryKey: treinoAtualKey });
      }
    },
  });
}

export function useTerminarTreino(atividadeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: TerminarTreinoData) =>
      postJson<Atividade>(`/api/atividades/${atividadeId}/terminar`, data),
    onSuccess: (atividade) => {
      queryClient.setQueryData(["atividades", atividadeId], atividade);
      queryClient.setQueryData(treinoAtualKey, null);
      invalidarTudo(queryClient);
    },
  });
}

/** "Cancelar treino": o treino ao vivo sai, com as evidências, sem penalidade (ADR 0078). */
export function useCancelarTreino(atividadeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => deleteEmpty(`/api/atividades/${atividadeId}`),
    onSuccess: () => {
      queryClient.setQueryData(treinoAtualKey, null);
      queryClient.removeQueries({ queryKey: ["atividades", atividadeId] });
      invalidarTudo(queryClient);
    },
  });
}

/** "Torcer" pelo colega que treina agora; uma vez a cada 10 minutos (ADR 0078). */
export function useTorcer(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (atividadeId: string) =>
      postEmpty(`/api/atividades/${atividadeId}/torcida`),
    onSettled: () =>
      void queryClient.invalidateQueries({
        queryKey: ["jogo", "cena", grupoId],
      }),
  });
}

/** O intervalo entre duas Torcidas da mesma pessoa no mesmo treino. */
export const intervaloTorcidaMs = 10 * 60 * 1000;
