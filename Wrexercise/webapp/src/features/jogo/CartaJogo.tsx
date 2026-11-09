import type { Carta, CodigoCarta } from "./useCartas";

const iconeDe = (codigo: CodigoCarta) => `/carta-${codigo.toLowerCase()}.png`;

/** Ícone 32×32 da carta, em escala inteira. */
export function IconeCarta({
  codigo,
  escala = 1,
}: {
  codigo: CodigoCarta;
  escala?: 1 | 2;
}) {
  const lado = 32 * escala;
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
 * (docs/gamificacao.md §1.3). A Provocação tem acento coral, e a Resposta, violeta. A moldura e o giz são os
 * mesmos nos dois temas. A versão `compacta` ocupa a largura da vaga (as 5 vagas da Mão cabem lado a lado) e mostra só o ícone e o preço.
 */
export function CartaJogo({
  carta,
  apagada = false,
  compacta = false,
}: {
  carta: Pick<Carta, "codigo" | "nome" | "tipo" | "preco">;
  apagada?: boolean;
  compacta?: boolean;
}) {
  return (
    <div
      className={`relative shrink-0 text-on-carta ${compacta ? "aspect-[3/4] w-full" : "h-32 w-24"} ${apagada ? "opacity-55" : ""}`}
      style={{
        backgroundImage:
          carta.tipo === "Resposta"
            ? "url(/carta-resposta.png)"
            : "url(/carta-provocacao.png)",
        backgroundSize: "100% 100%",
        imageRendering: "pixelated",
      }}
    >
      {/* Na compacta (~60px) o nome não cabe legível na faixa: o ícone identifica a carta e o nome fica para leitores de tela. */}
      <p
        className={
          compacta
            ? "sr-only"
            : "absolute inset-x-3 top-2 grid h-7 place-items-center text-center font-display text-xs leading-[0.95] text-balance uppercase"
        }
      >
        {carta.nome}
      </p>
      <span className="sr-only">
        {carta.tipo === "Provocacao" ? "Provocação" : "Resposta"}
      </span>
      <div
        className={`absolute inset-x-0 flex justify-center ${compacta ? "top-[30%]" : "top-10"}`}
      >
        <IconeCarta codigo={carta.codigo} escala={compacta ? 1 : 2} />
      </div>
      <p
        className={`absolute flex items-baseline gap-0.5 rounded-md border-2 border-carta-tinta bg-carta-tinta text-carta-giz ${compacta ? "-right-1 -bottom-1 px-1" : "-right-1.5 -bottom-1.5 px-1.5 py-0.5"}`}
      >
        <span className={`font-score ${compacta ? "text-xs" : "text-base"}`}>
          {carta.preco}
        </span>
        <span className="text-2xs font-bold">EN</span>
      </p>
    </div>
  );
}
