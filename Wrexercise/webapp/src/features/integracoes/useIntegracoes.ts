import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import { deleteEmpty, getJson, postJson } from "@/services/api";
import type { TipoAtividade } from "@/features/atividades";

/** Os apps que se conectam ao Wrexercise (ADR 0065). */
export type AppIntegracao = "Movr";

export const appIntegracaoLabels: Record<AppIntegracao, string> = {
  Movr: "Movr",
};

export type SituacaoRascunho = "Pendente" | "Postada" | "Expirada";

/** O treino que um app conectado preparou para o formulário de registro. */
export type Rascunho = {
  id: string;
  app: AppIntegracao;
  tipo: TipoAtividade;
  duracaoSegundos: number;
  startedAt: string;
  endedAt: string;
  situacao: SituacaoRascunho;
  atividadeId: string | null;
  grupoId: string | null;
  imagemUrl: string;
};

export type AppConectado = {
  id: string;
  app: AppIntegracao;
  createdAt: string;
  lastUsedAt: string | null;
};

const appsConectadosKey = ["apps-conectados"] as const;

export function rascunhoKey(rascunhoId: string) {
  return ["rascunhos", rascunhoId] as const;
}

export function useRascunho(rascunhoId: string | null) {
  return useQuery({
    queryKey: rascunhoKey(rascunhoId ?? ""),
    queryFn: ({ signal }) =>
      getJson<Rascunho>(`/api/rascunhos/${rascunhoId}`, signal),
    enabled: rascunhoId !== null,
    retry: shouldRetry,
    // O app pode trocar a imagem enquanto o rascunho está pendente: sem cache entre visitas.
    staleTime: 0,
  });
}

export function useAppsConectados() {
  return useQuery({
    queryKey: appsConectadosKey,
    queryFn: async ({ signal }) => {
      const { apps } = await getJson<{ apps: AppConectado[] }>(
        "/api/apps-conectados",
        signal,
      );
      return apps;
    },
    retry: shouldRetry,
  });
}

export function useDesconectarApp() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteEmpty(`/api/apps-conectados/${id}`),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: appsConectadosKey }),
  });
}

export type AutorizarAppData = {
  app: AppIntegracao;
  codeChallenge: string;
  redirectUri: string;
};

export function useAutorizarApp() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AutorizarAppData) =>
      postJson<{ codigo: string; redirectUri: string }>(
        "/api/integracoes/autorizacoes",
        data,
      ),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: appsConectadosKey }),
  });
}

/**
 * O endereço de retorno com o código e o `state` do app. O servidor já conferiu que o endereço está na
 * lista fechada; aqui só se monta a URL.
 */
export function montarRetorno(
  redirectUri: string,
  codigo: string,
  state: string,
): string {
  const separador = redirectUri.includes("?") ? "&" : "?";
  return `${redirectUri}${separador}code=${encodeURIComponent(codigo)}&state=${encodeURIComponent(state)}`;
}
