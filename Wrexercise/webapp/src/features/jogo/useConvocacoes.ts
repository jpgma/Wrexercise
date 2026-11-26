import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { TipoAtividade } from "@/features/atividades";
import { shouldRetry } from "@/features/grupos";
import { getJson, postJson, putJson } from "@/services/api";

/** A Convocação, como um todo (decisão 0091). */
export type SituacaoConvocacao =
  "Ativa" | "AguardandoVerificacao" | "Encerrada";

/** A participação de uma pessoa na Convocação. */
export type SituacaoParticipante =
  | "Pendente"
  | "Participando"
  | "AguardandoVerificacao"
  | "Concluida"
  | "Recusada"
  | "Desistiu"
  | "Falhou"
  | "Expirada"
  | "ForaDoGrupo";

export type ParticipanteConvocacao = {
  usuarioId: string;
  nomeExibicao: string;
  situacao: SituacaoParticipante;
  anfitriao: boolean;
};

export type Convocacao = {
  id: string;
  grupoId: string;
  codigo: "TodoMundo";
  nome: string;
  tipoAtividade: TipoAtividade;
  anfitriaoId: string;
  anfitriaoNome: string;
  criadaEm: string;
  expiraEm: string;
  situacao: SituacaoConvocacao;
  concluidas: number;
  /** A participação de quem vê, ou `null` se não foi convidado. */
  minhaSituacao: SituacaoParticipante | null;
  minhaRecompensa: number;
  /** Só para o anfitrião: a Energia que já voltou da carta. */
  devolucao: number | null;
  /** Só para o anfitrião: a parte do preço que cada convidado devolve. */
  devolucaoPorPessoa: number | null;
  podeSair: boolean;
  participantes: ParticipanteConvocacao[];
};

export type Convocacoes = {
  /** A ativa, ou a última encerrada há até 3 dias: o painel da praça. */
  destaque: Convocacao | null;
  recentes: Convocacao[];
};

export type PreviaConvocacao = {
  tipos: TipoAtividade[];
  convidados: number;
  recompensa: number;
  devolucaoPorPessoa: number;
  duracaoDias: number;
  /** O código do que impede convocar agora, ou `null`. */
  bloqueio: string | null;
  mensagem: string | null;
};

export type DecisaoConvocacao = "Aceita" | "Recusada" | "Desistiu";

const convocacoesKey = (grupoId: string) =>
  ["jogo", "convocacoes", grupoId] as const;

export function useConvocacoes(grupoId: string) {
  return useQuery({
    queryKey: convocacoesKey(grupoId),
    queryFn: ({ signal }) =>
      getJson<Convocacoes>(`/api/grupos/${grupoId}/convocacoes`, signal),
    retry: shouldRetry,
  });
}

export function useConvocacao(grupoId: string, convocacaoId: string) {
  return useQuery({
    queryKey: [...convocacoesKey(grupoId), convocacaoId],
    queryFn: ({ signal }) =>
      getJson<Convocacao>(
        `/api/grupos/${grupoId}/convocacoes/${convocacaoId}`,
        signal,
      ),
    retry: shouldRetry,
  });
}

export function usePreviaConvocacao(grupoId: string, cartaId: string | null) {
  return useQuery({
    // Fora da chave das Convocações: convocar gasta a carta, e a prévia não deve buscar de novo.
    queryKey: ["jogo", "previa-convocacao", grupoId, cartaId],
    enabled: cartaId !== null,
    queryFn: ({ signal }) =>
      getJson<PreviaConvocacao>(
        `/api/grupos/${grupoId}/convocacoes/previa?cartaId=${cartaId}`,
        signal,
      ),
    retry: shouldRetry,
  });
}

/** Depois de convocar ou responder: a Mão, a Energia e as Convocações mudam juntas. */
function useInvalidarJogo() {
  const queryClient = useQueryClient();
  return (grupoId: string) =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: convocacoesKey(grupoId) }),
      queryClient.invalidateQueries({ queryKey: ["jogo", "loja"] }),
      queryClient.invalidateQueries({ queryKey: ["jogo", "energia"] }),
      queryClient.invalidateQueries({ queryKey: ["jogo", "cena", grupoId] }),
      queryClient.invalidateQueries({ queryKey: ["notificacoes"] }),
    ]);
}

export function useConvocar(grupoId: string) {
  const invalidar = useInvalidarJogo();
  return useMutation({
    mutationFn: (dados: { cartaId: string; tipo: TipoAtividade }) =>
      postJson<Convocacao>(`/api/grupos/${grupoId}/convocacoes`, dados),
    onSuccess: () => invalidar(grupoId),
  });
}

export function useResponderConvocacao(grupoId: string, convocacaoId: string) {
  const invalidar = useInvalidarJogo();
  return useMutation({
    mutationFn: (decisao: DecisaoConvocacao) =>
      putJson<Convocacao>(
        `/api/grupos/${grupoId}/convocacoes/${convocacaoId}/participacao`,
        { decisao },
      ),
    onSuccess: () => invalidar(grupoId),
  });
}
