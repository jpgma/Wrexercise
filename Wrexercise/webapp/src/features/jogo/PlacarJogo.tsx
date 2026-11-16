import { useLayoutEffect, useRef } from "react";
import { Avatar } from "@/components/Avatar";
import { primeiroNome } from "./textosCartas";

/** Um participante no Placar: o item da classificação do `resumo` (ADR 0066). */
export type ItemPlacar = {
  posicao: number;
  usuarioId: string;
  nomeExibicao: string;
  semanasCumpridas: number;
  sessoesVerificadas: number;
  posicaoInicioSemana: number | null;
};

/** A classificação inteira do desafio em destaque, para o Placar e as medalhas da praça. */
export type PlacarCena = { itens: ItemPlacar[]; encerrado: boolean };

const plural = (n: number, singular: string, varios: string) =>
  `${n} ${n === 1 ? singular : varios}`;

/**
 * O Placar no alto da praça, como num fliperama: todos os participantes na ordem da classificação,
 * com a posição, o retrato, o primeiro nome e as semanas cumpridas. Abre já rolado até quem vê.
 * A seta compara com o início da semana; com o desafio encerrado, a ordem é a final, sem setas.
 */
export function PlacarJogo({
  placar,
  usuarioId,
  dormindo,
  oculto,
  onToque,
}: {
  placar: PlacarCena;
  usuarioId: string;
  dormindo: Set<string>;
  oculto: boolean;
  onToque: (usuarioId: string) => void;
}) {
  const fileira = useRef<HTMLOListElement>(null);
  const meu = useRef<HTMLLIElement>(null);

  // Rola só a fileira, e não a página como faria o `scrollIntoView`.
  useLayoutEffect(() => {
    const lista = fileira.current;
    const item = meu.current;
    if (!lista || !item) return;
    lista.scrollLeft =
      item.offsetLeft - (lista.clientWidth - item.clientWidth) / 2;
  }, [placar.itens.length]);

  return (
    <div
      aria-hidden={oculto}
      inert={oculto}
      className={`absolute inset-x-0 top-0 p-1.5 transition-opacity duration-200 ${oculto ? "pointer-events-none opacity-0" : ""}`}
      style={{ zIndex: 1000 }}
    >
      <div className="flex items-center gap-1 rounded-lg border-2 border-outline bg-surface/90 p-1 shadow-sticker-sm">
        {placar.encerrado && (
          <span className="badge badge-game shrink-0 shadow-none">Final</span>
        )}
        <ol
          ref={fileira}
          aria-label={
            placar.encerrado
              ? "Classificação final do desafio"
              : "Classificação do desafio"
          }
          className="flex min-w-0 flex-1 snap-x gap-1 overflow-x-auto [scrollbar-width:none]"
        >
          {placar.itens.map((item) => {
            const eu = item.usuarioId === usuarioId;
            const sono = dormindo.has(item.usuarioId);
            const variacao =
              placar.encerrado || item.posicaoInicioSemana === null
                ? 0
                : item.posicaoInicioSemana - item.posicao;
            const nome = primeiroNome(item.nomeExibicao);
            const rotulo = [
              `${item.posicao}º`,
              eu ? `${nome} (você)` : nome,
              plural(
                item.semanasCumpridas,
                "semana cumprida",
                "semanas cumpridas",
              ),
              variacao > 0 && `subiu ${variacao} desde segunda`,
              variacao < 0 && `caiu ${-variacao} desde segunda`,
              sono && "dormindo",
            ]
              .filter(Boolean)
              .join(", ");
            return (
              <li
                key={item.usuarioId}
                ref={eu ? meu : undefined}
                className="snap-start"
              >
                <button
                  type="button"
                  aria-label={rotulo}
                  className={`tap flex h-10 items-center gap-1 rounded-md pr-1.5 pl-1 ${eu ? "bg-game-soft text-on-game-soft" : "text-foreground"} ${sono ? "opacity-55" : ""}`}
                  onClick={() => onToque(item.usuarioId)}
                >
                  <span
                    className={`badge-rank size-6 text-xs ${item.posicao === 1 ? "badge-rank-1" : ""}`}
                  >
                    {item.posicao}
                  </span>
                  <Avatar
                    nome={item.nomeExibicao}
                    id={item.usuarioId}
                    size={24}
                  />
                  <span className="max-w-16 truncate text-xs font-bold">
                    {eu ? "Você" : nome}
                  </span>
                  <span className="font-score text-base leading-none">
                    {item.semanasCumpridas}
                    <span className="text-2xs">★</span>
                  </span>
                  {variacao !== 0 && (
                    <span
                      key={variacao}
                      className={`animate-pop text-2xs leading-none font-extrabold ${variacao > 0 ? "text-success" : "text-danger"}`}
                    >
                      {variacao > 0 ? `▲${variacao}` : `▼${-variacao}`}
                    </span>
                  )}
                  {sono && <span className="text-xs leading-none">💤</span>}
                </button>
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}
