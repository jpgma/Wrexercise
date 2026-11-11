import { Plus } from "lucide-react";
import { type ReactNode, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router";
import { CartaJogo } from "./CartaJogo";
import { primeiroNome } from "./textosCartas";
import { type CartaMao, useLoja } from "./useCartas";
import type { MembroCena } from "./useCena";

/** Arrastar a carta para cima além disto, ou rápido o bastante, arremessa. */
const ArrastoMinimo = 60;
const VelocidadeMinima = 0.5;
/** Abaixo disto o gesto ainda é um toque. */
const Folga = 8;

/**
 * Uma carta da Mão no Foco. Tocar seleciona; a selecionada sobe, balança e pode ser arrastada para
 * cima até sair da mão. O botão "Arremessar" faz o mesmo para quem não arrasta.
 */
function CartaDaMao({
  rotulo,
  selecionada,
  jogavel,
  selo,
  inclinacao,
  onSelecionar,
  onAcionar,
  children,
}: {
  rotulo: string;
  selecionada: boolean;
  jogavel: boolean;
  selo?: string;
  inclinacao: number;
  onSelecionar: () => void;
  onAcionar: () => void;
  children: ReactNode;
}) {
  const [arrasto, setArrasto] = useState(0);
  const [arrastando, setArrastando] = useState(false);
  const inicio = useRef<{ y: number; t: number } | null>(null);
  /** O gesto passou de um toque: o clique que vem junto não muda a seleção. */
  const ignorarClique = useRef(false);

  const soltar = (y: number, t: number) => {
    const comeco = inicio.current;
    inicio.current = null;
    setArrastando(false);
    setArrasto(0);
    if (!comeco) return;
    const dy = Math.min(0, y - comeco.y);
    if (-dy > Folga) ignorarClique.current = true;
    const velocidade = -dy / Math.max(1, t - comeco.t);
    if (
      -dy > ArrastoMinimo ||
      (-dy > Folga * 2 && velocidade > VelocidadeMinima)
    )
      onAcionar();
  };

  const subida = selecionada ? -22 + arrasto : Math.abs(inclinacao) * 1.5;

  return (
    <li
      className="relative max-w-16 min-w-0 flex-1"
      style={{
        transform: `translateY(${subida}px) rotate(${selecionada ? 0 : inclinacao}deg) scale(${selecionada ? 1.18 : 1})`,
        transition: arrastando
          ? "none"
          : "transform 220ms cubic-bezier(.34,1.56,.64,1)",
        zIndex: selecionada ? 2 : 1,
      }}
    >
      <button
        type="button"
        aria-pressed={selecionada}
        aria-label={rotulo}
        disabled={!jogavel}
        className={`block w-full rounded-md select-none focus-visible:ring-3 focus-visible:ring-game focus-visible:outline-none disabled:cursor-not-allowed ${selecionada ? "carta-balanca touch-none ring-3 ring-game" : "tap"}`}
        onClick={() => {
          if (ignorarClique.current) {
            ignorarClique.current = false;
            return;
          }
          onSelecionar();
        }}
        onPointerDown={(e) => {
          if (!selecionada) return;
          e.currentTarget.setPointerCapture(e.pointerId);
          inicio.current = { y: e.clientY, t: e.timeStamp };
          setArrastando(true);
        }}
        onPointerMove={(e) => {
          if (!inicio.current) return;
          setArrasto(Math.min(0, e.clientY - inicio.current.y));
        }}
        onPointerUp={(e) => soltar(e.clientY, e.timeStamp)}
        onPointerCancel={() => {
          inicio.current = null;
          setArrastando(false);
          setArrasto(0);
        }}
      >
        {children}
      </button>
      {selo && (
        <span className="chip pointer-events-none absolute inset-x-0 top-[3%] mx-auto w-fit px-1">
          {selo}
        </span>
      )}
    </li>
  );
}

/** A carta especial do Rival: só aparece quando o alvo pode ser escolhido agora. */
function CartaVs() {
  return (
    <div className="grid aspect-[3/4] w-full place-items-center rounded-md border-2 border-outline bg-game text-on-game shadow-sticker-sm">
      <span className="font-display text-2xl leading-none">VS</span>
      <span className="sr-only">Rival</span>
    </div>
  );
}

type Selecao = { tipo: "carta"; carta: CartaMao } | { tipo: "rival" };

/**
 * A Mão presa ao pé da tela durante o Foco. Só Provocações livres podem ser jogadas; as Respostas
 * e as cartas em jogo aparecem apagadas. Durante a encenação, a Mão desce e sai da frente.
 */
export function MaoFoco({
  membro,
  podeRival,
  recolhida,
  onArremessar,
  onRival,
}: {
  membro: MembroCena;
  podeRival: boolean;
  recolhida: boolean;
  onArremessar: (carta: CartaMao) => void;
  onRival: () => void;
}) {
  const loja = useLoja();
  const [selecao, setSelecao] = useState<Selecao | null>(null);
  const nome = primeiroNome(membro.nomeExibicao);
  const mao = loja.data?.mao ?? [];
  const limite = loja.data?.limiteMao ?? 0;
  const recebe = membro.recebeCartas;
  const total =
    mao.length + (podeRival ? 1 : 0) + (mao.length < limite ? 1 : 0);
  const inclinacaoDe = (i: number) => (i - (total - 1) / 2) * 4;

  const acionar = (s: Selecao) => {
    setSelecao(null);
    if (s.tipo === "rival") onRival();
    else onArremessar(s.carta);
  };

  const selecionada =
    selecao?.tipo === "carta"
      ? mao.find((c) => c.id === selecao.carta.id && !c.emJogo)
      : undefined;

  let instrucao: ReactNode;
  if (selecao?.tipo === "rival") {
    instrucao =
      "Vence quem cumprir a meta da semana primeiro e ganha 15 EN. A escolha vale até domingo.";
  } else if (selecionada) {
    instrucao = selecionada.efeito;
  } else if (!recebe) {
    instrucao = `${nome} não recebe cartas.`;
  } else if (mao.length === 0) {
    instrucao = "Sem cartas. Compre na Loja com o +.";
  } else {
    instrucao = "Toque numa carta e arraste para cima para arremessar.";
  }

  return createPortal(
    <div
      className={`fixed inset-x-0 bottom-0 z-50 border-t-2 border-outline bg-surface/95 px-4 pt-2 pb-[max(0.75rem,env(safe-area-inset-bottom))] backdrop-blur transition-transform duration-300 ${recolhida ? "pointer-events-none translate-y-full" : "animate-slide-up"}`}
    >
      <section aria-label="Sua Mão" className="mx-auto max-w-md">
        <div className="flex min-h-11 items-center gap-3">
          <p aria-live="polite" className="min-w-0 flex-1 text-xs text-muted">
            {instrucao}
          </p>
          {selecao && (
            <button
              type="button"
              className="btn-primary h-10 shrink-0 px-3 text-xs"
              onClick={() => acionar(selecao)}
            >
              {selecao.tipo === "rival" ? "Escolher Rival" : "Arremessar"}
            </button>
          )}
        </div>
        <ul className="flex items-end gap-1.5 pt-6 pb-1">
          {podeRival && (
            <CartaDaMao
              rotulo={`Escolher ${nome} como Rival da semana`}
              selecionada={selecao?.tipo === "rival"}
              jogavel
              inclinacao={inclinacaoDe(0)}
              onSelecionar={() =>
                setSelecao((s) =>
                  s?.tipo === "rival" ? null : { tipo: "rival" },
                )
              }
              onAcionar={() => acionar({ tipo: "rival" })}
            >
              <CartaVs />
            </CartaDaMao>
          )}
          {mao.map((carta, i) => {
            const jogavel =
              recebe && carta.tipo === "Provocacao" && !carta.emJogo;
            const marcada =
              selecao?.tipo === "carta" && selecao.carta.id === carta.id;
            return (
              <CartaDaMao
                key={carta.id}
                rotulo={`${carta.nome}${carta.emJogo ? ", em jogo" : ""}`}
                selecionada={marcada}
                jogavel={jogavel}
                selo={
                  carta.emJogo
                    ? "Em jogo"
                    : carta.tipo === "Resposta"
                      ? "Resposta"
                      : undefined
                }
                inclinacao={inclinacaoDe(i + (podeRival ? 1 : 0))}
                onSelecionar={() =>
                  setSelecao(marcada ? null : { tipo: "carta", carta })
                }
                onAcionar={() => acionar({ tipo: "carta", carta })}
              >
                <CartaJogo carta={carta} apagada={!jogavel} compacta />
              </CartaDaMao>
            );
          })}
          {loja.data && mao.length < limite && (
            <li className="max-w-16 min-w-0 flex-1">
              <Link
                to="/jogo/loja"
                aria-label="Comprar carta na Loja"
                className="grid aspect-[3/4] w-full place-items-center rounded-md border-2 border-dashed border-border-strong text-muted tap"
              >
                <Plus aria-hidden="true" className="size-6" />
              </Link>
            </li>
          )}
        </ul>
      </section>
    </div>,
    document.body,
  );
}
