import { Plus } from "lucide-react";
import { type ReactNode, useRef, useState } from "react";
import { Link } from "react-router";
import { CartaJogo, IconeCarta } from "./CartaJogo";
import { MesaJogadas } from "./MesaJogadas";
import {
  getBloqueioJogada,
  getLimiteEnvio,
  primeiroNome,
} from "./textosCartas";
import { type CartaMao, useJogadas, useLoja } from "./useCartas";
import type { MembroCena } from "./useCena";

/** Arrastar a carta para cima além disto, ou rápido o bastante, arremessa. */
const ArrastoMinimo = 60;
const VelocidadeMinima = 0.5;
/** Abaixo disto o gesto ainda é um toque. */
const Folga = 8;
/** Intervalo entre as cartas quando a Mão é distribuída. */
const DistribuicaoMs = 40;

/** A carta escolhida na Mão: uma Provocação ou, no Foco, a carta "VS" do Rival. */
export type SelecaoMao = { tipo: "carta"; carta: CartaMao } | { tipo: "rival" };

/**
 * Uma carta da Mão. Tocar seleciona; a selecionada sobe e balança. No Foco ela pode ser arrastada
 * para cima até sair da Mão. O botão "Arremessar" faz o mesmo para quem não arrasta.
 */
function CartaDaMao({
  rotulo,
  indice,
  selecionada,
  jogavel,
  arrastavel,
  selo,
  inclinacao,
  onSelecionar,
  onAcionar,
  onArrasto,
  children,
}: {
  rotulo: string;
  indice: number;
  selecionada: boolean;
  jogavel: boolean;
  arrastavel: boolean;
  selo?: string;
  inclinacao: number;
  onSelecionar: () => void;
  onAcionar: () => void;
  /** Quanto falta para soltar, de 0 a 1: a praça mira no alvo. */
  onArrasto: (fracao: number) => void;
  children: ReactNode;
}) {
  const [arrasto, setArrasto] = useState({ x: 0, y: 0 });
  const [arrastando, setArrastando] = useState(false);
  const inicio = useRef<{ x: number; y: number; t: number } | null>(null);
  /** O gesto passou de um toque: o clique que vem junto não muda a seleção. */
  const ignorarClique = useRef(false);

  const parar = () => {
    inicio.current = null;
    setArrastando(false);
    setArrasto({ x: 0, y: 0 });
    onArrasto(0);
  };

  const soltar = (y: number, t: number) => {
    const comeco = inicio.current;
    parar();
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

  const pronta = -arrasto.y > ArrastoMinimo;
  const subida = selecionada ? -22 + arrasto.y : Math.abs(inclinacao) * 1.5;
  // Arrastando, a carta acompanha o dedo de lado e se inclina para onde vai.
  const giro = selecionada
    ? Math.max(-8, Math.min(8, arrasto.x * 0.15))
    : inclinacao;
  const escala = selecionada ? (pronta ? 1.26 : 1.18) : 1;

  return (
    <li
      className="relative max-w-16 min-w-0 flex-1"
      style={{
        transform: `translate(${selecionada ? arrasto.x * 0.5 : 0}px, ${subida}px) rotate(${giro}deg) scale(${escala})`,
        transition: arrastando
          ? "none"
          : "transform 220ms cubic-bezier(.34,1.56,.64,1)",
        zIndex: selecionada ? 2 : 1,
      }}
    >
      <div
        className="carta-distribuir"
        style={{ animationDelay: `${indice * DistribuicaoMs}ms` }}
      >
        <button
          type="button"
          aria-pressed={selecionada}
          aria-label={rotulo}
          disabled={!jogavel}
          className={`block w-full rounded-md select-none focus-visible:ring-3 focus-visible:ring-game focus-visible:outline-none disabled:cursor-not-allowed ${selecionada ? `ring-3 ring-game ${arrastavel ? "touch-none" : ""} ${arrastando ? "" : "carta-balanca"}` : "tap"}`}
          onClick={() => {
            if (ignorarClique.current) {
              ignorarClique.current = false;
              return;
            }
            onSelecionar();
          }}
          onPointerDown={(e) => {
            if (!selecionada || !arrastavel) return;
            e.currentTarget.setPointerCapture(e.pointerId);
            inicio.current = { x: e.clientX, y: e.clientY, t: e.timeStamp };
            setArrastando(true);
          }}
          onPointerMove={(e) => {
            if (!inicio.current) return;
            const y = Math.min(0, e.clientY - inicio.current.y);
            setArrasto({ x: e.clientX - inicio.current.x, y });
            onArrasto(Math.min(1, -y / ArrastoMinimo));
          }}
          onPointerUp={(e) => soltar(e.clientY, e.timeStamp)}
          onPointerCancel={parar}
        >
          {children}
        </button>
      </div>
      {selo && (
        <span className="chip pointer-events-none absolute inset-x-0 top-[3%] mx-auto w-fit px-1">
          {selo}
        </span>
      )}
    </li>
  );
}

/** A carta especial do Rival: só aparece no Foco, quando o alvo pode ser escolhido agora. */
function CartaVs() {
  return (
    <div className="grid aspect-[3/4] w-full place-items-center rounded-md border-2 border-outline bg-game text-on-game shadow-sticker-sm">
      <span className="font-display text-2xl leading-none">VS</span>
      <span className="sr-only">Rival</span>
    </div>
  );
}

/**
 * A Mão de quem vê a praça, logo abaixo dela, com a Mesa embaixo (docs/gamificacao.md §3.5). Fora
 * do Foco, escolher uma Provocação faz os colegas que recebem cartas brilharem; no Foco, a carta
 * escolhida é arremessada no alvo. As cartas em jogo ficam na Mesa e deixam a vaga reservada.
 */
export function MaoJogo({
  grupoId,
  alvo,
  podeRival,
  recolhida,
  selecao,
  onSelecao,
  onArremessar,
  onRival,
  onArrasto,
}: {
  grupoId: string;
  /** O colega em Foco; `null` fora do Foco. */
  alvo: MembroCena | null;
  podeRival: boolean;
  /** Durante a encenação do Foco, a Mão some sem mudar de altura. */
  recolhida: boolean;
  selecao: SelecaoMao | null;
  onSelecao: (selecao: SelecaoMao | null) => void;
  onArremessar: (carta: CartaMao) => void;
  onRival: () => void;
  onArrasto: (fracao: number) => void;
}) {
  const loja = useLoja();
  const jogadas = useJogadas(grupoId);
  // A Mão remonta a cada Foco: o instante da abertura basta para o limite de 24 horas.
  const [agora] = useState(() => Date.now());
  const mao = loja.data?.mao ?? [];
  const limite = loja.data?.limiteMao ?? 0;
  const livres = mao.filter((c) => !c.emJogo);
  const reservadas = mao.filter((c) => c.emJogo);
  const comRival = alvo !== null && podeRival;
  const recebe = alvo?.recebeCartas ?? true;
  const nome = alvo ? primeiroNome(alvo.nomeExibicao) : "";
  // Fora do Foco, só o limite que vale para qualquer colega; no Foco, também os do alvo.
  const bloqueio = !alvo
    ? getLimiteEnvio(jogadas.data ?? [])
    : recebe
      ? getBloqueioJogada(jogadas.data ?? [], alvo.usuarioId, nome, agora)
      : null;
  const jogavel = (c: CartaMao) =>
    recebe && !bloqueio && c.tipo === "Provocacao";
  const total =
    livres.length +
    reservadas.length +
    (comRival ? 1 : 0) +
    (mao.length < limite ? 1 : 0);
  // Fora do Foco o leque é mais fechado: a Mão está guardada, não em jogo.
  const abertura = alvo ? 4 : 2;
  const inclinacaoDe = (i: number) => (i - (total - 1) / 2) * abertura;

  const acionar = (s: SelecaoMao) => {
    onSelecao(null);
    onArrasto(0);
    if (s.tipo === "rival") onRival();
    else onArremessar(s.carta);
  };

  const escolhida =
    selecao?.tipo === "carta"
      ? livres.find((c) => c.id === selecao.carta.id && jogavel(c))
      : undefined;
  const rivalMarcado = comRival && selecao?.tipo === "rival";
  const pronta = alvo ? (rivalMarcado ? selecao : escolhida && selecao) : null;

  let instrucao: ReactNode;
  if (rivalMarcado) {
    instrucao =
      "Vence quem cumprir a meta da semana primeiro e ganha 15 EN. A escolha vale até domingo.";
  } else if (escolhida) {
    instrucao = alvo
      ? escolhida.efeito
      : `${escolhida.efeito} Toque num colega para arremessar.`;
  } else if (!recebe) {
    instrucao = `${nome} não recebe cartas.`;
  } else if (bloqueio) {
    instrucao = bloqueio;
  } else if (livres.length === 0) {
    instrucao =
      mao.length === 0
        ? "Sem cartas. Compre na Loja com o +."
        : mao.length < limite
          ? "Todas as suas cartas estão na mesa. Compre mais na Loja com o +."
          : "Todas as suas cartas estão na mesa.";
  } else if (alvo) {
    instrucao = "Toque numa carta e arraste para cima para arremessar.";
  } else {
    instrucao = "Escolha uma carta e toque num colega na praça.";
  }

  let indice = 0;

  return (
    <section
      aria-labelledby="mao-titulo"
      className={`card mt-2 p-3 transition-[opacity,translate] duration-300 ${recolhida ? "pointer-events-none translate-y-2 opacity-0" : alvo ? "animate-slide-up" : ""}`}
    >
      <div className="flex min-h-11 items-center gap-3">
        <h2 id="mao-titulo" className="text-sm font-semibold">
          Sua Mão{" "}
          {loja.data && (
            <span className="font-score text-base font-normal text-muted">
              {mao.length}/{limite}
            </span>
          )}
        </h2>
        <div className="flex-1" />
        {pronta && (
          <button
            type="button"
            className="btn-primary h-10 shrink-0 px-3 text-xs"
            onClick={() => acionar(pronta)}
          >
            {pronta.tipo === "rival" ? "Escolher Rival" : "Arremessar"}
          </button>
        )}
      </div>
      <p aria-live="polite" className="min-h-8 text-xs text-muted">
        {instrucao}
      </p>
      {/* O alto reserva a subida e o aumento da carta escolhida: ela não cobre a instrução. */}
      <ul className="flex items-end gap-1.5 pt-10 pb-1">
        {comRival && (
          <CartaDaMao
            rotulo={`Escolher ${nome} como Rival da semana`}
            indice={indice}
            selecionada={rivalMarcado}
            jogavel
            arrastavel
            inclinacao={inclinacaoDe(indice++)}
            onSelecionar={() =>
              onSelecao(rivalMarcado ? null : { tipo: "rival" })
            }
            onAcionar={() => acionar({ tipo: "rival" })}
            onArrasto={onArrasto}
          >
            <CartaVs />
          </CartaDaMao>
        )}
        {livres.map((carta) => {
          const marcada = escolhida?.id === carta.id;
          const i = indice++;
          return (
            <CartaDaMao
              key={carta.id}
              rotulo={carta.nome}
              indice={i}
              selecionada={marcada}
              jogavel={jogavel(carta)}
              arrastavel={alvo !== null}
              selo={carta.tipo === "Resposta" ? "Resposta" : undefined}
              inclinacao={inclinacaoDe(i)}
              onSelecionar={() =>
                onSelecao(marcada ? null : { tipo: "carta", carta })
              }
              onAcionar={() => acionar({ tipo: "carta", carta })}
              onArrasto={onArrasto}
            >
              <CartaJogo carta={carta} apagada={!jogavel(carta)} compacta />
            </CartaDaMao>
          );
        })}
        {reservadas.map((carta) => {
          const i = indice++;
          return (
            <li
              key={carta.id}
              className="max-w-16 min-w-0 flex-1"
              style={{
                transform: `translateY(${Math.abs(inclinacaoDe(i)) * 1.5}px) rotate(${inclinacaoDe(i)}deg)`,
              }}
            >
              <div
                role="img"
                aria-label={`${carta.nome} está na mesa`}
                className="carta-distribuir grid aspect-[3/4] w-full place-items-center rounded-md border-2 border-dashed border-border-strong opacity-60"
                style={{ animationDelay: `${i * DistribuicaoMs}ms` }}
              >
                <IconeCarta codigo={carta.codigo} />
              </div>
            </li>
          );
        })}
        {loja.data && mao.length < limite && (
          <li className="max-w-16 min-w-0 flex-1">
            <Link
              to="/jogo/loja"
              aria-label="Comprar carta na Loja"
              className="carta-distribuir grid aspect-[3/4] w-full place-items-center rounded-md border-2 border-dashed border-border-strong text-muted tap"
              style={{ animationDelay: `${indice * DistribuicaoMs}ms` }}
            >
              <Plus aria-hidden="true" className="size-6" />
            </Link>
          </li>
        )}
      </ul>
      <MesaJogadas grupoId={grupoId} filtro={alvo?.usuarioId} />
    </section>
  );
}
