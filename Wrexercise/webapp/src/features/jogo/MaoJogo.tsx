import { Plus } from "lucide-react";
import { type CSSProperties, type ReactNode, useRef, useState } from "react";
import { Link } from "react-router";
import { CartaJogo } from "./CartaJogo";
import { ComoJogar } from "./ComoJogar";
import { MesaJogadas } from "./MesaJogadas";
import { getProximoPasso } from "./proximoPasso";
import { FadigaExplicada, SaldoEnergia } from "./SaldoEnergia";
import { useAvisoFadiga } from "./useEnergia";
import {
  getBloqueioJogada,
  getEfeitoComNome,
  getEfeitoFusao,
  getLimiteEnvio,
  getPrevia,
  podeFundir,
  primeiroNome,
} from "./textosCartas";
import {
  type CartaMao,
  useJogadas,
  useLoja,
  usePreviaJogada,
} from "./useCartas";
import type { MembroCena } from "./useCena";

/** Arrastar a carta para cima além disto, ou rápido o bastante, arremessa. */
const ArrastoMinimo = 60;
const VelocidadeMinima = 0.5;
/** Abaixo disto o gesto ainda é um toque. */
const Folga = 8;
/** Intervalo entre as cartas quando a Mão é distribuída. */
const DistribuicaoMs = 40;

/** Metade da distância entre os centros das duas cartas de uma Fusão, já juntas. */
const MeioDoPar = 20;

/**
 * A carta escolhida na Mão: uma Provocação ou, no Foco, a carta "VS" do Rival. No Foco, uma segunda
 * Provocação pode se fundir à escolhida (`fusao`, decisão 0070).
 */
export type SelecaoMao =
  { tipo: "carta"; carta: CartaMao; fusao?: CartaMao } | { tipo: "rival" };

/** Como uma carta entra numa Fusão montada na Mão. */
type FusaoNaMao = {
  /** 1: a outra carta está à direita; -1: à esquerda. */
  lado: 1 | -1;
  /** Quanto a carta anda para encontrar a outra, em px. */
  dx: number;
  /** A carta escolhida primeiro: leva as faíscas e o selo do par. */
  principal: boolean;
};

/**
 * Uma carta da Mão. Tocar seleciona; a selecionada sobe e balança. No Foco ela pode ser arrastada
 * para cima até sair da Mão. O botão "Arremessar" faz o mesmo para quem não arrasta. Numa Fusão, as
 * duas cartas recuam, batem uma na outra com faíscas e ficam juntas, brilhando.
 */
