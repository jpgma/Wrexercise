import { Users } from "lucide-react";
import { useState } from "react";
import type { Carta, CodigoCarta, TipoCarta } from "./useCartas";

const iconeDe = (codigo: CodigoCarta) => `/carta-${codigo.toLowerCase()}.png`;

/** O nome da categoria, como a interface mostra. */
export const nomeTipoCarta: Record<TipoCarta, string> = {
  Provocacao: "Provocação",
  Resposta: "Resposta",
  Global: "Global",
};

/**
 * Ícone 32×32 da carta, em escala inteira. Enquanto a arte de uma carta nova não existe (o TODO MUNDO),
 * fica um ícone provisório no lugar.
 */
export function IconeCarta({
  codigo,
  escala = 1,
}: {
  codigo: CodigoCarta;
  escala?: 1 | 2;
}) {
  const [semArte, setSemArte] = useState(false);
  const lado = 32 * escala;
  if (semArte)
    return (
      <Users
        aria-hidden="true"
        width={lado}
        height={lado}
        strokeWidth={2.5}
        className="shrink-0"
      />
    );
  return (
    <img
      src={iconeDe(codigo)}
      alt=""
      width={lado}
      height={lado}
      onError={() => setSemArte(true)}
      className="shrink-0 [image-rendering:pixelated]"
    />
  );
}

/**
 * A moldura da carta Global. Enquanto `carta-global.png` não existe, a da Provocação passa por um filtro
 * que puxa o coral para o amarelo da recompensa.
 */
function MolduraGlobal() {
  const [semArte, setSemArte] = useState(false);
  return (
    <img
      src={semArte ? "/carta-provocacao.png" : "/carta-global.png"}
      alt=""
      aria-hidden="true"
      onError={() => setSemArte(true)}
      className="absolute inset-0 size-full [image-rendering:pixelated]"
      style={
        semArte ? { filter: "hue-rotate(38deg) saturate(1.4)" } : undefined
      }
    />
  );
}

/**
 * A carta em 4× (96×128): moldura em pixel, nome na faixa, ícone e preço como etiqueta de placar
 * (docs/gamificacao.md §1.3). A Provocação tem acento coral, a Resposta, violeta, e a Global, amarelo. A moldura e o giz são os
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
      style={
        carta.tipo === "Global"
          ? undefined
          : {
              backgroundImage:
                carta.tipo === "Resposta"
                  ? "url(/carta-resposta.png)"
                  : "url(/carta-provocacao.png)",
              backgroundSize: "100% 100%",
              imageRendering: "pixelated",
            }
      }
    >
      {carta.tipo === "Global" && <MolduraGlobal />}
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
      <span className="sr-only">{nomeTipoCarta[carta.tipo]}</span>
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
