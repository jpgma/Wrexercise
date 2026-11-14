import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { gruposKey, shouldRetry } from "@/features/grupos";
import {
  ApiError,
  deleteEmpty,
  getJson,
  postFormDataComProgresso,
  postJson,
} from "@/services/api";
import type { EvidenciaPreparada } from "./comprimirImagem";
import type {
  Atividade,
  AtividadeResumo,
  CriarAtividadeData,
  Evidencia,
  FiltroAtividades,
} from "./types";

const atividadesKey = ["atividades"] as const;

function atividadeKey(id: string) {
  return [...atividadesKey, id] as const;
}

function atividadesDoGrupoKey(grupoId: string) {
  return [...gruposKey, grupoId, "atividades"] as const;
}

export function useAtividadesFeed(
  grupoId: string,
  limite = 20,
  filtro?: FiltroAtividades,
  enabled = true,
  autorId?: string,
) {
  return useInfiniteQuery({
    queryKey: [
      ...atividadesDoGrupoKey(grupoId),
      limite,
      filtro ?? "Todas",
      autorId ?? "Todos",
    ],
    queryFn: async ({ pageParam, signal }) => {
      const params = new URLSearchParams({ limite: String(limite) });
      if (filtro) params.set("filtro", filtro);
      if (autorId) params.set("autorId", autorId);
      if (pageParam) params.set("cursor", pageParam);
      const { atividades, proximoCursor } = await getJson<{
        atividades: AtividadeResumo[];
        proximoCursor: string | null;
      }>(`/api/grupos/${grupoId}/atividades?${params}`, signal);
      return { atividades, proximoCursor };
    },
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.proximoCursor,
    enabled,
    retry: shouldRetry,
  });
}

/** O que ainda espera a reação de quem está usando o aplicativo. */
export function useFilaAvaliacao(grupoId: string) {
  return useQuery({
    queryKey: [...atividadesDoGrupoKey(grupoId), "fila"],
    queryFn: async ({ signal }) => {
      const { atividades } = await getJson<{ atividades: AtividadeResumo[] }>(
        `/api/grupos/${grupoId}/atividades/para-avaliar`,
        signal,
      );
      return atividades;
    },
    retry: shouldRetry,
  });
}

export function useAtividadesRecentes(enabled: boolean) {
  return useInfiniteQuery({
    queryKey: [...atividadesKey, "recentes"],
    queryFn: async ({ pageParam, signal }) => {
      const params = new URLSearchParams({ limite: "10" });
      if (pageParam) params.set("cursor", pageParam);
      const { atividades, proximoCursor } = await getJson<{
        atividades: AtividadeResumo[];
        proximoCursor: string | null;
      }>(`/api/atividades/recentes?${params}`, signal);
      return { atividades, proximoCursor };
    },
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.proximoCursor,
    enabled,
    retry: shouldRetry,
  });
}

export function useAtividade(id: string, enabled = true) {
  return useQuery({
    queryKey: atividadeKey(id),
    queryFn: ({ signal }) =>
      getJson<Atividade>(`/api/atividades/${id}`, signal),
    enabled,
    retry: shouldRetry,
  });
}

/** O autor apaga a própria atividade nos primeiros 15 minutos (ADR 0054). */
export function useExcluirAtividade(id: string, grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => deleteEmpty(`/api/atividades/${id}`),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: atividadeKey(id) });
      void queryClient.invalidateQueries({
        queryKey: atividadesDoGrupoKey(grupoId),
      });
      void queryClient.invalidateQueries({
        queryKey: [...atividadesKey, "recentes"],
      });
      void queryClient.invalidateQueries({ queryKey: ["desafios"] });
      void queryClient.invalidateQueries({ queryKey: gruposKey });
      void queryClient.invalidateQueries({ queryKey: ["jogo"] });
    },
  });
}

export function useCriarAtividade(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    // A mesma chave numa nova tentativa devolve a atividade já criada (ADR 0053).
    mutationFn: ({ chave, ...data }: CriarAtividadeData & { chave: string }) =>
      postJson<Atividade>(`/api/grupos/${grupoId}/atividades`, data, {
        "Idempotency-Key": chave,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: atividadesDoGrupoKey(grupoId),
      });
      void queryClient.invalidateQueries({
        queryKey: [...atividadesKey, "recentes"],
      });
      void queryClient.invalidateQueries({ queryKey: ["desafios"] });
      // Poses da cena do Jogo (chave de features/jogo; uma feature não importa o miolo da outra).
      void queryClient.invalidateQueries({ queryKey: ["jogo"] });
    },
    onError: (error) => {
      // Chegou atividade para avaliar depois que a tela abriu: a fila atualizada troca o formulário pelo aviso.
      if (error instanceof ApiError && error.code === "AVALIACOES_PENDENTES") {
        void queryClient.invalidateQueries({
          queryKey: [...atividadesDoGrupoKey(grupoId), "fila"],
        });
      }
    },
  });
}

/** Fora de um hook: a atividade só existe depois de criada, no meio do envio do formulário. */
export function anexarEvidencia(
  atividadeId: string,
  { arquivo, miniatura, sinais }: EvidenciaPreparada,
  onProgresso: (fracao: number) => void = () => undefined,
) {
  const form = new FormData();
  form.append("arquivo", arquivo);
  if (miniatura) form.append("miniatura", miniatura, "miniatura.jpg");
  // Sinais da foto original (ADR 0052). A localização é guardada, mas a API não a devolve.
  if (sinais.capturadaEm && sinais.origem) {
    form.append("capturadaEm", sinais.capturadaEm.toISOString());
    form.append("origemDataCaptura", sinais.origem);
  }
  if (sinais.sha256Original)
    form.append("sha256Original", sinais.sha256Original);
  form.append("dhash", sinais.dhash);
  // Só no vídeo: os quadros do início e do fim, para o aviso VideoParado (ADR 0058).
  if (sinais.dhashInicio) form.append("dhashInicio", sinais.dhashInicio);
  if (sinais.dhashFim) form.append("dhashFim", sinais.dhashFim);
  if (sinais.latitude !== null && sinais.longitude !== null) {
    form.append("latitude", String(sinais.latitude));
    form.append("longitude", String(sinais.longitude));
  }
  return postFormDataComProgresso<Evidencia>(
    `/api/atividades/${atividadeId}/evidencias`,
    form,
    onProgresso,
  );
}
