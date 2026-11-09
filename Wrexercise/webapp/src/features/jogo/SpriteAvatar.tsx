import type { CSSProperties } from "react";
import {
  animacoes,
  direcoes,
  folha as formato,
  type Direcao,
  type NomeAnimacao,
} from "./animacoes";

type SpriteAvatarProps = {
  /** Caminho da folha em 8 direções (células de 48 px; ver `folha` em animacoes.ts). */
  folha: string;
  animacao: NomeAnimacao;
  direcao: Direcao;
  /** Fator inteiro de ampliação: 2 ou 3 mantêm os pixels nítidos. */
  escala: number;
  /** Mostra um só quadro da pose, sem animar (movimento reduzido). */
  estatico?: boolean;
  className?: string;
  style?: CSSProperties;
  /** Chamado ao fim de uma animação que toca uma vez. */
  onFim?: () => void;
};

export function SpriteAvatar({
  folha,
  animacao,
  direcao,
  escala,
  estatico = false,
  className = "",
  style,
  onFim,
}: SpriteAvatarProps) {
  const { linha, quadros, ms, repete, quadroEstatico } = animacoes[animacao];
  const celula = formato.celula * escala;

  return (
    <span
      aria-hidden="true"
      className={`sprite-avatar ${estatico ? "" : repete ? "sprite-repete" : "sprite-uma-vez"} ${className}`}
      onAnimationEnd={onFim}
      style={
        {
          ...style,
          width: celula,
          height: celula,
          backgroundImage: `url(${folha})`,
          backgroundSize: `${celula * formato.colunas}px ${celula * formato.linhas}px`,
          backgroundPositionX: estatico
            ? `${-quadroEstatico * celula}px`
            : undefined,
          backgroundPositionY: `${-(linha + direcoes.indexOf(direcao)) * celula}px`,
          "--quadros": quadros,
          "--ms": `${ms}ms`,
          "--celula": `${celula}px`,
        } as CSSProperties
      }
    />
  );
}
