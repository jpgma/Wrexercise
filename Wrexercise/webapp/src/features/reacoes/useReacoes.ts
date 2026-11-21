import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { gruposKey, shouldRetry } from "@/features/grupos";
import type { EvidenciaPreparada } from "@/features/atividades";
import {
  getJson,
  postFormDataComProgresso,
  postJson,
  putJson,
} from "@/services/api";
import type {
  Comentario,
  ReacoesDetalhe,
  RevisaoAtividadeData,
  TipoReacao,
} from "./types";

function reacoesKey(atividadeId: string) {
  return ["atividades", atividadeId, "reacoes"] as const;
}

export function useReacoes(atividadeId: string) {
  return useQuery({
    queryKey: reacoesKey(atividadeId),
    queryFn: ({ signal }) =>
      getJson<ReacoesDetalhe>(`/api/atividades/${atividadeId}/reacoes`, signal),
    retry: shouldRetry,
  });
}

export function useRegistrarReacao(atividadeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (tipo: TipoReacao) =>
      putJson<ReacoesDetalhe>(`/api/atividades/${atividadeId}/reacao`, {
        tipo,
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(reacoesKey(atividadeId), data);
      void queryClient.invalidateQueries({
        queryKey: ["atividades", atividadeId],
      });
      void queryClient.invalidateQueries({ queryKey: ["desafios"] });
      void queryClient.invalidateQueries({ queryKey: ["atividades"] });
      // As listas e a fila ficam sob o grupo; a classificação, em uma chave própria.
      void queryClient.invalidateQueries({ queryKey: gruposKey });
      void queryClient.invalidateQueries({
        queryKey: ["progresso-competicao"],
      });
    },
  });
}

function comentariosKey(atividadeId: string) {
  return ["atividades", atividadeId, "comentarios"] as const;
}

export function useComentarios(atividadeId: string) {
  return useQuery({
    queryKey: comentariosKey(atividadeId),
    queryFn: ({ signal }) =>
      getJson<{ comentarios: Comentario[] }>(
        `/api/atividades/${atividadeId}/comentarios`,
        signal,
      ),
    retry: shouldRetry,
  });
}

type NovoComentario = {
  texto: string;
  midia?: EvidenciaPreparada;
  onProgresso?: (fracao: number) => void;
};

export function useComentar(atividadeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    // Com foto ou vídeo, o comentário vai em multipart, com a miniatura feita no navegador (ADR 0083).
    mutationFn: ({ texto, midia, onProgresso }: NovoComentario) => {
      if (!midia) {
        return postJson<Comentario>(
          `/api/atividades/${atividadeId}/comentarios`,
          { texto },
        );
      }
      const form = new FormData();
      form.append("arquivo", midia.arquivo);
      if (midia.miniatura)
        form.append("miniatura", midia.miniatura, "miniatura.jpg");
      if (texto) form.append("texto", texto);
      return postFormDataComProgresso<Comentario>(
        `/api/atividades/${atividadeId}/comentarios/midia`,
        form,
        onProgresso ?? (() => undefined),
      );
    },
    onSuccess: () => {
      // Inclui os comentários desta atividade; as listas do feed mostram o total.
      void queryClient.invalidateQueries({ queryKey: ["atividades"] });
      void queryClient.invalidateQueries({ queryKey: gruposKey });
    },
  });
}

export function useRevisarAtividade(atividadeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: RevisaoAtividadeData) =>
      postJson<{ situacaoVerificacao: string }>(
        `/api/atividades/${atividadeId}/revisao`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: reacoesKey(atividadeId),
      });
      void queryClient.invalidateQueries({
        queryKey: ["atividades", atividadeId],
      });
      void queryClient.invalidateQueries({ queryKey: ["desafios"] });
      void queryClient.invalidateQueries({ queryKey: ["atividades"] });
      // As listas e a fila ficam sob o grupo; a classificação, em uma chave própria.
      void queryClient.invalidateQueries({ queryKey: gruposKey });
      void queryClient.invalidateQueries({
        queryKey: ["progresso-competicao"],
      });
    },
  });
}
