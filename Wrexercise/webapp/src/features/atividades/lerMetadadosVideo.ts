import type { MetadadosFoto } from "./lerMetadadosFoto";

/**
 * Lê do MP4 ou MOV original a data de criação (`mvhd`) e a localização (ISO 6709), antes de a conversão
 * apagar os metadados (ADR 0058). Leitor mínimo, sem dependência: percorre só as caixas do topo, por
 * fatias do arquivo, sem carregá-lo inteiro. Qualquer arquivo estranho resulta em "sem metadados".
 */

const maxBytesMoov = 8 * 1024 * 1024;
const maxCaixas = 64;
const epoca1904 = Date.UTC(1904, 0, 1);
const criacaoMinima = Date.UTC(2000, 0, 1);

const semMetadados: MetadadosFoto = {
  capturadaEm: null,
  origem: null,
  latitude: null,
  longitude: null,
};

function tipo(view: DataView, posicao: number) {
  return String.fromCharCode(
    view.getUint8(posicao),
    view.getUint8(posicao + 1),
    view.getUint8(posicao + 2),
    view.getUint8(posicao + 3),
  );
}

async function lerMoov(arquivo: Blob): Promise<DataView | null> {
  let posicao = 0;
  for (let i = 0; i < maxCaixas && posicao + 8 <= arquivo.size; i++) {
    const cabecalho = new DataView(
      await arquivo.slice(posicao, posicao + 16).arrayBuffer(),
    );
    let tamanho = cabecalho.getUint32(0);
    let inicioConteudo = 8;
    if (tamanho === 1 && cabecalho.byteLength >= 16) {
      tamanho = Number(cabecalho.getBigUint64(8));
      inicioConteudo = 16;
    } else if (tamanho === 0) {
      tamanho = arquivo.size - posicao;
    }
    if (tamanho < inicioConteudo || posicao + tamanho > arquivo.size)
      return null;

    if (tipo(cabecalho, 4) === "moov") {
      if (tamanho > maxBytesMoov) return null;
      return new DataView(
        await arquivo
          .slice(posicao + inicioConteudo, posicao + tamanho)
          .arrayBuffer(),
      );
    }
    posicao += tamanho;
  }
  return null;
}

function lerCriacao(moov: DataView): Date | null {
  for (let p = 0; p + 8 <= moov.byteLength;) {
    const tamanho = moov.getUint32(p);
    if (tamanho < 8 || p + tamanho > moov.byteLength) return null;
    if (tipo(moov, p + 4) === "mvhd" && tamanho >= 20) {
      const versao = moov.getUint8(p + 8);
      const segundos =
        versao === 1 && tamanho >= 28
          ? Number(moov.getBigUint64(p + 12))
          : moov.getUint32(p + 12);
      const instante = epoca1904 + segundos * 1000;
      // Câmeras sem relógio gravam zero.
      return segundos > 0 && instante >= criacaoMinima
        ? new Date(instante)
        : null;
    }
    p += tamanho;
  }
  return null;
}

/** Android grava `©xyz` em `udta`; o iPhone, a chave `com.apple.quicktime.location.ISO6709`. */
function lerLocalizacao(moov: DataView) {
  const texto = new TextDecoder("latin1").decode(moov);
  const achado = /([+-]\d{1,2}\.\d{2,})([+-]\d{1,3}\.\d{2,})/.exec(texto);
  if (!achado) return null;
  const latitude = Number(achado[1]);
  const longitude = Number(achado[2]);
  if (
    Math.abs(latitude) > 90 ||
    Math.abs(longitude) > 180 ||
    (latitude === 0 && longitude === 0)
  ) {
    return null;
  }
  return { latitude, longitude };
}

export async function lerMetadadosVideo(arquivo: File): Promise<MetadadosFoto> {
  let metadados = semMetadados;
  try {
    const moov = await lerMoov(arquivo);
    if (moov) {
      const capturadaEm = lerCriacao(moov);
      const local = lerLocalizacao(moov);
      metadados = {
        capturadaEm,
        origem: capturadaEm ? "Exif" : null,
        latitude: local?.latitude ?? null,
        longitude: local?.longitude ?? null,
      };
    }
  } catch {
    // Arquivo estranho: segue sem metadados.
  }

  if (metadados.capturadaEm || arquivo.lastModified <= 0) return metadados;
  return {
    ...metadados,
    capturadaEm: new Date(arquivo.lastModified),
    origem: "Arquivo",
  };
}