function CartaDaMao({
  rotulo,
  indice,
  selecionada,
  jogavel,
  arrastavel,
  selo,
  fusao,
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
  fusao?: FusaoNaMao;
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
  // A escolhida sobe por cima da linha do título: a Mão não reserva altura para ela.
  const subida = selecionada ? -12 + arrasto.y : Math.abs(inclinacao) * 1.5;
  // Arrastando, a carta acompanha o dedo de lado e se inclina para onde vai.
  // Na Fusão parada, as duas cartas abrem em "V", cada uma para o seu lado.
  const giro = selecionada
    ? fusao && !arrastando
      ? fusao.lado * -6
      : Math.max(-8, Math.min(8, arrasto.x * 0.15))
    : inclinacao;
  const escala = selecionada ? (pronta ? 1.18 : 1.1) : 1;
  const desvio = (fusao?.dx ?? 0) + (selecionada ? arrasto.x * 0.5 : 0);

  return (
    <li
      className="relative max-w-16 min-w-0 flex-1"
      style={{
        transform: `translate(${desvio}px, ${subida}px) rotate(${giro}deg) scale(${escala})`,
        transition: arrastando
          ? "none"
          : "transform 220ms cubic-bezier(.34,1.56,.64,1)",
        zIndex: fusao?.principal ? 3 : selecionada ? 2 : 1,
      }}
    >
      <div
        className="carta-distribuir"
        style={{ animationDelay: `${indice * DistribuicaoMs}ms` }}
      >
        <div
          className={fusao ? "fusao-colisao" : undefined}
          style={fusao && ({ "--lado": fusao.lado } as CSSProperties)}
        >
          <button
            type="button"
            aria-pressed={selecionada}
            aria-label={rotulo}
            disabled={!jogavel}
            className={`block w-full rounded-md select-none focus-visible:ring-3 focus-visible:ring-game focus-visible:outline-none disabled:cursor-not-allowed ${selecionada ? `ring-3 ring-game ${arrastavel ? "touch-none" : ""} ${arrastando ? "" : fusao ? "fusao-pulso" : "carta-balanca"}` : "tap"}`}
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
      </div>
      {selo && (
        <span className="chip pointer-events-none absolute inset-x-0 top-[3%] mx-auto w-fit px-1">
          {selo}
        </span>
      )}
      {fusao?.principal && !arrastando && (
        // As faíscas e o selo ficam no meio do par, que está a MeioDoPar do centro desta carta.
        <>
          <span
            aria-hidden="true"
            className="fusao-faiscas pointer-events-none absolute top-1/2 -mt-8"
            style={{
              left: `calc(50% + ${fusao.lado * MeioDoPar - 32}px)`,
            }}
          />
          <span
            aria-hidden="true"
            className="fusao-selo chip chip-game pointer-events-none absolute -top-2 left-1/2 -translate-x-1/2 px-1 uppercase"
            style={{ marginLeft: fusao.lado * MeioDoPar }}
          >
            Fusão
          </span>
        </>
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
 * escolhida é arremessada no alvo. A carta jogada sai da Mão e não volta (decisão 0074): a Jogada fica na Mesa.
 * Compacta para caber com a praça numa tela de 390×844: o texto de contexto toma o lugar do título
 * só quando há o que dizer, e a carta escolhida sobe por cima dele.
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
  /** Com `fusao`, as duas Provocações vão na mesma Jogada. */
  onArremessar: (carta: CartaMao, fusao?: CartaMao) => void;
  onRival: () => void;
  onArrasto: (fracao: number) => void;
}) {
  const loja = useLoja();
  const jogadas = useJogadas(grupoId);
  useAvisoFadiga();
  // A Mão remonta a cada Foco: o instante da abertura basta para o limite de 24 horas.
  const [agora] = useState(() => Date.now());
  const mao = loja.data?.mao ?? [];
  const limite = loja.data?.limiteMao ?? 0;
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
  const total = mao.length + (comRival ? 1 : 0) + (mao.length < limite ? 1 : 0);
  // Fora do Foco o leque é mais fechado: a Mão está guardada, não em jogo.
  const abertura = alvo ? 4 : 2;
  const inclinacaoDe = (i: number) => (i - (total - 1) / 2) * abertura;

  const escolhida =
    selecao?.tipo === "carta"
      ? mao.find((c) => c.id === selecao.carta.id && jogavel(c))
      : undefined;
  // A Fusão só existe no Foco: fora dele, a escolha é de uma carta só.
  const idFusao = selecao?.tipo === "carta" ? selecao.fusao?.id : undefined;
  const fundida =
    alvo && escolhida && idFusao
      ? mao.find(
          (c) => c.id === idFusao && jogavel(c) && podeFundir(escolhida, c),
        )
      : undefined;
  const rivalMarcado = comRival && selecao?.tipo === "rival";
  // No Foco, com a carta escolhida: o que ela vai pedir a este colega, já com o que o servidor fixaria.
  const previa = usePreviaJogada(
    grupoId,
    escolhida ? alvo?.usuarioId : undefined,
    escolhida?.id,
    fundida?.id,
  );
  const pronta: SelecaoMao | null = !alvo
    ? null
    : rivalMarcado
      ? { tipo: "rival" }
      : escolhida
        ? { tipo: "carta", carta: escolhida, fusao: fundida }
        : null;

  const acionar = (s: SelecaoMao) => {
    onSelecao(null);
    onArrasto(0);
    if (s.tipo === "rival") onRival();
    else onArremessar(s.carta, s.fusao);
  };

  /** A distância entre duas vagas vizinhas da Mão, medida ao montar a Fusão. */
  const lista = useRef<HTMLUListElement>(null);
  const [passo, setPasso] = useState(66);
  const medirPasso = () => {
    const [a, b] = lista.current?.children ?? [];
    if (a instanceof HTMLElement && b instanceof HTMLElement)
      setPasso(b.offsetLeft - a.offsetLeft);
  };

  /**
   * Tocar numa carta livre. No Foco, com uma Provocação já escolhida, outra Provocação diferente se
   * funde a ela; tocar numa das duas a tira do par.
   */
  const selecionar = (carta: CartaMao) => {
    if (carta.id === escolhida?.id)
      return onSelecao(fundida ? { tipo: "carta", carta: fundida } : null);
    if (carta.id === fundida?.id && escolhida)
      return onSelecao({ tipo: "carta", carta: escolhida });
    if (alvo && escolhida && podeFundir(escolhida, carta)) {
      medirPasso();
      return onSelecao({ tipo: "carta", carta: escolhida, fusao: carta });
    }
    onSelecao({ tipo: "carta", carta });
  };

  // Sem nada a dizer, a linha mostra o título; com algo, o texto toma o lugar dele.
  let contexto: string | null = null;
  if (rivalMarcado) {
    contexto =
      "Vence quem cumprir a meta da semana primeiro e ganha 15 EN. Vale até domingo.";
  } else if (escolhida && previa.data) {
    contexto = getPrevia(nome, escolhida, fundida, previa.data);
  } else if (escolhida && fundida) {
    contexto = `${getEfeitoFusao(escolhida, fundida)} As cartas não voltam; sem cumprir, metade do preço volta.`;
  } else if (escolhida) {
    contexto = !alvo
      ? `${getEfeitoComNome(escolhida)} Toque num colega.`
      : `${getEfeitoComNome(escolhida)} A carta não volta; sem cumprir, metade do preço volta.`;
  } else if (!recebe) {
    contexto = `${nome} não recebe cartas.`;
  } else if (bloqueio && mao.length > 0) {
    contexto = bloqueio;
  }
  // Fora do Foco e sem nada escolhido, a linha sugere o próximo passo; a contagem vira um selo.
  const proximoPasso =
    contexto === null && !alvo && loja.data && jogadas.data
      ? getProximoPasso(jogadas.data, loja.data)
      : null;
  contexto ??= proximoPasso;

  let indice = 0;

  return (
    <section
      aria-labelledby="mao-titulo"
      className={`card mt-2 p-3 transition-[opacity,translate] duration-300 ${recolhida ? "pointer-events-none translate-y-2 opacity-0" : alvo ? "animate-slide-up" : ""}`}
    >
      <div className="flex min-h-11 items-center gap-2">
        <h2
          id="mao-titulo"
          className={
            contexto ? "sr-only" : "min-w-0 flex-1 text-sm font-semibold"
          }
        >
          Sua Mão{" "}
          {loja.data && (
            <span className="font-score text-base font-normal text-muted">
              {mao.length}/{limite}
            </span>
          )}
        </h2>
        <p
          aria-live="polite"
          className={
            contexto
              ? "line-clamp-3 min-w-0 flex-1 text-xs text-muted"
              : "sr-only"
          }
        >
          {contexto}
        </p>
        {pronta ? (
          <button
            type="button"
            className="btn-primary h-10 shrink-0 px-3 text-xs"
            onClick={() => acionar(pronta)}
          >
            {pronta.tipo === "rival"
              ? "Escolher Rival"
              : pronta.fusao
                ? "Jogar fusão"
                : "Arremessar"}
          </button>
        ) : (
          <>
            {proximoPasso && (
              <span className="chip font-score font-normal">
                <span className="sr-only">Cartas na Mão: </span>
                {mao.length}/{limite}
              </span>
            )}
            <ComoJogar />
            <FadigaExplicada />
            <Link
              to="/jogo/loja"
              aria-label="Energia e Loja"
              className="shrink-0 tap"
            >
              <SaldoEnergia />
            </Link>
          </>
        )}
      </div>
      <ul ref={lista} className="flex items-end gap-1.5 pt-3 pb-1">
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
        {mao.map((carta, posicao) => {
          const principal = escolhida?.id === carta.id;
          const segunda = fundida?.id === carta.id;
          const marcada = principal || segunda;
          const i = indice++;
          // Na Fusão, cada carta anda metade do caminho até a outra e para a MeioDoPar do meio.
          let fusao: FusaoNaMao | undefined;
          if (escolhida && fundida && marcada) {
            const outra = mao.indexOf(principal ? fundida : escolhida);
            const lado = outra > posicao ? 1 : -1;
            fusao = {
              lado,
              dx: lado * ((Math.abs(outra - posicao) * passo) / 2 - MeioDoPar),
              principal,
            };
          }
          const funde =
            alvo !== null &&
            !marcada &&
            !fundida &&
            escolhida !== undefined &&
            jogavel(carta) &&
            podeFundir(escolhida, carta);
          return (
            <CartaDaMao
              key={carta.id}
              rotulo={
                segunda && escolhida
                  ? `${carta.nome}, fundida com ${escolhida.nome}`
                  : funde && escolhida
                    ? `${carta.nome}: fundir com ${escolhida.nome}`
                    : carta.nome
              }
              indice={i}
              selecionada={marcada}
              jogavel={jogavel(carta)}
              arrastavel={alvo !== null}
              selo={
                carta.tipo === "Resposta"
                  ? "Resposta"
                  : funde
                    ? "Fundir"
                    : undefined
              }
              fusao={fusao}
              inclinacao={inclinacaoDe(i)}
              onSelecionar={() => selecionar(carta)}
              onAcionar={() =>
                acionar(
                  escolhida && marcada
                    ? { tipo: "carta", carta: escolhida, fusao: fundida }
                    : { tipo: "carta", carta },
                )
              }
              onArrasto={onArrasto}
            >
              <CartaJogo carta={carta} apagada={!jogavel(carta)} compacta />
            </CartaDaMao>
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
