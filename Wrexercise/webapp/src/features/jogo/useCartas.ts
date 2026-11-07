import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import { getJson, postJson, postJsonEmpty, putJsonEmpty } from "@/services/api";

export type CodigoCarta =
  "Mais10Min" | "Mais20Min" | "Variedade" | "FimDeSemana" | "AntesDas8";

export type Carta = {
  codigo: CodigoCarta;
  nome: string;
  tipo: "Provocacao" | "Resposta";
  preco: number;
  efeito: string;
};

export type CartaMao = Carta & { id: string; emJogo: boolean };

export type Loja = {
  saldo: number;
  limiteMao: number;
  catalogo: Carta[];
  mao: CartaMao[];
};

export type SituacaoJogada =
  | "Enviada"
  | "Recusada"
  | "Aceita"
  | "AguardandoVerificacao"
  | "Concluida"
  | "Expirada";

export type Jogada = {
  id: string;
  grupoId: string;
  remetenteId: string;
  remetenteNome: string;
  destinatarioId: string;
  destinatarioNome: string;
  carta: Carta;
  situacao: SituacaoJogada;
  enviadaEm: string;
  expiraEm: string;
  duracaoAlvoSegundos: number | null;
  tipoEvitar: string | null;
  /** Quem consulta é o destinatário. */
  recebida: boolean;
  /** Quem consulta ainda não viu a carta chegar (destinatário) ou a resposta (remetente). */
  nova: boolean;
};

const lojaKey = ["jogo", "loja"] as const;
const energiaKey = ["jogo", "energia"] as const;
const jogadasKey = (grupoId: string) => ["jogo", "jogadas", grupoId] as const;
const notificacoesKey = ["notificacoes"] as const;

export function useLoja() {
  return useQuery({
    queryKey: lojaKey,
    queryFn: ({ signal }) => getJson<Loja>("/api/jogo/loja", signal),
  });
}

export function useComprarCarta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (codigo: CodigoCarta) =>
      postJson<CartaMao>("/api/jogo/cartas", { codigo }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: lojaKey }),
        queryClient.invalidateQueries({ queryKey: energiaKey }),
      ]);
    },
  });
}

export function useJogadas(grupoId: string) {
  return useQuery({
    queryKey: jogadasKey(grupoId),
    queryFn: async ({ signal }) => {
      const { jogadas } = await getJson<{ jogadas: Jogada[] }>(
        `/api/grupos/${grupoId}/jogadas`,
        signal,
      );
      return jogadas;
    },
    retry: shouldRetry,
  });
}

export function useJogarCarta(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dados: { cartaId: string; destinatarioId: string }) =>
      postJson<{ id: string; expiraEm: string }>(
        `/api/grupos/${grupoId}/jogadas`,
        dados,
      ),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: lojaKey }),
        queryClient.invalidateQueries({ queryKey: jogadasKey(grupoId) }),
      ]);
    },
  });
}

export function useResponderJogada(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      resposta,
    }: {
      id: string;
      resposta: "Aceita" | "Recusada";
    }) => putJsonEmpty(`/api/jogadas/${id}/resposta`, { resposta }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: jogadasKey(grupoId) }),
        queryClient.invalidateQueries({ queryKey: notificacoesKey }),
      ]);
    },
  });
}

/** Registra que a chegada da carta ou a resposta já apareceu para quem consulta. */
export function useMarcarJogadasVistas(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) =>
      postJsonEmpty(`/api/grupos/${grupoId}/jogadas/vistas`, { ids }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: jogadasKey(grupoId) }),
  });
}
