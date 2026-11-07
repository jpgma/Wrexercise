const nomesFolhasPadrao = [
  "bia",
  "caio",
  "davi",
  "jonas",
  "lara",
  "nina",
] as const;

/** FNV-1a de 32 bits: estável entre execuções e sem dependências. */
function hashFnv1a(texto: string): number {
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
