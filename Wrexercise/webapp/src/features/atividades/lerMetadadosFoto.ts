/**
 * Lê do EXIF de um JPEG a data em que a foto foi tirada e a localização (ADR 0052), antes de o canvas
 * apagar os metadados. Leitor mínimo, sem dependência: só as etiquetas que o aplicativo usa.
 * Qualquer arquivo estranho resulta em "sem metadados", nunca em erro.
 */

/** `Camera`: capturada pela câmera do próprio aplicativo, com a data do momento (ADR 0058). */
export type OrigemDataCaptura = "Exif" | "Arquivo" | "Camera";

export type MetadadosFoto = {
  capturadaEm: Date | null;
  origem: OrigemDataCaptura | null;
  latitude: number | null;
  longitude: number | null;
};

// O EXIF fica no começo do arquivo; 128 KB cobrem o APP1 das câmeras de celular.
const bytesLidos = 128 * 1024;

const tagExifIfd = 0x8769;
const tagGpsIfd = 0x8825;
const tagDataOriginal = 0x9003;
const tagFusoOriginal = 0x9011;
const tagLatitudeRef = 0x0001;
const tagLatitude = 0x0002;
const tagLongitudeRef = 0x0003;
const tagLongitude = 0x0004;

type Entrada = { tipo: number; quantidade: number; posicaoValor: number };

class LeitorTiff {
  constructor(
    private readonly view: DataView,
    private readonly inicio: number,
    private readonly littleEndian: boolean,
  ) {}

  u16(posicao: number) {
    return this.view.getUint16(this.inicio + posicao, this.littleEndian);
  }

  u32(posicao: number) {
    return this.view.getUint32(this.inicio + posicao, this.littleEndian);
  }

  entradas(posicaoIfd: number): Map<number, Entrada> {
    const mapa = new Map<number, Entrada>();
    const total = this.u16(posicaoIfd);
    for (let i = 0; i < total; i++) {
      const entrada = posicaoIfd + 2 + i * 12;
      const tipo = this.u16(entrada + 2);
      const quantidade = this.u32(entrada + 4);
      const tamanho = (tipo === 5 ? 8 : 1) * quantidade;
      // Valores de até 4 bytes ficam dentro da própria entrada; os maiores, no deslocamento indicado.
      const posicaoValor = tamanho <= 4 ? entrada + 8 : this.u32(entrada + 8);
      mapa.set(this.u16(entrada), { tipo, quantidade, posicaoValor });
    }
    return mapa;
  }

  texto(entrada: Entrada | undefined): string | null {
    if (!entrada || entrada.tipo !== 2) return null;
    let texto = "";
    for (let i = 0; i < entrada.quantidade; i++) {
      const byte = this.view.getUint8(this.inicio + entrada.posicaoValor + i);
      if (byte === 0) break;
      texto += String.fromCharCode(byte);
    }
    return texto;
  }

  /** Graus, minutos e segundos (três RATIONAL) em graus decimais. */
  grausDecimais(entrada: Entrada | undefined): number | null {
    if (!entrada || entrada.tipo !== 5 || entrada.quantidade < 3) return null;
    let total = 0;
    for (let i = 0; i < 3; i++) {
      const numerador = this.u32(entrada.posicaoValor + i * 8);
      const denominador = this.u32(entrada.posicaoValor + i * 8 + 4);
      if (denominador === 0) return null;
      total += numerador / denominador / 60 ** i;
    }
    return total;
  }
}

