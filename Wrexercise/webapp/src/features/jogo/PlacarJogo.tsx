import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Avatar } from "@/components/Avatar";
import { primeiroNome } from "./textosCartas";
import { useMovimentoReduzido } from "./useMovimentoReduzido";

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

/** Velocidade do letreiro, em px por segundo: dá para ler um item sem esperar muito pelo próximo. */
const VELOCIDADE = 30;
/** Depois de um toque ou arrasto, o letreiro espera este tempo antes de voltar a correr. */
const PAUSA_MS = 3000;

const plural = (n: number, singular: string, varios: string) =>
  `${n} ${n === 1 ? singular : varios}`;

/**
 * O Placar no alto da praça, como num fliperama: todos os participantes na ordem da classificação,
 * com a posição, o retrato, o primeiro nome e as semanas cumpridas. Abre já rolado até quem vê.
 * A seta compara com o início da semana; com o desafio encerrado, a ordem é a final, sem setas.
 *
 * Quando os itens não cabem na faixa, ela corre sozinha como um letreiro, em volta contínua
 * (ADR 0067). Tocar, arrastar, passar o mouse ou focar pelo teclado pausa o letreiro. Com
 * movimento reduzido, ou quando tudo cabe, a faixa fica parada.
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
  const faixa = useRef<HTMLDivElement>(null);
  const lista = useRef<HTMLOListElement>(null);
  const copia = useRef<HTMLOListElement>(null);
  const meu = useRef<HTMLLIElement>(null);
  /** Instante (`Date.now()`) até o qual o letreiro fica parado; `Infinity` enquanto alguém o segura. */
  const pausadoAte = useRef(0);
  const movimentoReduzido = useMovimentoReduzido();
  const [transborda, setTransborda] = useState(false);
  const corre = transborda && !movimentoReduzido;

  // O letreiro só corre quando os itens não cabem. A cópia fica fora da medida.
  useLayoutEffect(() => {
    const caixa = faixa.current;
    const itens = lista.current;
    if (!caixa || !itens) return;
    const observador = new ResizeObserver(() =>
      setTransborda(itens.offsetWidth > caixa.clientWidth),
    );
    observador.observe(caixa);
    observador.observe(itens);
    return () => observador.disconnect();
  }, []);

  // Rola só a faixa, e não a página como faria o `scrollIntoView`.
  useLayoutEffect(() => {
    const caixa = faixa.current;
    const item = meu.current;
    if (!caixa || !item) return;
    caixa.scrollLeft =
      item.offsetLeft - (caixa.clientWidth - item.clientWidth) / 2;
  }, [placar.itens.length, corre]);

  /** A largura de uma volta: a distância entre a lista e a cópia dela. */
  const getVolta = () =>
    (copia.current?.offsetLeft ?? 0) - (lista.current?.offsetLeft ?? 0);

  useEffect(() => {
    const caixa = faixa.current;
    if (!corre || oculto || !caixa) return;
    let quadro = 0;
    let antes = performance.now();
    // O `scrollLeft` pode arredondar para inteiro; a posição exata fica aqui.
    let posicao = caixa.scrollLeft;
    const passo = (agora: number) => {
      // O limite evita um salto ao voltar de uma aba em segundo plano.
      const decorrido = Math.min(agora - antes, 50);
      antes = agora;
      if (Date.now() < pausadoAte.current) {
        posicao = caixa.scrollLeft;
      } else {
        posicao += (VELOCIDADE * decorrido) / 1000;
        const volta = getVolta();
        if (volta > 0 && posicao > volta) posicao -= volta;
        caixa.scrollLeft = posicao;
      }
      quadro = requestAnimationFrame(passo);
    };
    quadro = requestAnimationFrame(passo);
    return () => cancelAnimationFrame(quadro);
  }, [corre, oculto]);

  const segurar = () => {
    pausadoAte.current = Infinity;
  };
  const soltar = () => {
    pausadoAte.current = Date.now() + PAUSA_MS;
  };

  // No arrasto à mão, a faixa dá a volta nas duas pontas, e a pausa dura enquanto ela se mexe.
  const aoRolar = () => {
    const caixa = faixa.current;
    if (!corre || !caixa) return;
    const volta = getVolta();
    if (volta <= 0) return;
    if (caixa.scrollLeft <= 0) caixa.scrollLeft += volta;
    else if (caixa.scrollLeft > volta) caixa.scrollLeft -= volta;
    const ate = pausadoAte.current;
    if (ate !== Infinity && Date.now() < ate) soltar();
  };

  const renderItens = (naCopia: boolean) =>
    placar.itens.map((item) => {
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
        plural(item.semanasCumpridas, "semana cumprida", "semanas cumpridas"),
        variacao > 0 && `subiu ${variacao} desde segunda`,
        variacao < 0 && `caiu ${-variacao} desde segunda`,
        sono && "dormindo",
      ]
        .filter(Boolean)
        .join(", ");
      return (
        <li
          key={item.usuarioId}
          ref={eu && !naCopia ? meu : undefined}
          className={corre ? undefined : "snap-start"}
        >
          <button
            type="button"
            aria-label={rotulo}
            tabIndex={naCopia ? -1 : undefined}
            className={`tap flex h-10 items-center gap-1 rounded-md pr-1.5 pl-1 ${eu ? "bg-game-soft text-on-game-soft" : "text-foreground"} ${sono ? "opacity-55" : ""}`}
            onClick={() => onToque(item.usuarioId)}
          >
            <span
              className={`badge-rank size-6 text-xs ${item.posicao === 1 ? "badge-rank-1" : ""}`}
            >
              {item.posicao}
            </span>
            <Avatar nome={item.nomeExibicao} id={item.usuarioId} size={24} />
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
    });

  // Marca onde a volta recomeça, entre o último e o 1º.
  const emenda = corre && (
    <li aria-hidden="true" className="self-center px-1 text-xs text-muted">
      ◆
    </li>
  );

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
        <div
          ref={faixa}
          className={`flex min-w-0 flex-1 gap-1 overflow-x-auto [scrollbar-width:none] ${corre ? "" : "snap-x"}`}
          onScroll={aoRolar}
          onTouchStart={segurar}
          onTouchEnd={soltar}
          onTouchCancel={soltar}
          onPointerEnter={(e) => {
            if (e.pointerType === "mouse") segurar();
          }}
          onPointerLeave={(e) => {
            if (e.pointerType === "mouse") pausadoAte.current = 0;
          }}
          onFocus={(e) => {
            // Só o foco do teclado: o foco de um toque não deve prender o letreiro.
            if (e.target.matches(":focus-visible")) segurar();
          }}
          onBlur={soltar}
        >
          <ol
            ref={lista}
            aria-label={
              placar.encerrado
                ? "Classificação final do desafio"
                : "Classificação do desafio"
            }
            className="flex shrink-0 gap-1"
          >
            {renderItens(false)}
            {emenda}
          </ol>
          {corre && (
            // A cópia fecha a volta: só para os olhos e para o toque.
            <ol ref={copia} aria-hidden="true" className="flex shrink-0 gap-1">
              {renderItens(true)}
              {emenda}
            </ol>
          )}
        </div>
      </div>
    </div>
  );
}
