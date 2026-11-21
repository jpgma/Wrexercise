import type { Bbox, Trecho } from "./usePercurso";

/** O tamanho do tile do OpenStreetMap, em px. */
export const tamanhoTile = 256;
/** Os mesmos limites da rota de tiles do servidor (`MapaPercurso.cs`). */
export const zoomMinimo = 12;
export const zoomMaximo = 17;
const maxTiles = 36;

/** O ponto no plano do Web Mercator, em px do zoom. */
export function projetar(latitude: number, longitude: number, zoom: number) {
  const n = tamanhoTile * 2 ** zoom;
  const lat = (latitude * Math.PI) / 180;
  return {
    x: ((longitude + 180) / 360) * n,
    y: ((1 - Math.asinh(Math.tan(lat)) / Math.PI) / 2) * n,
  };
}

function faixaDeTiles(bbox: Bbox, zoom: number) {
  const no = projetar(bbox.latMax, bbox.lonMin, zoom);
  const se = projetar(bbox.latMin, bbox.lonMax, zoom);
  return {
    xMin: Math.floor(no.x / tamanhoTile),
    yMin: Math.floor(no.y / tamanhoTile),
    xMax: Math.floor(se.x / tamanhoTile),
    yMax: Math.floor(se.y / tamanhoTile),
  };
}

/** A caixa que cobre o percurso, com os pontos extras (os ainda não enviados, o do parceiro). */
export function juntarBbox(bbox: Bbox | null, trechos: Trecho[]): Bbox | null {
  let resultado = bbox;
  for (const trecho of trechos) {
    for (const [lat, lon] of trecho) {
      resultado = resultado
        ? {
            latMin: Math.min(resultado.latMin, lat),
            lonMin: Math.min(resultado.lonMin, lon),
            latMax: Math.max(resultado.latMax, lat),
            lonMax: Math.max(resultado.lonMax, lon),
          }
        : { latMin: lat, lonMin: lon, latMax: lat, lonMax: lon };
    }
  }
  return resultado;
}

export type Enquadramento = {
  zoom: number;
  /** O canto de cima à esquerda da janela, em px do zoom. */
  origem: { x: number; y: number };
  largura: number;
  altura: number;
  /** Os tiles que o servidor serve e que tocam a janela. */
  tiles: { x: number; y: number }[];
};

/**
 * O maior zoom em que o percurso cabe na janela, com folga nas bordas, e os tiles a pedir. A janela não pode
 * passar de 512 px em nenhum lado: assim ela nunca vai além de um tile em volta do percurso, que é o que o
 * servidor aceita (`bboxServidor`, a caixa do que o servidor mostra a quem vê).
 */
export function enquadrar(
  bbox: Bbox,
  largura: number,
  altura: number,
  bboxServidor: Bbox | null = bbox,
  folga = 24,
): Enquadramento {
  let zoom = zoomMinimo;
  for (let z = zoomMaximo; z >= zoomMinimo; z--) {
    const no = projetar(bbox.latMax, bbox.lonMin, z);
    const se = projetar(bbox.latMin, bbox.lonMax, z);
    const f = faixaDeTiles(bbox, z);
    if (
      se.x - no.x <= largura - 2 * folga &&
      se.y - no.y <= altura - 2 * folga &&
      (f.xMax - f.xMin + 1) * (f.yMax - f.yMin + 1) <= maxTiles
    ) {
      zoom = z;
      break;
    }
  }

  const no = projetar(bbox.latMax, bbox.lonMin, zoom);
  const se = projetar(bbox.latMin, bbox.lonMax, zoom);
  const origem = {
    x: (no.x + se.x) / 2 - largura / 2,
    y: (no.y + se.y) / 2 - altura / 2,
  };

  // Só os tiles que o servidor aceita: os do percurso que ele mostra, com um de margem.
  const tiles: { x: number; y: number }[] = [];
  if (bboxServidor) {
    const permitido = faixaDeTiles(bboxServidor, zoom);
    const total =
      (permitido.xMax - permitido.xMin + 1) *
      (permitido.yMax - permitido.yMin + 1);
    if (total <= maxTiles) {
      for (
        let y = Math.floor(origem.y / tamanhoTile);
        y <= Math.floor((origem.y + altura) / tamanhoTile);
        y++
      ) {
        for (
          let x = Math.floor(origem.x / tamanhoTile);
          x <= Math.floor((origem.x + largura) / tamanhoTile);
          x++
        ) {
          if (
            x >= permitido.xMin - 1 &&
            x <= permitido.xMax + 1 &&
            y >= permitido.yMin - 1 &&
            y <= permitido.yMax + 1
          ) {
            tiles.push({ x, y });
          }
        }
      }
    }
  }

  return { zoom, origem, largura, altura, tiles };
}

/** Um trecho em px da janela. */
export function paraJanela(trecho: Trecho, janela: Enquadramento) {
  return trecho.map(([lat, lon]) => {
    const p = projetar(lat, lon, janela.zoom);
    return { x: p.x - janela.origem.x, y: p.y - janela.origem.y };
  });
}
