import { lerMetadadosFoto, type MetadadosFoto } from "./lerMetadadosFoto";

const maxLado = 1600;
const qualidade = 0.82;

// Os cartões mostram a miniatura em 64 px: 240 mantém a nitidez em telas 3x.
const maxLadoMiniatura = 240;
const qualidadeMiniatura = 0.6;

/**
 * Sinais sobre a foto ou o vídeo original, lidos antes de o canvas ou a conversão apagarem os metadados
 * (ADR 0052). No vídeo, `dhash` é o do quadro do meio, e `dhashInicio`/`dhashFim`, os dos quadros a 10% e
 * 90% (ADR 0058).
 */
export type SinaisEvidencia = MetadadosFoto & {
  sha256Original: string | null;
  /** Uma captura de tela (ADR 0060). Ausente nos vídeos e nos rascunhos antigos: vale `false`. */
  print?: boolean;
  dhash: string;
  dhashInicio?: string;
  dhashFim?: string;
};

export type TipoEvidencia = "Foto" | "Video";

export type EvidenciaPreparada = {
  /** Ausente nos rascunhos gravados antes dos vídeos: vale `Foto`. */
  tipo?: TipoEvidencia;
  arquivo: File;
  miniatura: Blob | null;
  sinais: SinaisEvidencia;
  /** Só no vídeo. */
  duracaoMs?: number;
};

/** Qualquer coisa que o canvas desenha e que diz o próprio tamanho: bitmap de foto ou quadro de vídeo. */
export type FonteImagem = ImageBitmap | HTMLCanvasElement | OffscreenCanvas;

export class FotoNaoProcessadaError extends Error {
  constructor() {
    super("Não foi possível processar esta foto. Tente outra imagem.");
  }
}

export function redimensionar(
  bitmap: FonteImagem,
  limite: number,
  qualidadeJpeg: number,
): Promise<Blob | null> {
  const escala = Math.min(1, limite / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * escala));
  canvas.height = Math.max(1, Math.round(bitmap.height * escala));
  const ctx = canvas.getContext("2d");
  if (!ctx) return Promise.resolve(null);

  ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  return new Promise((resolve) =>
    canvas.toBlob(resolve, "image/jpeg", qualidadeJpeg),
  );
}

/**
 * dHash de 64 bits: a imagem em 9×8 tons de cinza, um bit por par de vizinhos na linha. Fotos iguais
 * redimensionadas ou recomprimidas dão hashes próximos; o servidor compara a distância entre eles.
 */
export function calcularDhash(bitmap: FonteImagem): string | null {
  const canvas = document.createElement("canvas");
  canvas.width = 9;
  canvas.height = 8;
  const ctx = canvas.getContext("2d", { willReadFrequently: true });
  if (!ctx) return null;

  ctx.drawImage(bitmap, 0, 0, 9, 8);
  const { data } = ctx.getImageData(0, 0, 9, 8);
  const cinza = (x: number, y: number) => {
    const i = (y * 9 + x) * 4;
    return (
      (data[i] ?? 0) * 0.299 +
      (data[i + 1] ?? 0) * 0.587 +
      (data[i + 2] ?? 0) * 0.114
    );
  };

  let bits = 0n;
  for (let y = 0; y < 8; y++) {
    for (let x = 0; x < 8; x++) {
      bits = (bits << 1n) | (cinza(x, y) > cinza(x + 1, y) ? 1n : 0n);
    }
  }
  return bits.toString(16).padStart(16, "0");
}

export async function calcularSha256(arquivo: Blob): Promise<string | null> {
  // Só existe em contexto seguro (HTTPS ou localhost).
  if (typeof crypto.subtle === "undefined") return null;
  const digest = await crypto.subtle.digest(
    "SHA-256",
    await arquivo.arrayBuffer(),
  );
  return Array.from(new Uint8Array(digest), (b) =>
    b.toString(16).padStart(2, "0"),
  ).join("");
}

/**
 * Um print do próprio aparelho tem o tamanho exato da tela, em pixels físicos, em pé ou deitado. A margem
 * cobre o arredondamento de `devicePixelRatio` fracionário (2,625 × 411 = 1078,9 para uma tela de 1080).
 */
function isTamanhoDaTela(largura: number, altura: number) {
  const dpr = window.devicePixelRatio || 1;
  const telaLargura = Math.round(screen.width * dpr);
  const telaAltura = Math.round(screen.height * dpr);
  const perto = (a: number, b: number) => Math.abs(a - b) <= 3;
  return (
    (perto(largura, telaLargura) && perto(altura, telaAltura)) ||
    (perto(largura, telaAltura) && perto(altura, telaLargura))
  );
}

/**
 * Print pelo nome do arquivo ou, quando o nome se perdeu (o seletor de fotos do Android troca o nome e a
 * data do arquivo), por uma imagem do tamanho da tela, sem câmera e sem data no EXIF (ADR 0060).
 */
function isPrint(metadados: MetadadosFoto, bitmap: ImageBitmap) {
  if (metadados.origem === "Print") return true;
  return (
    !metadados.temCamera &&
    metadados.origem !== "Exif" &&
    isTamanhoDaTela(bitmap.width, bitmap.height)
  );
}

/**
 * A câmera do aplicativo sabe quando capturou: a data vale como a do EXIF (ADR 0058). O quadro tirado do
 * vídeo da câmera não tem localização.
 */
export function marcarCapturaNoApp(
  preparada: EvidenciaPreparada,
  capturadaEm: Date,
): EvidenciaPreparada {
  return {
    ...preparada,
    sinais: {
      ...preparada.sinais,
      capturadaEm,
      origem: "Camera",
      latitude: null,
      longitude: null,
      print: false,
    },
  };
}

/**
 * Reduz a foto e gera a miniatura do feed antes do upload, para economizar banda. Passar pelo canvas
 * descarta os metadados do arquivo enviado; a data e a localização seguem em campos à parte.
 * Se o navegador não conseguir processar a imagem, ela não é enviada: o original carregaria o EXIF
 * inteiro para o servidor (Q030).
 */
export async function prepararEvidencia(
  arquivo: File,
): Promise<EvidenciaPreparada> {
  if (!arquivo.type.startsWith("image/") || arquivo.type.includes("gif")) {
    throw new FotoNaoProcessadaError();
  }

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(arquivo);
  } catch {
    throw new FotoNaoProcessadaError();
  }

  try {
    const [foto, miniatura, metadados, sha256Original] = await Promise.all([
      redimensionar(bitmap, maxLado, qualidade),
      redimensionar(bitmap, maxLadoMiniatura, qualidadeMiniatura),
      lerMetadadosFoto(arquivo),
      calcularSha256(arquivo),
    ]);
    const dhash = calcularDhash(bitmap);
    if (!foto || !dhash) throw new FotoNaoProcessadaError();

    const nome = arquivo.name.replace(/\.\w+$/, "") + ".jpg";
    return {
      tipo: "Foto",
      arquivo: new File([foto], nome, { type: "image/jpeg" }),
      miniatura,
      sinais: {
        ...metadados,
        sha256Original,
        dhash,
        print: isPrint(metadados, bitmap),
      },
    };
  } finally {
    bitmap.close();
  }
}
