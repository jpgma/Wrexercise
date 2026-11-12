import type { CSSProperties } from "react";

/**
 * Os recortes de `praca-objetos.png` (pixel-art/praca/build.py, que imprime esta lista), em px do
 * mestre 1×: `sx` é a coluna na folha, e `x`/`y` o canto do recorte na praça. Se a praça mudar,
 * rode o build.py e cole a lista nova.
 */
export const objetosPraca = [
  { nome: "floreira-esquerda", sx: 0, x: 0, y: 183, largura: 22, altura: 17 },
  { nome: "caixotes", sx: 22, x: 12, y: 60, largura: 20, altura: 25 },
  // A barra de calistenia (docs/gamificacao.md §9.3) é um objeto à parte para que, no futuro,
  // um avatar possa usá-la.
  { nome: "barra", sx: 42, x: 105, y: 42, largura: 42, altura: 40 },
  { nome: "floreira", sx: 84, x: 151, y: 58, largura: 23, altura: 23 },
  {
    nome: "floreira-direita",
    sx: 107,
    x: 158,
    y: 183,
    largura: 22,
    altura: 17,
  },
] as const;

/** A porta aberta cobre a porta fechada do fundo. */
const portaAberta = { sx: 129, x: 33, y: 33, largura: 27, altura: 47 } as const;
const folha = { largura: 156, altura: 47 } as const;
const escala = 2;

/** O pé da mureta do fundo, em px da praça: a porta fica sempre atrás de quem sai por ela. */
export const peDaMureta = 74 * escala;
/** Os pés de quem está no vão da porta. */
export const vaoDaPorta = { x: 52 * escala, y: peDaMureta + 4 };

function Recorte({
  recorte,
  zIndex,
}: {
  recorte: {
    sx: number;
    x: number;
    y: number;
    largura: number;
    altura: number;
  };
  zIndex: number;
}) {
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute"
      style={
        {
          left: recorte.x * escala,
          top: recorte.y * escala,
          width: recorte.largura * escala,
          height: recorte.altura * escala,
          zIndex,
          backgroundImage: "url(/praca-objetos.png)",
          backgroundSize: `${folha.largura * escala}px ${folha.altura * escala}px`,
          backgroundPosition: `${-recorte.sx * escala}px 0`,
          imageRendering: "pixelated",
        } as CSSProperties
      }
    />
  );
}

/**
 * Os objetos soltos no chão, desenhados por cima do fundo e ordenados pela base, como os avatares:
 * quem fica atrás de um objeto some por trás dele. A porta abre na entrada do dia.
 */
export function ObjetosPraca({
  portaAberta: aberta,
}: {
  portaAberta: boolean;
}) {
  return (
    <>
      {objetosPraca.map((o) => (
        <Recorte key={o.nome} recorte={o} zIndex={(o.y + o.altura) * escala} />
      ))}
      {aberta && <Recorte recorte={portaAberta} zIndex={peDaMureta} />}
    </>
  );
}
