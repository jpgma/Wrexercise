import { useState } from "react";
import { enquadrar, juntarBbox, paraJanela, tamanhoTile } from "./geometria";
import type { Percurso, Trecho } from "./usePercurso";

/** A janela do mapa, em px do zoom: o SVG escala junto com a largura da tela. */
const largura = 360;
const altura = 240;

const caminho = (pontos: { x: number; y: number }[]) =>
  pontos
    .map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
    .join(" ");

/**
 * O mapa do Percurso (ADR 0084): os tiles vêm do próprio servidor, que só serve os que estão em volta do
 * percurso; o navegador nunca fala com o servidor de mapas. Os trechos são linhas cheias; as lacunas entre
 * eles, tracejadas. O começo e o fim só aparecem para quem treinou: o grupo vê o percurso aparado.
 * `extra`: os pontos que o aparelho ainda não enviou, desenhados na ponta do trajeto ao vivo.
 */
export function MapaPercurso({
  percurso,
  extra = [],
  rotulo,
  className = "",
}: {
  percurso: Percurso;
  extra?: Trecho[];
  rotulo: string;
  className?: string;
}) {
  const [semTiles, setSemTiles] = useState(false);
  const trechos = [...percurso.segmentos, ...extra.filter((t) => t.length > 0)];
  const parceiro = percurso.parceiro?.segmentos ?? [];
  const bbox = juntarBbox(percurso.bbox, [...extra, ...parceiro]);

  if (!bbox || trechos.length === 0) {
    return (
      <div
        role="img"
        aria-label={rotulo}
        className={`grid aspect-[3/2] w-full place-items-center rounded-xl border border-border bg-surface-muted halftone px-6 text-center text-sm text-muted ${className}`}
      >
        {percurso.voce
          ? "O percurso aparece aqui assim que o GPS firmar."
          : "O percurso ainda está perto do começo. O grupo vê o trajeto depois dos primeiros 200 m."}
      </div>
    );
  }

  const janela = enquadrar(bbox, largura, altura, percurso.bbox);
  const linhas = trechos.map((t) => paraJanela(t, janela));
  const linhasParceiro = parceiro.map((t) => paraJanela(t, janela));
  const lacunas = linhas.slice(1).flatMap((trecho, i) => {
    const a = linhas[i]?.at(-1);
    const b = trecho[0];
    return a && b ? [[a, b]] : [];
  });
  const inicio = linhas[0]?.[0];
  const fim = linhas.at(-1)?.at(-1);
  const base = `/api/atividades/${percurso.atividadeId}/percurso/mapa/${janela.zoom}`;

  return (
    <svg
      role="img"
      aria-label={rotulo}
      viewBox={`0 0 ${largura} ${altura}`}
      className={`block aspect-[3/2] w-full overflow-hidden rounded-xl border border-border bg-surface-muted ${className}`}
    >
      {!semTiles &&
        janela.tiles.map(({ x, y }) => (
          <image
            key={`${x}-${y}`}
            href={`${base}/${x}/${y}`}
            x={x * tamanhoTile - janela.origem.x}
            y={y * tamanhoTile - janela.origem.y}
            width={tamanhoTile}
            height={tamanhoTile}
            onError={() => setSemTiles(true)}
          />
        ))}
      <g fill="none" strokeLinecap="round" strokeLinejoin="round">
        {linhasParceiro.map((pontos, i) => (
          <path
            key={`p${i}`}
            d={caminho(pontos)}
            className="stroke-game"
            strokeWidth={4}
            strokeOpacity={0.85}
          />
        ))}
        {lacunas.map((par, i) => (
          <path
            key={`l${i}`}
            d={caminho(par)}
            className="stroke-primary"
            strokeWidth={3}
            strokeDasharray="4 6"
          />
        ))}
        {linhas.map((pontos, i) => (
          <g key={`t${i}`}>
            <path
              d={caminho(pontos)}
              className="stroke-outline"
              strokeWidth={7}
            />
            <path
              d={caminho(pontos)}
              className="stroke-primary"
              strokeWidth={4}
            />
          </g>
        ))}
      </g>
      {percurso.voce && inicio && fim && (
        <>
          <circle
            cx={inicio.x}
            cy={inicio.y}
            r={6}
            className="fill-surface stroke-outline"
            strokeWidth={3}
          />
          <circle
            cx={fim.x}
            cy={fim.y}
            r={7}
            className={`fill-primary stroke-surface ${percurso.aoVivo ? "motion-safe:animate-pulse" : ""}`}
            strokeWidth={3}
          />
        </>
      )}
      <text
        x={largura - 4}
        y={altura - 4}
        textAnchor="end"
        className="fill-muted text-[9px]"
      >
        Mapa © OpenStreetMap
      </text>
    </svg>
  );
}
