import { Swords } from "lucide-react";
import {
  type CSSProperties,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { Link } from "react-router";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import {
  formatoFolha,
  hashFnv1a,
  useFolhaAvatar,
  useVitrine,
} from "@/components/folhaAvatar";
import { getDirecao, variacoesDeParado, type NomeAnimacao } from "./animacoes";
import { IconeCarta } from "./CartaJogo";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { MaoJogo, type SelecaoMao } from "./MaoJogo";
import { MenuAvatar } from "./MenuAvatar";
import { ObjetosPraca, vaoDaPorta } from "./ObjetosPraca";
import { SaldoEnergia } from "./SaldoEnergia";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMarcarEnfeitesVistos, useMeuAvatar } from "./useAvatares";
import { PainelFoco } from "./PainelFoco";
import { primeiroNome } from "./textosCartas";
import {
  type CartaMao,
  type CodigoCarta,
  useJogadas,
  useJogarCarta,
  useMarcarJogadasVistas,
} from "./useCartas";
import { useCena, type Cena, type MembroCena } from "./useCena";
import {
  type Atuacao,
  type CartaEmCena,
  type RivalEmCena,
  useEncenacoes,
} from "./useEncenacoes";
import { useEntrada } from "./useEntrada";
import { type EfeitoFoco, useFoco } from "./useFoco";
import { useMovimentoReduzido } from "./useMovimentoReduzido";
import { useVidaNaPraca } from "./useVidaNaPraca";
import { usePeriodoDoDia } from "./periodoDoDia";
import { Bola, LuzDaPraca, Pombo } from "./VidaNaPraca";
import {
  getRelacaoRival,
  getVencedorId,
  type RelacaoRival,
  type Rival,
  useEscolherRival,
  useMarcarRivaisVistos,
  useRivais,
} from "./useRivais";

// A praça tem 180×200 px, vista em ¾, e é mostrada em 2×.
const semNovos: string[] = [];
const semRivais: RivalEmCena[] = [];
const largura = 360;
const altura = 400;
const centroX = largura / 2;
const { ancora, corpo } = formatoFolha;
/** Altura do corpo acima dos pés, em px da folha. */
const alturaCorpo = ancora.y - corpo.topo;
/** O quadro aparece sempre em 2×, qualquer que seja o tamanho do grupo. */
const escala = 2;

/**
 * Elipse larga e baixa, no centro do chão, sobre o oval pintado na praça. Com 10 membros ela tem o
 * tamanho máximo; com menos, encolhe e mantém a distância entre vizinhos. O raio vertical mínimo
 * deixa quem fica atrás visível acima de quem fica na frente.
 */
const elipse = {
  centroY: 288,
  raioX: { max: 148, min: 60 },
  raioY: { max: 76, min: 48 },
};
const GrupoCheio = 10;
/** Centro da praça: para onde olha quem está parado. */
const centro: Posicao = { x: centroX, y: elipse.centroY };
const medalhas: Record<number, string> = { 1: "🥇", 2: "🥈", 3: "🥉" };
/** Por quanto tempo o nome aparece depois de um toque no avatar. */
const NomeMs = 3000;

