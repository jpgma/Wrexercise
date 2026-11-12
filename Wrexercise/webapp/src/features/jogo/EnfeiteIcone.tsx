import type { CSSProperties } from "react";
import { atlasEnfeites, getDesenhoEnfeite } from "./enfeitesNaPraca";

/**
 * O desenho de um Enfeite (o quadro 1 do atlas `enfeites.png`, ampliado sem suavizar), encaixado num
 * quadrado de `tamanho` px e apoiado na base. `conquista` define o nível que aparece.
 */
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
  const desenho = getDesenhoEnfeite(conquista);
  if (!desenho) return null;
  const { celula, quadros, linhas } = atlasEnfeites;
  const maior = Math.max(desenho.largura, desenho.altura);
  // Escala inteira quando cabe, para os pixels ficarem iguais; fracionária só para encolher.
  const fator =
    tamanho >= maior ? Math.floor(tamanho / maior) : tamanho / maior;
  return (
    <span
      aria-hidden="true"
      className={`inline-flex items-end justify-center ${brilho ? "enfeite-brilho" : ""} ${className}`}
      style={{ width: tamanho, height: tamanho, ...style }}
    >
      <span
        className="block"
        style={{
          width: desenho.largura * fator,
          height: desenho.altura * fator,
          backgroundImage: "url(/enfeites.png)",
          backgroundSize: `${celula * quadros * fator}px ${celula * linhas * fator}px`,
          backgroundPosition: `${-desenho.esquerda * fator}px ${-(desenho.linha * celula + celula - desenho.altura) * fator}px`,
          imageRendering: "pixelated",
        }}
      />
    </span>
  );
}
