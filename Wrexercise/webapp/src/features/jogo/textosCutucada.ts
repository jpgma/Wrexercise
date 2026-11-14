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

export type CodigoCutucada = (typeof tiposCutucada)[number]["codigo"];

export function getTipoCutucada(codigo: string | null | undefined) {
  return tiposCutucada.find((t) => t.codigo === codigo);
}