function mulberry32(semente: number): () => number {
  let a = semente;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

type Posicao = { x: number; y: number };

/**
 * O que ainda não foi visto do Rival: a provocação, para quem foi escolhido; o resultado, para quem
 * escolheu. A provocação de quem acabou de escolher toca no Foco e não tem o que registrar.
 */
function getRivaisEmCena(rivais: Rival[] | undefined): RivalEmCena[] {
  return (rivais ?? [])
    .filter((r) => r.nova)
    .map((r) => ({
      id: r.id,
      escolhedorId: r.escolhedorId,
      rivalId: r.rivalId,
      efeito: r.escolhido ? "resultado" : "provocacao",
      vencedorId: r.escolhido ? getVencedorId(r) : null,
    }));
}

/** Quem vê a tela fica na frente (embaixo); os demais seguem a ordem de entrada ao redor. */
function posicionar(membros: MembroCena[], usuarioId: string): Posicao[] {
  const n = membros.length;
  const { centroY } = elipse;
  if (n === 1) return [{ x: centroX, y: centroY }];
  const fracao = Math.min(n, GrupoCheio) / GrupoCheio;
  const raioX = Math.max(elipse.raioX.min, elipse.raioX.max * fracao);
  const raioY = Math.max(elipse.raioY.min, elipse.raioY.max * fracao);
  const outros = membros.filter((m) => m.usuarioId !== usuarioId);
  const ordenados = [
    ...membros.filter((m) => m.usuarioId === usuarioId),
    ...outros,
  ];
  const posicoes = new Map<string, Posicao>();
  ordenados.forEach((m, i) => {
    const angulo = Math.PI / 2 + (i * 2 * Math.PI) / n;
    posicoes.set(m.usuarioId, {
      x: centroX + raioX * Math.cos(angulo),
      y: centroY + raioY * Math.sin(angulo),
    });
  });
  return membros.map(
    (m) => posicoes.get(m.usuarioId) ?? { x: centroX, y: centroY },
  );
}

function AvatarCena({
  membro,
  posicao,
  reduzido,
  atuacao,
  brilho,
  nomeVisivel,
  posicaoClassificacao,
  proprio,
  apagado,
  mira,
  rival,
  onToque,
}: {
  membro: MembroCena;
  posicao: Posicao;
  reduzido: boolean;
  atuacao: Atuacao | undefined;
  /** O Enfeite do avatar brilha: é o Enfeite novo de quem vê a cena. */
  brilho: boolean;
  /** O nome só aparece depois de um toque e durante as encenações. */
  nomeVisivel: boolean;
  /** 1, 2 ou 3 no desafio em destaque: a medalha aparece sempre, com ou sem o nome. */
  posicaoClassificacao: number | undefined;
  /** O avatar de quem vê a cena: o toque abre o menu, e não o Foco. */
  proprio: boolean;
  /** Durante o Foco em outra pessoa, quem não atua fica apagado. */
  apagado: boolean;
  /** Anel de mira sob os pés, de 0 a 1: alvo de uma carta escolhida ou arrastada. */
  mira: number | null;
  /** O par do Rival desta semana com quem vê a cena. */
  rival: RelacaoRival | null;
  onToque: () => void;
}) {
  const folha = useFolhaAvatar(membro.usuarioId);
  const enfeite = useVitrine(membro.usuarioId)[0];
  const [variacao, setVariacao] = useState<NomeAnimacao | null>(null);
  /** Conta os toques: cada um recomeça o pulinho. */
  const [pulos, setPulos] = useState(0);
  const parado = membro.pose === "parado";

  const sorteio = useMemo(
    () => mulberry32(hashFnv1a(membro.usuarioId)),
    [membro.usuarioId],
  );

  // Quem está parado de vez em quando se alonga ou olha em volta, fora de sincronia com os outros.
  useEffect(() => {
    if (!parado || reduzido || variacoesDeParado.length === 0) return;
    let temporizador: number;
    const agendar = () => {
      temporizador = window.setTimeout(
        () => {
          if (document.visibilityState === "visible") {
            setVariacao(
              variacoesDeParado[
                Math.floor(sorteio() * variacoesDeParado.length)
              ] ?? null,
            );
          } else {
            agendar();
          }
        },
        6000 + sorteio() * 8000,
      );
    };
    agendar();
    return () => window.clearTimeout(temporizador);
  }, [parado, reduzido, sorteio, variacao]);

  const animacao: NomeAnimacao =
    atuacao?.animacao ?? (parado ? (variacao ?? "parado") : membro.pose);
  const direcao = atuacao?.direcao ?? getDirecao(posicao, centro);
  const deslocamento = {
    transform: atuacao
      ? `translate(${atuacao.dx}px, ${atuacao.dy}px)`
      : undefined,
    transition: `transform ${atuacao?.passoMs ?? 0}ms linear, opacity 300ms ease-out`,
    opacity: apagado ? 0.45 : undefined,
    // Atrás da porta, antes de sair na entrada do dia.
    visibility: atuacao?.oculto ? "hidden" : undefined,
  } as const;
  const primeiroNome = membro.nomeExibicao.trim().split(/\s+/)[0];
  const medalha = posicaoClassificacao
    ? medalhas[posicaoClassificacao]
    : undefined;
  const rotulo = [
    membro.nomeExibicao,
    medalha && `${posicaoClassificacao}º lugar no desafio`,
    rival === "escolhido" && "seu Rival da semana",
    rival === "escolheu" && "escolheu você como Rival",
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <>
      <button
        type="button"
        aria-label={rotulo}
        aria-haspopup={proprio ? "dialog" : undefined}
        onClick={() => {
          if (!reduzido) setPulos((n) => n + 1);
          onToque();
        }}
        className="absolute block cursor-pointer rounded-md focus-visible:ring-3 focus-visible:ring-game focus-visible:outline-none"
        style={{
          // A área de toque é só o corpo; a célula da folha (48 px) passa dela sem receber toques.
          left: posicao.x - (ancora.x - corpo.esquerda) * escala,
          top: posicao.y - alturaCorpo * escala,
          width: corpo.largura * escala,
          height: (alturaCorpo + 1) * escala,
          zIndex: Math.round(posicao.y + (atuacao?.dy ?? 0)),
          ...deslocamento,
        }}
      >
        {mira !== null && (
          <span
            aria-hidden="true"
            className="mira-alvo pointer-events-none absolute rounded-[50%] border-2 border-game"
            style={{
              left: (ancora.x - corpo.esquerda) * escala - 18,
              top: alturaCorpo * escala - 7,
              width: 36,
              height: 14,
              opacity: 0.5 + mira * 0.5,
              transform: `scale(${1 + mira * 0.35})`,
            }}
          />
        )}
        {/* A sombra fica no chão, sob os pés: não acompanha o pulinho. */}
        <span
          aria-hidden="true"
          className="sombra-cena pointer-events-none absolute"
          style={{
            left: (ancora.x - corpo.esquerda) * escala - 11,
            top: alturaCorpo * escala - 3,
            width: 22,
            height: 6,
          }}
        />
        <span
          key={pulos}
          aria-hidden="true"
          className={`pointer-events-none absolute inset-0 ${pulos > 0 ? "avatar-pulo" : ""}`}
        >
          <SpriteAvatar
            key={`${animacao}-${direcao}`}
            folha={folha}
            animacao={animacao}
            direcao={direcao}
            escala={escala}
            estatico={reduzido || atuacao?.quadro !== undefined}
            quadro={atuacao?.quadro}
            className="pointer-events-none absolute"
            style={{
              left: -corpo.esquerda * escala,
              top: -corpo.topo * escala,
            }}
            onFim={() => setVariacao(null)}
          />
        </span>
      </button>
      {enfeite && (
        <EnfeiteIcone
          conquista={enfeite.conquista}
          tamanho={16}
          brilho={brilho}
          className="pointer-events-none absolute block"
          style={{
            left: posicao.x + 9 * escala,
            top: posicao.y - 16 - 2,
            zIndex: Math.round(posicao.y + (atuacao?.dy ?? 0)) + 1,
            ...deslocamento,
          }}
        />
      )}
      {/* Os nomes ficam numa camada acima de todos os avatares: ninguém esconde o nome de outro. */}
      {(nomeVisivel || medalha || rival) && (
        <span
          aria-hidden="true"
          className="pointer-events-none absolute -translate-x-1/2 rounded bg-surface/85 px-1 text-2xs leading-tight font-semibold whitespace-nowrap text-foreground"
          style={{
            left: posicao.x,
            top: posicao.y + 3,
            zIndex: 1000,
            ...deslocamento,
          }}
        >
          {medalha && <span className="text-sm leading-none">{medalha}</span>}
          {rival && (
            <Swords
              className={`inline size-3 align-[-2px] ${rival === "escolhido" ? "rounded-sm bg-game p-px text-on-game" : "text-game"}`}
            />
          )}
          {(medalha || rival) && nomeVisivel && " "}
          {nomeVisivel && primeiroNome}
        </span>
      )}
    </>
  );
}

/** Em telas mais estreitas que a praça, ela encolhe inteira em vez de ser cortada. */
function useFatorDeEncaixe() {
  // Ref em estado: a praça só existe depois que a cena carrega, e o efeito precisa esperar por ela.
  const [elemento, setElemento] = useState<HTMLDivElement | null>(null);
  const [fator, setFator] = useState(1);

  useEffect(() => {
    if (!elemento) return;
    const medir = () =>
      setFator(Math.min(1, elemento.clientWidth / largura) || 1);
    medir();
    const observador = new ResizeObserver(medir);
    observador.observe(elemento);
    return () => observador.disconnect();
  }, [elemento]);

  return { ref: setElemento, fator };
}

/** O pódio do desafio em destaque: até 3 pessoas, cada uma com a sua posição. */
export type PodioCena = { usuarioId: string; posicao: number }[];

export function CenaGrupo({
  grupoId,
  usuarioId,
  podio = [],
}: {
  grupoId: string;
  usuarioId: string;
  podio?: PodioCena;
}) {
  const cena = useCena(grupoId);
  const reduzido = useMovimentoReduzido();

  if (cena.isError) return null;
  if (!cena.data) {
    return (
      <div
        role="status"
        className="mx-auto grid w-full place-items-center rounded-xl bg-surface-muted text-sm text-muted"
        style={{ maxWidth: largura, height: altura }}
      >
        Carregando a praça…
      </div>
    );
  }

  return (
    <Praca
      cena={cena.data}
      grupoId={grupoId}
      usuarioId={usuarioId}
      reduzido={reduzido}
      podio={podio}
    />
  );
}

/** A carta da encenação: voa até quem recebe, cai no lugar ao ser aceita ou sai pelo lado. */
function CartaVoando({
  carta,
}: {
  carta: Pick<CartaEmCena, "efeito" | "de" | "para"> & { codigo: CodigoCarta };
}) {
  // A carta (40 px) fica na altura do peito, centrada no avatar.
  const acima = 13 * escala + 20;
  return (
    <span
      aria-hidden="true"
      className={`carta-encenacao carta-${carta.efeito} pointer-events-none absolute grid size-10 place-items-center rounded-md border-2 border-carta-tinta bg-carta-giz`}
      style={
        {
          left: 0,
          top: 0,
          zIndex: 1001,
          "--de-x": `${carta.de.x - 20}px`,
          "--de-y": `${carta.de.y - acima}px`,
          "--para-x": `${carta.para.x - 20}px`,
          "--para-y": `${carta.para.y - acima}px`,
        } as CSSProperties
      }
    >
      <IconeCarta codigo={carta.codigo} />
    </span>
  );
}

/** O nome da carta estoura sobre quem recebe. O "VS" do Rival fica fora da câmera, no alto da praça. */
function ImpactoNoFoco({ efeito }: { efeito: EfeitoFoco }) {
  if (efeito.tipo !== "impacto") return null;
  return (
    <span
      aria-hidden="true"
      className="foco-impacto pointer-events-none absolute rounded-md border-2 border-outline bg-reward px-1.5 py-0.5 font-display text-sm leading-none whitespace-nowrap text-on-reward uppercase shadow-sticker-sm"
      style={{
        left: efeito.ponto.x,
        top: efeito.ponto.y - alturaCorpo * escala - 24,
        zIndex: 1001,
      }}
    >
      {efeito.texto}!
    </span>
  );
}

function Praca({
  cena,
  grupoId,
  usuarioId,
  reduzido,
  podio,
}: {
  cena: Cena;
  grupoId: string;
  usuarioId: string;
  reduzido: boolean;
  podio: PodioCena;
}) {
  const { ref, fator } = useFatorDeEncaixe();
  const { showToast } = useToast();
  const [tocado, setTocado] = useState<MembroCena | null>(null);
  const [nomeTocado, setNomeTocado] = useState<string | null>(null);
  // O menu cobre a praça: o tempo do nome só começa a contar quando ele fecha.
  useEffect(() => {
    if (!nomeTocado || tocado) return;
    const t = window.setTimeout(() => setNomeTocado(null), NomeMs);
    return () => window.clearTimeout(t);
  }, [nomeTocado, tocado]);
  const posicaoNoPodio = useMemo(
    () => new Map(podio.map((p) => [p.usuarioId, p.posicao])),
    [podio],
  );
  const jogadas = useJogadas(grupoId);
  const { mutate: marcarVistas } = useMarcarJogadasVistas(grupoId);
  const meuAvatar = useMeuAvatar();
  const { mutate: marcarEnfeitesVistos } = useMarcarEnfeitesVistos();
  const rivais = useRivais(grupoId);
  const { mutate: marcarRivaisVistos } = useMarcarRivaisVistos(grupoId);
  const jogar = useJogarCarta(grupoId);
  const escolherRival = useEscolherRival(grupoId);
  const rivaisEmCena = useMemo(
    () => getRivaisEmCena(rivais.data?.rivais),
    [rivais.data],
  );

  const { membros } = cena;
  const posicoes = useMemo(
    () => posicionar(membros, usuarioId),
    [membros, usuarioId],
  );
  const porUsuario = useMemo(
    () => new Map(membros.map((m, i) => [m.usuarioId, posicoes[i]!])),
    [membros, posicoes],
  );
  const periodo = usePeriodoDoDia();
  const entrada = useEntrada({
    posicoes: porUsuario,
    centroY: elipse.centroY,
    porta: vaoDaPorta,
    largura,
    altura,
    reduzido,
  });
  const foco = useFoco({
    posicoes: porUsuario,
    centro,
    alturaPeito: Math.round((alturaCorpo * escala) / 2),
    usuarioId,
    reduzido,
    pausado: entrada.ativa,
  });
  const emFoco = foco.alvoId !== null;
  const [selecao, setSelecao] = useState<SelecaoMao | null>(null);
  /** Quanto falta para a carta arrastada soltar, de 0 a 1. */
  const [arrasto, setArrasto] = useState(0);
  // Ao trocar de alvo, a carta escolhida vai junto (fluxo carta primeiro); a "VS" é de um alvo só.
  // Ao fechar o Foco, a escolha acaba.
  const [alvoAnterior, setAlvoAnterior] = useState(foco.alvoId);
  if (alvoAnterior !== foco.alvoId) {
    setAlvoAnterior(foco.alvoId);
    setSelecao((s) => (foco.alvoId && s?.tipo === "carta" ? s : null));
    setArrasto(0);
  }
  const palco = useRef<HTMLDivElement>(null);
  // Ao abrir o Foco, a praça sobe para o alto da tela, com a Mão logo abaixo. Se as duas não
  // couberem acima da navegação, a Mão fica inteira e o alto da praça sai da tela.
  useEffect(() => {
    const elemento = palco.current;
    if (!emFoco || !elemento) return;
    const navegacao = document.querySelector<HTMLElement>(
      'nav[aria-label="Principal"]',
    );
    const rodape = navegacao?.offsetParent ? navegacao.offsetHeight : 0;
    const caixa = elemento.getBoundingClientRect();
    const visivel = window.innerHeight - rodape - 8;
    const topo =
      window.scrollY + caixa.top - 8 + Math.max(0, caixa.height + 8 - visivel);
    window.scrollTo({ top: topo, behavior: reduzido ? "auto" : "smooth" });
  }, [emFoco, reduzido]);
  const emAcao = foco.fase !== "aberto";
  const alvo = membros.find((m) => m.usuarioId === foco.alvoId);
  const cartaEscolhida = !emFoco && selecao?.tipo === "carta";
  /** O anel sob os pés: no Foco, quando a carta é arrastada; fora dele, em quem pode receber. */
  const getMira = (membro: MembroCena) => {
    if (emAcao) return null;
    if (emFoco)
      return membro.usuarioId === foco.alvoId && arrasto > 0 ? arrasto : null;
    return cartaEscolhida &&
      membro.usuarioId !== usuarioId &&
      membro.recebeCartas
      ? 0
      : null;
  };
  // As encenações esperam a entrada terminar e o Foco fechar.
  const aguardar = entrada.ativa || emFoco;
  const encenacao = useEncenacoes({
    jogadas: aguardar ? undefined : jogadas.data,
    posicoes: porUsuario,
    centro,
    reduzido,
    onVistas: marcarVistas,
    usuarioId,
    enfeitesNovos: aguardar ? semNovos : (meuAvatar.data?.novos ?? semNovos),
    onEnfeitesVistos: marcarEnfeitesVistos,
    rivais: aguardar ? semRivais : rivaisEmCena,
    onRivaisVistos: marcarRivaisVistos,
  });
  const vida = useVidaNaPraca({
    membros,
    posicoes: porUsuario,
    largura,
    altura,
    pausada: entrada.ativa || encenacao.ativa || emFoco,
    reduzido,
    comPombos: periodo !== "noite",
  });
  const getAtuacao = (id: string) =>
    entrada.atuacoes.get(id) ??
    foco.atuacoes.get(id) ??
    encenacao.atuacoes.get(id) ??
    vida.atuacoes.get(id);
  /** Quem atua na encenação, ou a carta que voa até alguém, mostra o nome. */
  const emEncenacao = (id: string) =>
    encenacao.atuacoes.has(id) ||
    encenacao.carta?.jogada.remetenteId === id ||
    encenacao.carta?.jogada.destinatarioId === id;
  /** No Foco, só o alvo e, durante a ação, quem vê a cena ficam acesos. */
  const atuaNoFoco = (id: string) =>
    id === foco.alvoId || (emAcao && id === usuarioId);

  const arremessar = (carta: CartaMao) => {
    if (!alvo) return;
    const nome = primeiroNome(alvo.nomeExibicao);
    const envio = jogar.mutateAsync({
      cartaId: carta.id,
      destinatarioId: alvo.usuarioId,
    });
    void Promise.allSettled([
      envio,
      foco.arremessar(carta.codigo, carta.nome, envio),
    ]).then(([resultado]) =>
      showToast(
        resultado.status === "fulfilled"
          ? `Carta entregue para ${nome}!`
          : getErrorMessage(resultado.reason),
      ),
    );
  };

  const rivalizar = () => {
    if (!alvo) return;
    const nome = primeiroNome(alvo.nomeExibicao);
    const envio = escolherRival.mutateAsync(alvo.usuarioId);
    void Promise.allSettled([envio, foco.rivalizar(envio)]).then(
      ([resultado]) =>
        showToast(
          resultado.status === "fulfilled"
            ? `${nome} é o seu Rival da semana!`
            : getErrorMessage(resultado.reason),
        ),
    );
  };

  const { camera } = foco;

  return (
    <div
      ref={palco}
      className="mx-auto w-full scroll-mt-2"
      style={{ maxWidth: largura }}
    >
      <div className="mb-2 flex min-h-6 justify-end">
        <Link to="/jogo/loja" aria-label="Energia e Loja" className="tap">
          <SaldoEnergia />
        </Link>
      </div>
      <div ref={ref} className="w-full" style={{ height: altura * fator }}>
        <section
          aria-label="Praça do grupo"
          className={`relative overflow-hidden rounded-xl transition-colors duration-300 ${emFoco ? "bg-surface-muted" : ""}`}
          style={{
            width: largura,
            height: altura,
            transform: `scale(${fator})`,
            transformOrigin: "top left",
          }}
        >
          {/* O tremor do impacto fica numa camada própria: a câmera já usa o transform. */}
          <div
            className={`absolute inset-0 ${foco.tremor ? "camera-tremor" : ""}`}
          >
            {/* A câmera: aproxima e desloca a praça inteira, com avatares e objetos. */}
            <div
              className="absolute inset-0"
              style={{
                transform: `translate(${camera.x}px, ${camera.y}px) scale(${camera.zoom})`,
                transformOrigin: "0 0",
                transition: `transform ${camera.ms}ms cubic-bezier(0.2, 0.8, 0.2, 1)`,
                backgroundImage: "url(/praca.png)",
                backgroundSize: `${largura}px ${altura}px`,
                imageRendering: "pixelated",
              }}
              onClick={(e) => {
                // Tocar no chão, fora dos avatares, fecha o Foco ou solta a carta escolhida.
                if (emAcao || e.target !== e.currentTarget) return;
                if (emFoco) foco.fechar();
                else setSelecao(null);
              }}
            >
              {membros.map((membro, i) => (
                <AvatarCena
                  key={membro.usuarioId}
                  membro={membro}
                  posicao={posicoes[i] ?? { x: centroX, y: altura / 2 }}
                  reduzido={reduzido}
                  atuacao={getAtuacao(membro.usuarioId)}
                  brilho={encenacao.brilho && membro.usuarioId === usuarioId}
                  nomeVisivel={
                    !emFoco &&
                    (nomeTocado === membro.usuarioId ||
                      emEncenacao(membro.usuarioId))
                  }
                  posicaoClassificacao={posicaoNoPodio.get(membro.usuarioId)}
                  proprio={membro.usuarioId === usuarioId}
                  apagado={emFoco && !atuaNoFoco(membro.usuarioId)}
                  mira={getMira(membro)}
                  rival={
                    membro.usuarioId === usuarioId
                      ? null
                      : getRelacaoRival(
                          rivais.data,
                          usuarioId,
                          membro.usuarioId,
                        )
                  }
                  onToque={() => {
                    if (emAcao) return;
                    if (membro.usuarioId !== usuarioId) {
                      foco.abrir(membro.usuarioId);
                    } else if (emFoco) {
                      foco.fechar();
                    } else {
                      setTocado(membro);
                      setNomeTocado(membro.usuarioId);
                    }
                  }}
                />
              ))}
              <ObjetosPraca portaAberta={entrada.portaAberta} />
              {vida.pombo && <Pombo pombo={vida.pombo} />}
              {vida.bola && <Bola bola={vida.bola} />}
              <LuzDaPraca periodo={periodo} />
              {encenacao.carta && (
                <CartaVoando
                  carta={{
                    ...encenacao.carta,
                    codigo: encenacao.carta.jogada.carta.codigo,
                  }}
                />
              )}
              {foco.carta && <CartaVoando carta={foco.carta} />}
              {foco.efeito && <ImpactoNoFoco efeito={foco.efeito} />}
            </div>
          </div>
          {foco.efeito?.tipo === "vs" && (
            <span
              aria-hidden="true"
              className="vs-batida pointer-events-none absolute inset-x-0 top-20 mx-auto w-fit rounded-xl border-4 border-outline bg-game px-4 font-display text-7xl leading-tight text-on-game shadow-sticker"
              style={{ zIndex: 1003 }}
            >
              VS
            </span>
          )}
          {alvo && (
            <PainelFoco
              key={alvo.usuarioId}
              grupoId={grupoId}
              usuarioId={usuarioId}
              membro={alvo}
              oculto={emAcao}
              onFechar={foco.fechar}
            />
          )}
          {(entrada.ativa || encenacao.ativa) && (
            // Tocar na praça pula a entrada e a encenação: elas nunca prendem a pessoa.
            <button
              type="button"
              className="absolute inset-0 cursor-pointer"
              style={{ zIndex: 1002 }}
              onClick={entrada.ativa ? entrada.pular : encenacao.pular}
            >
              <span className="sr-only">Pular a encenação</span>
            </button>
          )}
        </section>
      </div>
      <MaoJogo
        key={alvo?.usuarioId ?? ""}
        grupoId={grupoId}
        alvo={alvo ?? null}
        podeRival={
          !!alvo && (rivais.data?.candidatos.includes(alvo.usuarioId) ?? false)
        }
        recolhida={emAcao}
        selecao={selecao}
        onSelecao={setSelecao}
        onArremessar={arremessar}
        onRival={rivalizar}
        onArrasto={setArrasto}
      />
      <p aria-live="polite" className="sr-only">
        {foco.efeito?.tipo === "impacto" && alvo
          ? `${foco.efeito.texto} entregue para ${primeiroNome(alvo.nomeExibicao)}.`
          : ""}
      </p>
      <MenuAvatar
        grupoId={grupoId}
        membro={tocado}
        onClose={() => setTocado(null)}
      />
    </div>
  );
}
