import type { CSSProperties } from "react";
import {
  animacoes,
  direcoes,
  folha as formato,
  type Direcao,
  type NomeAnimacao,
} from "./animacoes";
import { useLinhasDaFolha } from "@/components/folhaAvatar";

type SpriteAvatarProps = {
  /** Caminho da folha em 8 direções (células de 48 px; ver `folha` em animacoes.ts). */
  folha: string;
  animacao: NomeAnimacao;
  direcao: Direcao;
  /** Fator inteiro de ampliação: 2 ou 3 mantêm os pixels nítidos. */
  escala: number;
  /** Mostra um só quadro da pose, sem animar (movimento reduzido). */
  estatico?: boolean;
  /** O quadro mostrado quando `estatico`; sem valor, o que melhor identifica a pose. */
  quadro?: number;
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
  quadro,
  className = "",
  style,
  onFim,
}: SpriteAvatarProps) {
  // Uma folha sem a linha da animação (a de 97 linhas não tem as de dormir) mostra `parado`, parado.
  const semLinha =
    animacoes[animacao].linha + direcoes.length > useLinhasDaFolha(folha);
  const { linha, quadros, ms, repete, quadroEstatico } =
    animacoes[semLinha ? "parado" : animacao];
  const parar = estatico || semLinha;
  const celula = formato.celula * escala;

  return (
    <span
      aria-hidden="true"
      className={`sprite-avatar ${parar ? "" : repete ? "sprite-repete" : "sprite-uma-vez"} ${className}`}
      onAnimationEnd={onFim}
      style={
        {
          ...style,
          width: celula,
          height: celula,
          backgroundImage: `url(${folha})`,
          // Só a largura: a altura acompanha, e serve às folhas de 97 e de 129 linhas.
          backgroundSize: `${celula * formato.colunas}px auto`,
          backgroundPositionX: parar
            ? `${-(semLinha ? 0 : (quadro ?? quadroEstatico)) * celula}px`
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
