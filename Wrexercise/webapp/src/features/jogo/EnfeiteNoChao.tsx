import type { CSSProperties } from "react";
import { hashFnv1a } from "@/components/folhaAvatar";
import {
  atlasEnfeites,
  enfeiteAceso,
  type EnfeiteColocado,
} from "./enfeitesNaPraca";

/** A praça é mostrada em 2×. */
const escala = 2;
const celula = atlasEnfeites.celula * escala;

/**
 * Um Enfeite apoiado no chão da praça (docs/gamificacao.md §3.1), com o ciclo parado de 4 quadros fora
 * de sincronia com os outros. O poste fica apagado de dia e pisca aceso no entardecer e à noite.
 */
export function EnfeiteNoChao({
  item,
  reduzido,
  apagado,
  aceso,
  brilho,
}: {
  item: EnfeiteColocado;
  reduzido: boolean;
  /** Durante o Foco, os Enfeites de quem não atua ficam apagados, como os avatares. */
  apagado: boolean;
  /** As luminárias da praça estão acesas. */
  aceso: boolean;
  /** É o Enfeite novo de quem vê a cena: brilha enquanto a encenação toca. */
  brilho: boolean;
}) {
  const { desenho } = item;
  const poste = item.enfeite.conquista.toLowerCase() === enfeiteAceso;
  const ciclo = poste ? (aceso ? "enfeite-aceso" : "") : "sprite-repete";
  const quadros = poste ? 3 : atlasEnfeites.quadros;
  const meiaLargura = (desenho.largura * escala) / 2;
  const atraso = hashFnv1a(item.chave) % (quadros * desenho.ms);

  return (
    <span
      aria-hidden="true"
      // O surgir e o brilho ficam fora do sprite: os dois usam `animation`, como o ciclo de quadros.
      className={`pointer-events-none absolute ${item.extra || brilho ? "enfeite-surgir" : ""} ${brilho ? "enfeite-brilho" : ""}`}
      style={{
        left: item.x - (desenho.esquerda * escala + meiaLargura),
        top: item.y - celula,
        width: celula,
        height: celula,
        zIndex: item.y,
        opacity: apagado ? 0.45 : undefined,
        transition: "opacity 300ms ease-out",
      }}
    >
      <span
        className="sombra-cena absolute"
        style={{
          left: desenho.esquerda * escala + meiaLargura * 0.1,
          top: celula - 5,
          width: meiaLargura * 1.8,
          height: 6,
        }}
      />
      <span
        className={`sprite-avatar absolute inset-0 ${reduzido ? "" : ciclo}`}
        style={
          {
            backgroundImage: "url(/enfeites.png)",
            backgroundSize: `${celula * atlasEnfeites.quadros}px ${celula * atlasEnfeites.linhas}px`,
            // Parado (movimento reduzido ou o poste de dia), fica no quadro 1; o poste aceso, no 2.
            backgroundPositionX: poste && aceso ? -celula : 0,
            backgroundPositionY: -desenho.linha * celula,
            animationDelay: `-${atraso}ms`,
            "--quadros": quadros,
            "--ms": `${desenho.ms}ms`,
            "--celula": `${celula}px`,
          } as CSSProperties
        }
      />
    </span>
  );
}
