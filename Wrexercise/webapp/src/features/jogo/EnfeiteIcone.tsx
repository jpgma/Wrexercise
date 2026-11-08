import type { CSSProperties } from "react";

/** O desenho de um Enfeite (16×16, ampliado sem suavizar). `conquista` define o nível que aparece. */
export function EnfeiteIcone({
  conquista,
  tamanho = 32,
  brilho = false,
  className = "",
  style,
}: {
  conquista: string;
  tamanho?: number;
  brilho?: boolean;
  className?: string;
  style?: CSSProperties;
}) {
  return (
    <img
      src={`/enfeite-${conquista.toLowerCase()}.png`}
      alt=""
      aria-hidden="true"
      width={tamanho}
      height={tamanho}
      className={`${brilho ? "enfeite-brilho" : ""} ${className}`}
      style={{ imageRendering: "pixelated", ...style }}
    />
  );
}
