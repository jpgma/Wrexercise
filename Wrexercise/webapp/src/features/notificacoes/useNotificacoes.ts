import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getJson, postEmpty } from "@/services/api";

export type Notificacao = {
  id: string;
  tipo:
    | "NovaAtividade"
    | "Reacao"
    | "Comentario"
    | "SituacaoAlterada"
    | "Disputa"
    | "CartaRecebida"
    | "CartaAceita"
    | "CartaRecusada"
    | "JogadaConcluida";
  /** Nas notificações de Jogadas, vem `jogadaId` no lugar de `atividadeId`. */
  atividadeId: string | null;
  jogadaId: string | null;
  grupoId: string;
  grupoNome: string;
  atorNome: string | null;
  situacao: string | null;
  total: number;
  lida: boolean;
  updatedAt: string;
};

const notificacoesKey = ["notificacoes"] as const;

// Sem polling: as duas consultas se atualizam quando a janela volta ao foco, o padrão do TanStack Query.
export function useNotificacoes() {
  return useQuery({
    queryKey: [...notificacoesKey, "lista"],
    queryFn: async ({ signal }) => {
      const { notificacoes } = await getJson<{ notificacoes: Notificacao[] }>(
        "/api/notificacoes",
        signal,
      );
      return notificacoes;
    },
  });
}

export function useTotalNaoLidas() {
  return useQuery({
    queryKey: [...notificacoesKey, "nao-lidas"],
    queryFn: async ({ signal }) => {
      const { total } = await getJson<{ total: number }>(
        "/api/notificacoes/nao-lidas",
        signal,
      );
      return total;
    },
  });
}

export function useMarcarLida() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => postEmpty(`/api/notificacoes/${id}/lida`),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: notificacoesKey }),
  });
}

export function useMarcarTodasLidas() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => postEmpty("/api/notificacoes/lidas"),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: notificacoesKey }),
  });
}