function encontrarTiff(view: DataView): LeitorTiff | null {
  if (view.getUint16(0) !== 0xffd8) return null;
  let posicao = 2;
  while (posicao + 4 <= view.byteLength) {
    const marcador = view.getUint16(posicao);
    if ((marcador & 0xff00) !== 0xff00 || marcador === 0xffda) return null;
    const tamanho = view.getUint16(posicao + 2);
    // APP1 que começa com "Exif\0\0".
    if (marcador === 0xffe1 && view.getUint32(posicao + 4) === 0x45786966) {
      const inicio = posicao + 10;
      const ordem = view.getUint16(inicio);
      if (ordem !== 0x4949 && ordem !== 0x4d4d) return null;
      return new LeitorTiff(view, inicio, ordem === 0x4949);
    }
    posicao += 2 + tamanho;
  }
  return null;
}

/** "2026:11:10 07:12:30" com fuso opcional "-03:00". Sem fuso, vale o relógio do aparelho. */
function interpretarData(
  data: string | null,
  fuso: string | null,
): Date | null {
  const partes = data?.match(
    /^(\d{4}):(\d{2}):(\d{2}) (\d{2}):(\d{2}):(\d{2})/,
  );
  if (!partes) return null;
  const [ano = 0, mes = 1, dia = 1, hora = 0, minuto = 0, segundo = 0] = partes
    .slice(1)
    .map(Number);
  const offset = fuso?.match(/^([+-])(\d{2}):(\d{2})$/);
  const instante = offset
    ? new Date(
        Date.UTC(ano, mes - 1, dia, hora, minuto, segundo) -
          (offset[1] === "-" ? -1 : 1) *
            (Number(offset[2]) * 60 + Number(offset[3])) *
            60_000,
      )
    : new Date(ano, mes - 1, dia, hora, minuto, segundo);
  return Number.isNaN(instante.getTime()) ? null : instante;
}

function lerExif(view: DataView): Omit<MetadadosFoto, "origem"> {
  const vazio = { capturadaEm: null, latitude: null, longitude: null };
  const tiff = encontrarTiff(view);
  if (!tiff) return vazio;

  const ifd0 = tiff.entradas(tiff.u32(4));
  let capturadaEm: Date | null = null;
  const exif = ifd0.get(tagExifIfd);
  if (exif) {
    const entradas = tiff.entradas(tiff.u32(exif.posicaoValor));
    capturadaEm = interpretarData(
      tiff.texto(entradas.get(tagDataOriginal)),
      tiff.texto(entradas.get(tagFusoOriginal)),
    );
  }

  let latitude: number | null = null;
  let longitude: number | null = null;
  const gps = ifd0.get(tagGpsIfd);
  if (gps) {
    const entradas = tiff.entradas(tiff.u32(gps.posicaoValor));
    const lat = tiff.grausDecimais(entradas.get(tagLatitude));
    const lon = tiff.grausDecimais(entradas.get(tagLongitude));
    if (lat !== null && lon !== null) {
      latitude = tiff.texto(entradas.get(tagLatitudeRef)) === "S" ? -lat : lat;
      longitude =
        tiff.texto(entradas.get(tagLongitudeRef)) === "W" ? -lon : lon;
    }
  }

  return { capturadaEm, latitude, longitude };
}

/**
 * Sem data no EXIF, usa a data do arquivo como pista mais fraca: o servidor só a leva em conta quando ela é
 * anterior ao treino, porque copiar ou receber o arquivo a muda para depois, nunca para antes.
 */
export async function lerMetadadosFoto(arquivo: File): Promise<MetadadosFoto> {
  let exif: Omit<MetadadosFoto, "origem"> = {
    capturadaEm: null,
    latitude: null,
    longitude: null,
  };
  try {
    const buffer = await arquivo.slice(0, bytesLidos).arrayBuffer();
    exif = lerExif(new DataView(buffer));
  } catch {
    // RangeError de um EXIF truncado ou malformado: segue sem metadados.
  }

  if (exif.capturadaEm) return { ...exif, origem: "Exif" };
  if (arquivo.lastModified > 0) {
    return {
      ...exif,
      capturadaEm: new Date(arquivo.lastModified),
      origem: "Arquivo",
    };
  }
  return { ...exif, capturadaEm: null, origem: null };
}
