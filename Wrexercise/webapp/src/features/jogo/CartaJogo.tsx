import type { Carta, CodigoCarta } from "./useCartas";

const iconeDe = (codigo: CodigoCarta) => `/carta-${codigo.toLowerCase()}.png`;

/** Ícone 16×16 da carta, em escala inteira. */
export function IconeCarta({
  codigo,
  escala = 2,
}: {
  codigo: CodigoCarta;
  escala?: 2 | 3;
}) {
  const lado = 16 * escala;
  return (
    <img
      src={iconeDe(codigo)}
      alt=""
      width={lado}
      height={lado}
      className="shrink-0 [image-rendering:pixelated]"
    />
  );
}

/**
 * A carta em 4× (96×128): moldura em pixel, nome na faixa, ícone e preço como etiqueta de placar
 * (docs/gamificacao.md §1.3). A moldura e o giz são os mesmos nos dois temas.
 */
export function CartaJogo({
  carta,
  apagada = false,
}: {
  carta: Pick<Carta, "codigo" | "nome" | "tipo" | "preco">;
  apagada?: boolean;
}) {
  return (
    <div
      className={`relative h-32 w-24 shrink-0 text-on-carta ${apagada ? "opacity-55" : ""}`}
      style={{
        backgroundImage: "url(/carta-provocacao.png)",
        backgroundSize: "100% 100%",
        imageRendering: "pixelated",
      }}
    >
      <p className="absolute inset-x-3 top-2 grid h-7 place-items-center text-center font-display text-xs leading-[0.95] text-balance uppercase">
        {carta.nome}
      </p>
      <span className="sr-only">
        {carta.tipo === "Provocacao" ? "Provocação" : "Resposta"}
      </span>
      <div className="absolute inset-x-0 top-12 flex justify-center">
        <IconeCarta codigo={carta.codigo} escala={3} />
      </div>
      <p className="absolute -right-1.5 -bottom-1.5 flex items-baseline gap-0.5 rounded-md border-2 border-carta-tinta bg-carta-tinta px-1.5 py-0.5 text-carta-giz">
        <span className="font-score text-base">{carta.preco}</span>
        <span className="text-2xs font-bold">EN</span>
      </p>
    </div>
  );
}
