/**
 * As mensagens prontas da Cutucada (decisão 0063). Espelha `Features/Jogo/Contracts/TiposCutucada.cs`:
 * mude os dois juntos.
 */
export const tiposCutucada = [
  { codigo: "Sofa", emoji: "🛋️", texto: "Levanta desse sofá!" },
  { codigo: "Frango", emoji: "🐔", texto: "Tá com medo do treino?" },
  { codigo: "Meta", emoji: "📉", texto: "Tua meta tá chorando." },
  { codigo: "Halter", emoji: "🏋️", texto: "O halter sente sua falta." },
  { codigo: "Despertador", emoji: "⏰", texto: "Acorda, dorminhoco!" },
  { codigo: "Fantasma", emoji: "👻", texto: "Sumiu, foi?" },
] as const;

/** A cutucada com texto escrito por quem cutuca (decisão 0064). Na praça, só aparece o 💬. */
export const cutucadaLivre = {
  codigo: "Livre",
  emoji: "💬",
  texto: "",
} as const;

/** Em caracteres (pontos de código), como o servidor conta. */
export const TextoMaximoCutucada = 80;

export type CodigoCutucada =
  (typeof tiposCutucada)[number]["codigo"] | typeof cutucadaLivre.codigo;

export function getTipoCutucada(codigo: string | null | undefined) {
  if (codigo === cutucadaLivre.codigo) return cutucadaLivre;
  return tiposCutucada.find((t) => t.codigo === codigo);
}

/** Uma linha só, sem espaços sobrando: o mesmo que o servidor guarda. */
export function normalizarTextoCutucada(texto: string): string {
  return texto.replace(/\s+/g, " ").trim();
}

export function contarCaracteres(texto: string): number {
  return [...texto].length;
}
