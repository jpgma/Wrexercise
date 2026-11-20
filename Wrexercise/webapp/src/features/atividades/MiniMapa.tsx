import { MapPin } from "lucide-react";
import { useState } from "react";

/** O mesmo zoom do servidor (`TileMapa.ZoomPadrao`): nível de rua. */
const zoom = 16;
const tamanhoTile = 256;
/** Quantos pixels do tile cabem no quadrado: uns 450 metros de largura. */
const janela = 192;

/** A posição do ponto no esquema de tiles do OpenStreetMap (Web Mercator), em tiles. */
function posicaoNoTile(latitude: number, longitude: number) {
  const n = 2 ** zoom;
  const lat = (latitude * Math.PI) / 180;
  return {
    x: ((longitude + 180) / 360) * n,
    y: ((1 - Math.asinh(Math.tan(lat)) / Math.PI) / 2) * n,
  };
}

/**
 * Um quadrado com o mapa em volta do ponto e o pino no centro (ADR 0080). Os tiles vêm do próprio
 * servidor (`base` + `/{dx}/{dy}`), que só serve os vizinhos do local guardado no banco. Um toque abre o
 * Google Maps, fora do aplicativo. Sem rede ou sem o servidor de mapas, fica só o pino sobre o padrão.
 */
export function MiniMapa({
  base,
  latitude,
  longitude,
  rotulo,
  tamanho = 88,
  className = "",
}: {
  base: string;
  latitude: number;
  longitude: number;
  /** O texto para leitores de tela, como "Ver no mapa o local do comprovante 1". */
  rotulo: string;
  tamanho?: number;
  className?: string;
}) {
  const [falhou, setFalhou] = useState(false);
  const { x, y } = posicaoNoTile(latitude, longitude);
  const px = (x - Math.floor(x)) * tamanhoTile;
  const py = (y - Math.floor(y)) * tamanhoTile;
  // Os 2×2 tiles que deixam o ponto longe das bordas: o vizinho do lado mais próximo.
  const dx0 = px < tamanhoTile / 2 ? -1 : 0;
  const dy0 = py < tamanhoTile / 2 ? -1 : 0;
  const escala = tamanho / janela;
  const bx = (px + (dx0 === -1 ? tamanhoTile : 0)) * escala;
  const by = (py + (dy0 === -1 ? tamanhoTile : 0)) * escala;
  const lado = tamanhoTile * escala;

  return (
    <a
      href={`https://www.google.com/maps/search/?api=1&query=${latitude},${longitude}`}
      target="_blank"
      rel="noopener noreferrer"
      aria-label={rotulo}
      style={{ width: tamanho, height: tamanho }}
      className={`relative block shrink-0 overflow-hidden rounded-xl border border-border bg-surface-muted tap ${className}`}
    >
      {falhou ? (
        <span aria-hidden="true" className="absolute inset-0 halftone" />
      ) : (
        <span
          aria-hidden="true"
          className="mapa-tiles absolute grid grid-cols-2"
          style={{
            left: tamanho / 2 - bx,
            top: tamanho / 2 - by,
            width: lado * 2,
            height: lado * 2,
          }}
        >
          {[0, 1].flatMap((linha) =>
            [0, 1].map((coluna) => (
              <img
                key={`${linha}-${coluna}`}
                src={`${base}/${dx0 + coluna}/${dy0 + linha}`}
                alt=""
                draggable={false}
                loading="lazy"
                onError={() => setFalhou(true)}
                className="size-full select-none"
              />
            )),
          )}
        </span>
      )}
      <MapPin
        aria-hidden="true"
        className="absolute top-1/2 left-1/2 size-7 -translate-x-1/2 -translate-y-full fill-primary text-surface drop-shadow"
      />
    </a>
  );
}

/** A atribuição que a política de uso do OpenStreetMap exige perto de todo mapa. */
export function AtribuicaoMapa({ className = "" }: { className?: string }) {
  return (
    <span className={`text-2xs text-muted ${className}`}>
      Mapa © OpenStreetMap
    </span>
  );
}
