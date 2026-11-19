import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { shouldRetry } from "@/features/grupos";
import { getJson, postJson, postJsonEmpty, putJsonEmpty } from "@/services/api";

export type CodigoProvocacao =
  "Mais10Min" | "Mais20Min" | "Variedade" | "FimDeSemana" | "AntesDas8";

export type CodigoResposta =
  "DesafioDuplo" | "Aposta" | "PrazoExtra" | "MudancaDePlanos";

export type CodigoCarta = CodigoProvocacao | CodigoResposta;

export type Carta = {
  codigo: CodigoCarta;
  nome: string;
  tipo: "Provocacao" | "Resposta";
  preco: number;
  efeito: string;
};

export type CartaMao = Carta & { id: string };

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
  /** O objetivo em vigor: o da carta ou o trocado pela MUDANÇA DE PLANOS. */
  objetivo: CodigoProvocacao;
  /** A Resposta usada no aceite, se houver. */
  cartaResposta: Carta | null;
  /** A Jogada foi devolvida por um DESAFIO DUPLO. */
  desafioDuplo: boolean;
  /** Os objetivos que a MUDANÇA DE PLANOS pode escolher; só na Jogada recebida que espera resposta. */
  objetivosEquivalentes: CodigoProvocacao[];
  situacao: SituacaoJogada;
  enviadaEm: string;
  expiraEm: string;
  duracaoAlvoSegundos: number | null;
  tipoEvitar: string | null;
  /** Quem consulta é o destinatário. */
  recebida: boolean;
  /** Quem consulta ainda não viu a carta chegar (destinatário) ou a resposta (remetente). */
  nova: boolean;
  /** A segunda Provocação de uma Fusão, ou `null` (decisão 0070). */
  cartaFusao: Carta | null;
  /** A Energia de quem cumpre, já com o bônus da Fusão e a APOSTA. */
  recompensa: number;
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

/** O que o arremesso fixaria agora: objetivo, prazo e recompensas (`PreverJogada.cs`). */
export type PreviaJogada = {
  duracaoAlvoSegundos: number | null;
  tipoEvitar: string | null;
  expiraEm: string;
  recompensaDestinatario: number;
  recompensaRemetente: number;
};

/**
 * A prévia de uma carta para um colega, pedida só com os dois escolhidos. A chave fica sob a das
 * Jogadas do grupo: o que renova as Jogadas renova a prévia.
 */
export function usePreviaJogada(
  grupoId: string,
  destinatarioId: string | undefined,
  cartaId: string | undefined,
  cartaFusaoId: string | undefined,
) {
  return useQuery({
    queryKey: [
      ...jogadasKey(grupoId),
      "previa",
      destinatarioId,
      cartaId,
      cartaFusaoId ?? null,
    ],
    queryFn: ({ signal }) =>
      getJson<PreviaJogada>(
        `/api/grupos/${grupoId}/jogadas/previa?destinatarioId=${destinatarioId}&cartaId=${cartaId}${cartaFusaoId ? `&cartaFusaoId=${cartaFusaoId}` : ""}`,
        signal,
      ),
    enabled: destinatarioId !== undefined && cartaId !== undefined,
    retry: shouldRetry,
  });
}

export function useJogarCarta(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dados: {
      cartaId: string;
      destinatarioId: string;
      /** A segunda Provocação, para jogar uma Fusão. */
      cartaFusaoId?: string;
    }) =>
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

export type RespostaJogada = {
  id: string;
  resposta: "Aceita" | "Recusada";
  /** A Resposta da Mão usada para aceitar. */
  cartaRespostaId?: string;
  /** O objetivo novo, só com a MUDANÇA DE PLANOS. */
  objetivo?: CodigoProvocacao;
};

export function useResponderJogada(grupoId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...corpo }: RespostaJogada) =>
      putJsonEmpty(`/api/jogadas/${id}/resposta`, corpo),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: jogadasKey(grupoId) }),
        queryClient.invalidateQueries({ queryKey: notificacoesKey }),
        queryClient.invalidateQueries({ queryKey: lojaKey }),
        // Recusar pode dar FADIGA.
        queryClient.invalidateQueries({ queryKey: energiaKey }),
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
