import type { CSSProperties } from "react";
import { useFolhaAvatar } from "@/components/folhaAvatar";

type AvatarProps = {
  nome: string;
  /** Identificador estável: a cor continua a mesma se a pessoa mudar de nome. */
  id?: string;
  size?: number;
  ring?: boolean;
};

function getInitials(nome: string): string {
  const palavras = nome.trim().split(/\s+/);
  const primeira = palavras[0]?.charAt(0) ?? "";
  const ultima = palavras.length > 1 ? (palavras.at(-1)?.charAt(0) ?? "") : "";
  return (primeira + ultima).toUpperCase();
}

function getHue(semente: string): number {
  let hash = 0;
  for (const letra of semente) {
    hash = (hash * 31 + letra.charCodeAt(0)) % 360;
  }
  return hash;
}

export function Avatar({ nome, id, size = 40, ring = false }: AvatarProps) {
  const folha = useFolhaAvatar(id ?? "");
  if (id) {
    // Retrato: linha 0, coluna 0 da folha (8×12 células de 32 px), ampliado sem suavizar.
    return (
      <span
        aria-hidden="true"
        className={`avatar shrink-0 rounded-full ${ring ? "ring-2 ring-surface" : ""}`}
        style={
          {
            width: size,
            height: size,
            "--avatar-hue": getHue(id),
            backgroundImage: `url(${folha})`,
            backgroundSize: `${size * 8}px ${size * 12}px`,
            backgroundPosition: "0 0",
            backgroundRepeat: "no-repeat",
            imageRendering: "pixelated",
          } as CSSProperties
        }
      />
    );
  }
  return (
    <span
      aria-hidden="true"
      className={`avatar grid shrink-0 place-items-center rounded-full font-semibold ${ring ? "ring-2 ring-surface" : ""}`}
      style={
        {
          width: size,
          height: size,
          fontSize: size * 0.36,
          "--avatar-hue": getHue(id ?? nome),
        } as CSSProperties
      }
    >
      {getInitials(nome)}
    </span>
  );
}
