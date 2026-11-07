import type { CSSProperties } from "react";
import { animacoes, type NomeAnimacao } from "./animacoes";

type SpriteAvatarProps = {
  /** Caminho da folha 256×384 (8 colunas × 12 linhas de 32 px). */
  folha: string;
  animacao: NomeAnimacao;
  /** Fator inteiro de ampliação: 2 ou 3 mantêm os pixels nítidos. */
  escala: number;
  /** Mostra um só quadro da pose, sem animar (movimento reduzido). */
  estatico?: boolean;
  espelhar?: boolean;
  /** Chamado ao fim de uma animação que toca uma vez. */
  onFim?: () => void;
};

export function SpriteAvatar({
  folha,
  animacao,
  escala,
  estatico = false,
  espelhar = false,
  onFim,
}: SpriteAvatarProps) {
  const { linha, quadros, ms, repete, quadroEstatico } = animacoes[animacao];
  const celula = 32 * escala;

  return (
    <span
      aria-hidden="true"
      className={`sprite-avatar ${estatico ? "" : repete ? "sprite-repete" : "sprite-uma-vez"}`}
      onAnimationEnd={onFim}
      style={
        {
          width: celula,
          height: celula,
          backgroundImage: `url(${folha})`,
          backgroundSize: `${celula * 8}px ${celula * 12}px`,
          backgroundPositionX: estatico
            ? `${-quadroEstatico * celula}px`
            : undefined,
          backgroundPositionY: `${-linha * celula}px`,
          transform: espelhar ? "scaleX(-1)" : undefined,
          "--quadros": quadros,
          "--ms": `${ms}ms`,
          "--celula": `${celula}px`,
        } as CSSProperties
      }
    />
  );
}
