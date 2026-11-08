import { useQuery } from "@tanstack/react-query";
import { useSessao } from "@/features/autenticacao";
import { getJson } from "@/services/api";

const nomesFolhasPadrao = [
  "bia",
  "caio",
  "davi",
  "jonas",
  "lara",
  "nina",
] as const;

/** FNV-1a de 32 bits: estável entre execuções e sem dependências. */
export function hashFnv1a(texto: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < texto.length; i++) {
    hash ^= texto.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash;
}

/** Folha de avatar padrão de uma pessoa, escolhida pelo identificador (ver docs/avatar-kit.md). */
export function folhaPadrao(usuarioId: string): string {
  const indice = hashFnv1a(usuarioId) % nomesFolhasPadrao.length;
  return `/avatar-${nomesFolhasPadrao[indice]}.png`;
}

export const avataresKey = ["jogo", "avatares"] as const;

type AvataresResponse = {
  avatares: { usuarioId: string; folha: string }[];
};

function paraMapa(resposta: AvataresResponse): Map<string, string> {
  return new Map(resposta.avatares.map((a) => [a.usuarioId, a.folha]));
}

/**
 * A folha de uma pessoa: a que ela importou, se quem vê divide um grupo com ela, ou a padrão.
 * Um mapa só para o aplicativo todo, buscado uma vez e compartilhado por todos os avatares. Sem sessão
 * (a prévia pública do convite), não busca nada.
 */
export function useFolhaAvatar(usuarioId: string): string {
  const { data: usuario } = useSessao();
  const { data } = useQuery({
    queryKey: avataresKey,
    queryFn: ({ signal }) =>
      getJson<AvataresResponse>("/api/jogo/avatares", signal),
    select: paraMapa,
    enabled: Boolean(usuario),
    staleTime: 5 * 60_000,
  });
  return data?.get(usuarioId) ?? folhaPadrao(usuarioId);
}
