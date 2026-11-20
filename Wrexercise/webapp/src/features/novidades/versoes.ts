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

/** Quantas versões o diálogo mostra de uma vez; o resto fica na página de Novidades. */
const maximoNoDialogo = 5;

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
 * O que mostrar ao abrir o app. Sem versão vista neste aparelho, só a atual; com uma versão antiga,
 * todas as que vieram depois dela; em dia, nada.
 */
export function getNovidadesPendentes(vista: string | null): Versao[] {
  if (vista === null) return versoes.slice(0, 1);
  return versoes
    .filter(({ versao }) => compararVersoes(versao, vista) > 0)
    .slice(0, maximoNoDialogo);
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

export function marcarVersaoVista() {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieVersao}=${encodeURIComponent(versaoAtual)}; Max-Age=${400 * 24 * 60 * 60}; Path=/; SameSite=Lax${seguro}`;
}

/** "2026-11-20" → "20/11/2026", sem passar pelo fuso. */
export function formatarDataVersao(data: string): string {
  const [ano, mes, dia] = data.split("-");
  return `${dia}/${mes}/${ano}`;
}
