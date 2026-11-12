import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { useSessao } from "@/features/autenticacao";
import { getJson } from "@/services/api";

/** Formato da folha de avatar em 8 direções (docs/avatar-formato.md §6, ADR 0039). */
export const formatoFolha = {
  /** Lado da célula, em px. */
  celula: 48,
  colunas: 9,
  linhas: 97,
  /** Ponto entre os pés, igual em todos os quadros. */
  ancora: { x: 24, y: 44 },
  /** Área do corpo dentro da célula: é a área de toque na cena. */
  corpo: { esquerda: 12, topo: 8, largura: 24 },
  /** Recorte quadrado da cabeça e dos ombros na `rotacao` `sul` (linha 0, coluna 0). */
  retrato: { x: 12, y: 10, lado: 24 },
} as const;

/** FNV-1a de 32 bits: estável entre execuções e sem dependências. */
export function hashFnv1a(texto: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < texto.length; i++) {
    hash ^= texto.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash;
}

/**
 * A folha de avatar padrão, em 8 direções, de quem não importou uma folha: o base masculino, a mesma
 * para todos (ADR 0039, Andamento), gerada por `pixel-art/base-masculino/build.py`.
 */
export function folhaPadrao(): string {
  return "/avatar-base-masculino.png";
}

export const avataresKey = ["jogo", "avatares"] as const;

/** Um Enfeite: `conquista` é a do nível que define o desenho (a linha do atlas `/enfeites.png`). */
export type Enfeite = { codigo: string; conquista: string; nome: string };

type AvataresResponse = {
  avatares: { usuarioId: string; folha: string | null; vitrine: Enfeite[] }[];
};

function paraMapa(resposta: AvataresResponse) {
  return new Map(resposta.avatares.map((a) => [a.usuarioId, a]));
}

const semEnfeites: Enfeite[] = [];
const semVitrines = new Map<string, Enfeite[]>();

/**
 * O mapa de avatares (folha importada e vitrine de cada pessoa) de quem divide um grupo com quem vê.
 * Um mapa só para o aplicativo todo, buscado uma vez e compartilhado por todos os avatares. Sem sessão
 * (a prévia pública do convite), não busca nada.
 */
function useMapaAvatares() {
  const { data: usuario } = useSessao();
  return useQuery({
    queryKey: avataresKey,
    queryFn: ({ signal }) =>
      getJson<AvataresResponse>("/api/jogo/avatares", signal),
    select: paraMapa,
    enabled: Boolean(usuario),
    staleTime: 5 * 60_000,
  });
}

/**
 * A folha de uma pessoa: a importada, se houver, ou a padrão. Sem `usuarioId` (ou antes de o mapa
 * chegar), a padrão. As folhas importadas seguem o mesmo formato em 8 direções (ADR 0039, Andamento).
 */
export function useFolhaAvatar(usuarioId?: string): string {
  const { data } = useMapaAvatares();
  return (usuarioId && data?.get(usuarioId)?.folha) || folhaPadrao();
}

/** A vitrine de Enfeites de uma pessoa (vazia para quem está no modo App). */
export function useVitrine(usuarioId: string): Enfeite[] {
  const { data } = useMapaAvatares();
  return data?.get(usuarioId)?.vitrine ?? semEnfeites;
}

/** As vitrines de todos que dividem um grupo com quem vê, por usuário: a praça põe os Enfeites no chão. */
export function useVitrines(): Map<string, Enfeite[]> {
  const { data } = useMapaAvatares();
  return useMemo(
    () =>
      data ? new Map([...data].map(([id, a]) => [id, a.vitrine])) : semVitrines,
    [data],
  );
}
