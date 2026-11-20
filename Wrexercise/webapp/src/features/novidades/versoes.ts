import dados from "./versoes.json";

/** Uma versão do app e o que mudou nela, na língua de quem usa (decisão 0077). */
export type Versao = {
  versao: string;
  data: string;
  novidades: string[];
};

/** Todas as versões, da mais nova para a mais antiga. A primeira é a versão atual. */
export const versoes: readonly Versao[] = dados;

export const versaoAtual = versoes[0]?.versao ?? "0.0.0";

/** Compara duas versões `X.Y.Z`: negativo se `a` é mais antiga, positivo se é mais nova. */
export function compararVersoes(a: string, b: string): number {
  const partesA = a.split(".").map(Number);
  const partesB = b.split(".").map(Number);
  for (let i = 0; i < 3; i++) {
    const diferenca = (partesA[i] ?? 0) - (partesB[i] ?? 0);
    if (diferenca !== 0) return diferenca;
  }
  return 0;
}

/**
 * O que mostrar ao abrir o app, da mais antiga para a mais nova, para o diálogo contar a história
 * em ordem. Sem versão vista neste aparelho, só a atual; com uma versão antiga, todas as que vieram
 * depois dela, sem limite; em dia, nada.
 */
export function getNovidadesPendentes(vista: string | null): Versao[] {
  if (vista === null) return versoes.slice(0, 1);
  return versoes
    .filter(({ versao }) => compararVersoes(versao, vista) > 0)
    .reverse();
}

// A última versão vista fica num cookie comum, como o passeio do Jogo (`ComoJogar.tsx`): não é
// credencial, e o ESLint bloqueia `localStorage`. Vale por aparelho.
const cookieVersao = "wx_versao_vista";

export function getVersaoVista(): string | null {
  const parte = document.cookie
    .split(";")
    .map((p) => p.trim())
    .find((p) => p.startsWith(`${cookieVersao}=`));
  return parte
    ? decodeURIComponent(parte.slice(cookieVersao.length + 1))
    : null;
}

/** Guarda `versao` como a última vista; quem fecha o diálogo no meio vê só o resto da próxima vez. */
export function marcarVersaoVista(versao: string = versaoAtual) {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieVersao}=${encodeURIComponent(versao)}; Max-Age=${400 * 24 * 60 * 60}; Path=/; SameSite=Lax${seguro}`;
}

/** "2026-11-20" → "20/11/2026", sem passar pelo fuso. */
export function formatarDataVersao(data: string): string {
  const [ano, mes, dia] = data.split("-");
  return `${dia}/${mes}/${ano}`;
}
