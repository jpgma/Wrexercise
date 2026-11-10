import { formatoFolha } from "@/components/folhaAvatar";

/** O formato aceito na importação (docs/avatar-formato.md §6). O servidor confere tudo de novo. */
export const folhaImportada = {
  largura: formatoFolha.colunas * formatoFolha.celula,
  altura: formatoFolha.linhas * formatoFolha.celula,
  /** Abaixo de 1 MiB, para caber no limite do corpo da API com o envelope multipart. */
  maxBytes: 1000 * 1024,
} as const;

const assinatura = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

/**
 * O arquivo já está num formato que o servidor aceita como veio: RGBA de 8 bits ou paleta indexada,
 * sem animação APNG. Assim, uma folha indexada (a do Aseprite, por exemplo) chega sem crescer.
 * Só lê os cabeçalhos dos blocos, sem descomprimir nada.
 */
function isAceitaComoVeio(bytes: Uint8Array): boolean {
  if (!assinatura.every((b, i) => bytes[i] === b)) return false;
  const dados = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  let posicao = 8;
  let primeiro = true;
  while (posicao + 12 <= bytes.length) {
    const tamanho = dados.getUint32(posicao);
    const tipo = String.fromCharCode(
      ...bytes.subarray(posicao + 4, posicao + 8),
    );
    if (primeiro) {
      if (tipo !== "IHDR" || tamanho !== 13) return false;
      const profundidade = bytes[posicao + 16];
      const tipoCor = bytes[posicao + 17];
      const corAceita =
        (tipoCor === 6 && profundidade === 8) ||
        (tipoCor === 3 && [1, 2, 4, 8].includes(profundidade ?? 0));
      if (!corAceita) return false;
      primeiro = false;
    } else if (tipo === "acTL") {
      return false;
    } else if (tipo === "IEND") {
      return posicao + 12 === bytes.length;
    }
    posicao += 12 + tamanho;
  }
  return false;
}

/**
 * Prepara a folha escolhida para o envio. Só confere o tamanho: o desenho é livre. Um PNG RGBA ou
 * indexado vai como veio; qualquer outro (RGB, tons de cinza, 16 bits, APNG, outro formato de imagem)
 * é gravado de novo num canvas, como PNG RGBA.
 */
export async function prepararFolha(arquivo: File): Promise<Blob> {
  const { largura, altura, maxBytes } = folhaImportada;
  const imagem = await createImageBitmap(arquivo).catch(() => {
    throw new Error("Não foi possível abrir essa imagem. Use um arquivo PNG.");
  });
  // Lidos antes do close(): depois dele, o bitmap informa 0×0.
  const { width, height } = imagem;
  if (width !== largura || height !== altura) {
    imagem.close();
    throw new Error(
      `A folha precisa ter exatamente ${largura}×${altura} px. Esta tem ${width}×${height} px.`,
    );
  }

  const original = new Uint8Array(await arquivo.arrayBuffer());
  if (original.length <= maxBytes && isAceitaComoVeio(original)) {
    imagem.close();
    return new Blob([original], { type: "image/png" });
  }

  const canvas = document.createElement("canvas");
  canvas.width = largura;
  canvas.height = altura;
  canvas.getContext("2d")?.drawImage(imagem, 0, 0);
  imagem.close();

  const blob = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, "image/png"),
  );
  if (!blob) throw new Error("Não foi possível preparar a imagem.");
  if (blob.size > maxBytes) {
    throw new Error(
      `A folha ficou com ${Math.ceil(blob.size / 1024)} KB, e o limite é 1000 KB. Use menos cores ou salve com paleta indexada.`,
    );
  }
  return blob;
}
