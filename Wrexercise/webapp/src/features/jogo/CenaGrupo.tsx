import { Swords } from "lucide-react";
import {
  type CSSProperties,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import {
  formatoFolha,
  hashFnv1a,
  useFolhaAvatar,
  useVitrines,
} from "@/components/folhaAvatar";
import {
  getDirecao,
  getDuracao,
  variacoesDeParado,
  type NomeAnimacao,
} from "./animacoes";
import { IconeCarta } from "./CartaJogo";
import { EnfeiteNoChao } from "./EnfeiteNoChao";
import { getChaoDosEnfeites, posicionarEnfeites } from "./enfeitesNaPraca";
import { MaoJogo, type SelecaoMao } from "./MaoJogo";
import { ObjetosPraca, vaoDaPorta } from "./ObjetosPraca";
import { getAnimacaoDoTreino } from "./AvatarTreino";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMarcarEnfeitesVistos, useMeuAvatar } from "./useAvatares";
import { CelebracaoConquista } from "./CelebracaoConquista";
import { type DestinoChegada, PainelChegada } from "./PainelChegada";
import { PainelFoco } from "./PainelFoco";
import type { RespostaEnviada } from "./RespostaCarta";
import { type PlacarCena, PlacarJogo } from "./PlacarJogo";
import { getTipoCutucada } from "./textosCutucada";
import { getBloqueioJogada, primeiroNome } from "./textosCartas";
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
import { useDespertos } from "./useDespertos";
import { useEntrada } from "./useEntrada";
import { type EfeitoFoco, useFoco } from "./useFoco";
import { useMovimentoReduzido } from "./useMovimentoReduzido";
import { useVidaNaPraca } from "./useVidaNaPraca";
import { VitrineFoco } from "./VitrineFoco";
import { isLuzAcesa, usePeriodoDoDia } from "./periodoDoDia";
import {
  Bola,
  BolhaSonho,
  LuzDaPraca,
  Pombo,
  SonhoMs,
  ZzzSono,
} from "./VidaNaPraca";
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
/** Respiro entre a praça e as bordas da tela ao rolar para o Foco. */
const Folga = 8;
/** Ao lado da cabeça de quem dorme sentado, em px da folha: onde o "Zzz" começa a subir. */
const CabecaSentado = { x: 27, y: 22 };
/** A bolha de sonho de quem dorme aparece de vez em quando, no lugar do "Zzz". */
const SonhoACada = { min: 14_000, max: 28_000 };
const SonhoDura = 4 * SonhoMs;

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
  // Treino junto (ADR 0078): quem treina ao vivo fica lado a lado, logo depois de quem vê.
  const outros = membros
    .filter((m) => m.usuarioId !== usuarioId)
    .sort((a, b) => Number(!!b.treinoAoVivo) - Number(!!a.treinoAoVivo));
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
  nomeVisivel,
  posicaoClassificacao,
  apagado,
  mira,
  rival,
  onToque,
}: {
  membro: MembroCena;
  posicao: Posicao;
  reduzido: boolean;
  atuacao: Atuacao | undefined;
  /** O nome só aparece durante as encenações. */
  nomeVisivel: boolean;
  /** 1, 2 ou 3 no desafio em destaque: a medalha aparece sempre, com ou sem o nome. */
  posicaoClassificacao: number | undefined;
  /** Durante o Foco, quem não atua fica apagado. */
  apagado: boolean;
  /** Anel de mira sob os pés, de 0 a 1: alvo de uma carta escolhida ou arrastada. */
  mira: number | null;
  /** O par do Rival desta semana com quem vê a cena. */
  rival: RelacaoRival | null;
  onToque: () => void;
}) {
  const folha = useFolhaAvatar(membro.usuarioId);
  const [variacao, setVariacao] = useState<NomeAnimacao | null>(null);
  /** Conta os toques: cada um recomeça o pulinho. */
  const [pulos, setPulos] = useState(0);
  const parado = membro.pose === "parado";
  const dormindo = membro.pose === "dormindo";
  // Quem dorme adormece de novo a cada vez que a cena abre, ou quando passa a dormir.
  const [adormecendo, setAdormecendo] = useState(dormindo && !reduzido);
  const [poseAnterior, setPoseAnterior] = useState(membro.pose);
  if (poseAnterior !== membro.pose) {
    setPoseAnterior(membro.pose);
    setAdormecendo(dormindo && !reduzido);
  }
  const [sonhando, setSonhando] = useState(false);

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

  // O `adormecer` toca quando ninguém mais move o avatar: depois da entrada e das encenações.
  const emAtuacao = atuacao !== undefined;
  useEffect(() => {
    if (!adormecendo || emAtuacao) return;
    const temporizador = window.setTimeout(
      () => setAdormecendo(false),
      getDuracao("adormecer"),
    );
    return () => window.clearTimeout(temporizador);
  }, [adormecendo, emAtuacao]);

  // De vez em quando, quem dorme sonha com o treino: a bolha toma o lugar do "Zzz" por um instante.
  useEffect(() => {
    if (!dormindo || reduzido) return;
    let temporizador: number;
    const agendar = () => {
      temporizador = window.setTimeout(
        () => {
          if (document.visibilityState !== "visible") {
            agendar();
            return;
          }
          setSonhando(true);
          temporizador = window.setTimeout(() => {
            setSonhando(false);
            agendar();
          }, SonhoDura);
        },
        SonhoACada.min + sorteio() * (SonhoACada.max - SonhoACada.min),
      );
    };
    agendar();
    return () => {
      window.clearTimeout(temporizador);
      setSonhando(false);
    };
  }, [dormindo, reduzido, sorteio]);

  const aoVivo = membro.treinoAoVivo;
  const animacao: NomeAnimacao =
    atuacao?.animacao ??
    (aoVivo
      ? getAnimacaoDoTreino(aoVivo.tipo)
      : parado
        ? (variacao ?? "parado")
        : dormindo
          ? adormecendo
            ? "adormecer"
            : "dormindo"
          : membro.pose);
  /** O "Zzz" e a bolha só aparecem com o avatar sentado, dormindo. */
  const sono = animacao === "dormindo";
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
    aoVivo && "treinando ao vivo agora",
    membro.cutucadas &&
      (membro.cutucadas.total === 1
        ? "cutucado 1 vez hoje"
        : `cutucado ${membro.cutucadas.total} vezes hoje`),
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <>
      <button
        type="button"
        aria-label={rotulo}
        onClick={() => {
          // Quem dorme não pula: o Foco o faz levar um susto (`acordar`).
          if (!reduzido && !dormindo) setPulos((n) => n + 1);
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
          {sono &&
            (sonhando ? (
              <BolhaSonho
                x={(CabecaSentado.x - corpo.esquerda) * escala}
                y={(CabecaSentado.y - corpo.topo) * escala}
              />
            ) : (
              <ZzzSono
                x={(CabecaSentado.x - corpo.esquerda) * escala}
                y={(CabecaSentado.y - corpo.topo) * escala}
                reduzido={reduzido}
              />
            ))}
          {aoVivo && !atuacao && (
            <BolhaAoVivo
              x={(ancora.x - corpo.esquerda) * escala}
              startedAt={aoVivo.startedAt}
              torcidas={aoVivo.torcidas}
              reduzido={reduzido}
            />
          )}
          {sono && membro.cutucadas && (
            <BolhaCutucada
              x={(CabecaSentado.x - corpo.esquerda) * escala}
              y={(CabecaSentado.y - corpo.topo) * escala}
              {...membro.cutucadas}
            />
          )}
        </span>
      </button>
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

/** Os minutos desde o início, atualizados a cada 30 segundos. */
function useMinutosDesde(inicio: string) {
  const [agora, setAgora] = useState(() => Date.now());
  useEffect(() => {
    const intervalo = window.setInterval(() => setAgora(Date.now()), 30_000);
    return () => window.clearInterval(intervalo);
  }, []);
  return Math.max(0, Math.floor((agora - new Date(inicio).getTime()) / 60_000));
}

/**
 * Sobre a cabeça de quem treina ao vivo (ADR 0078): "AO VIVO", os minutos e as Torcidas. Todos na praça
 * veem quem está treinando agora.
 */
function BolhaAoVivo({
  x,
  startedAt,
  torcidas,
  reduzido,
}: {
  x: number;
  startedAt: string;
  torcidas: number;
  reduzido: boolean;
}) {
  const minutos = useMinutosDesde(startedAt);
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute flex -translate-x-1/2 items-center gap-1 rounded-full border-2 border-outline bg-surface px-1.5 text-2xs leading-tight font-semibold whitespace-nowrap shadow-sticker"
      style={{ left: x, top: -20 }}
    >
      <span
        className={`size-1.5 rounded-full bg-danger ${reduzido ? "" : "motion-safe:animate-pulse"}`}
      />
      AO VIVO
      <span className="font-score">{minutos}′</span>
      {torcidas > 0 && <span>📣{torcidas > 1 ? torcidas : ""}</span>}
    </span>
  );
}

/**
 * Sobre quem dorme e foi cutucado nas últimas 24 horas: o emoji da última cutucada e quantas foram,
 * do lado oposto ao "Zzz". Todos na praça veem quem está sendo chamado (decisão 0063).
 */
function BolhaCutucada({
  x,
  y,
  total,
  ultimoTipo,
}: {
  x: number;
  y: number;
  total: number;
  ultimoTipo: string;
}) {
  const tipo = getTipoCutucada(ultimoTipo);
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute flex -translate-x-full items-center gap-0.5 rounded-full border-2 border-outline bg-surface px-1 text-xs leading-none whitespace-nowrap shadow-sticker"
      style={{ left: x - 6, top: y - 22 }}
    >
      <span>{tipo?.emoji ?? "👉"}</span>
      {total > 1 && <span className="font-score text-2xs">×{total}</span>}
    </span>
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

/** A Chegada de uma carta recebida (decisão 0076): a cena encena o arremesso e pede a resposta. */
export type ChegadaNaCena = {
  jogadaId: string;
  onConcluir: (destino: DestinoChegada) => void;
};

export function CenaGrupo({
  grupoId,
  usuarioId,
  placar,
  chegada,
}: {
  grupoId: string;
  usuarioId: string;
  /** A classificação do desafio em destaque; sem ela, nem Placar nem medalhas. */
  placar?: PlacarCena;
  chegada?: ChegadaNaCena;
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
      placar={placar}
      chegada={chegada}
    />
  );
}

/** A carta da encenação: voa até quem recebe, cai no lugar ao ser aceita ou sai pelo lado. */
function CartaVoando({
  carta,
}: {
  carta: Pick<CartaEmCena, "efeito" | "de" | "para"> & {
    codigo: CodigoCarta;
    /** A segunda carta de uma Fusão: voa junto, atrás da primeira. */
    codigoFusao?: CodigoCarta;
  };
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
      {carta.codigoFusao && (
        <span className="absolute -top-1.5 -right-2.5 -z-10 grid size-10 rotate-12 place-items-center rounded-md border-2 border-carta-tinta bg-carta-giz">
          <IconeCarta codigo={carta.codigoFusao} />
        </span>
      )}
      <IconeCarta codigo={carta.codigo} />
    </span>
  );
}

/** O nome da carta estoura sobre quem recebe. O "VS" do Rival fica fora da câmera, no alto da praça. */
function ImpactoNoFoco({ efeito }: { efeito: EfeitoFoco }) {
  if (efeito.tipo !== "impacto") return null;
  return (
    <>
      {efeito.fusao && (
        <span
          aria-hidden="true"
          className="fusao-faiscas pointer-events-none absolute"
          style={{
            left: efeito.ponto.x - 32,
            top: efeito.ponto.y - 13 * escala - 32,
            zIndex: 1001,
          }}
        />
      )}
      <ImpactoTexto efeito={efeito} />
    </>
  );
}

function ImpactoTexto({
  efeito,
}: {
  efeito: Extract<EfeitoFoco, { tipo: "impacto" }>;
}) {
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
  placar,
  chegada,
}: {
  cena: Cena;
  grupoId: string;
  usuarioId: string;
  reduzido: boolean;
  placar: PlacarCena | undefined;
  chegada: ChegadaNaCena | undefined;
}) {
  const { ref, fator } = useFatorDeEncaixe();
  const { showToast } = useToast();
  // Medalhas só para os 3 primeiros que já têm atividade verificada.
  const posicaoNoPodio = useMemo(
    () =>
      new Map(
        (placar?.itens ?? [])
          .filter((p) => p.posicao <= 3 && p.sessoesVerificadas > 0)
          .map((p) => [p.usuarioId, p.posicao]),
      ),
    [placar],
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
  const dormindo = useMemo(
    () =>
      new Set(
        membros.filter((m) => m.pose === "dormindo").map((m) => m.usuarioId),
      ),
    [membros],
  );
  const aoVivo = useMemo(
    () =>
      new Set(membros.filter((m) => m.treinoAoVivo).map((m) => m.usuarioId)),
    [membros],
  );
  const { despertos, marcarVistos: marcarDespertosVistos } = useDespertos(
    grupoId,
    membros,
  );
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
    dormindo,
    porta: vaoDaPorta,
  });
  const emFoco = foco.alvoId !== null;

  // A Chegada (decisão 0076): sem a entrada, a carta voa até quem vê e a resposta toma a Mão.
  const emChegada = chegada !== undefined;
  const jogadaChegada = chegada
    ? jogadas.data?.find((j) => j.id === chegada.jogadaId)
    : undefined;
  const [respondida, setRespondida] = useState<RespostaEnviada | null>(null);
  const chegadaIniciada = useRef<string | null>(null);
  const { ativa: entradaAtiva, pular: pularEntrada } = entrada;
  useEffect(() => {
    if (emChegada && entradaAtiva) pularEntrada();
  }, [emChegada, entradaAtiva, pularEntrada]);
  const { receber } = foco;
  useEffect(() => {
    if (
      !jogadaChegada ||
      entradaAtiva ||
      chegadaIniciada.current === jogadaChegada.id ||
      !(jogadaChegada.recebida && jogadaChegada.situacao === "Enviada")
    )
      return;
    chegadaIniciada.current = jogadaChegada.id;
    void receber({
      remetenteId: jogadaChegada.remetenteId,
      codigo: jogadaChegada.carta.codigo,
      codigoFusao: jogadaChegada.cartaFusao?.codigo,
      texto: jogadaChegada.cartaFusao ? "Fusão" : jogadaChegada.carta.nome,
    });
  }, [jogadaChegada, entradaAtiva, receber]);
  /** Na Chegada, só quem vê e quem jogou a carta ficam acesos. */
  const atuaNaChegada = (id: string) =>
    id === usuarioId || id === jogadaChegada?.remetenteId;
  const responderChegada = (resposta: RespostaEnviada) => {
    setRespondida(resposta);
    if (jogadaChegada) {
      void foco.responderChegada(
        resposta.aceitou,
        jogadaChegada.carta.codigo,
        jogadaChegada.cartaFusao?.codigo,
      );
    }
  };
  const concluirChegada = (destino: DestinoChegada) => {
    foco.soltarChegada();
    chegada?.onConcluir(destino);
  };
  /** O Foco no próprio avatar: a Vitrine toma o lugar da Mão. */
  const focoProprio = foco.alvoId === usuarioId;
  const [selecao, setSelecao] = useState<SelecaoMao | null>(null);
  /** O instante da escolha da carta: base do limite de 24 horas para os anéis da praça. */
  const [escolhidaEm, setEscolhidaEm] = useState(0);
  const escolher = (s: SelecaoMao | null) => {
    setSelecao(s);
    setEscolhidaEm(Date.now());
  };
  /** Quanto falta para a carta arrastada soltar, de 0 a 1. */
  const [arrasto, setArrasto] = useState(0);
  // Ao trocar de alvo, a carta escolhida vai junto (fluxo carta primeiro); a "VS" é de um alvo só.
  // Ao fechar o Foco, ou abrir o próprio, a escolha acaba.
  const [alvoAnterior, setAlvoAnterior] = useState(foco.alvoId);
  if (alvoAnterior !== foco.alvoId) {
    setAlvoAnterior(foco.alvoId);
    setSelecao((s) =>
      foco.alvoId && !focoProprio && s?.tipo === "carta"
        ? { tipo: "carta", carta: s.carta }
        : null,
    );
    setArrasto(0);
  }
  const palco = useRef<HTMLDivElement>(null);
  // Ao abrir o Foco, a praça sobe para o alto da tela, com a Mão logo abaixo: numa tela de
  // 390×844, as duas cabem sem rolar mais.
  useEffect(() => {
    if (!(emFoco || emChegada) || !palco.current) return;
    window.scrollTo({
      top: window.scrollY + palco.current.getBoundingClientRect().top - Folga,
      behavior: reduzido ? "auto" : "smooth",
    });
  }, [emFoco, emChegada, reduzido]);
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
      membro.recebeCartas &&
      !getBloqueioJogada(jogadas.data ?? [], membro.usuarioId, "", escolhidaEm)
      ? 0
      : null;
  };
  // As encenações esperam a entrada terminar, o Foco fechar e a Chegada acabar.
  const aguardar = entrada.ativa || emFoco || emChegada;
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
    dormindo,
    despertos: aguardar ? semNovos : despertos,
    onDespertosVistos: marcarDespertosVistos,
  });
  const vitrines = useVitrines();
  const enfeitesNovos = meuAvatar.data?.novos ?? semNovos;
  /** O Foco mostra os 3 Enfeites do alvo; a encenação do Enfeite novo mostra o novo de quem vê. */
  const extras = useMemo(() => {
    const mapa = new Map<string, "todos" | string[]>();
    if (encenacao.brilho) mapa.set(usuarioId, enfeitesNovos);
    if (foco.alvoId) mapa.set(foco.alvoId, "todos");
    return mapa;
  }, [encenacao.brilho, usuarioId, enfeitesNovos, foco.alvoId]);
  const enfeites = useMemo(
    () =>
      posicionarEnfeites({
        membros,
        posicoes: porUsuario,
        centro,
        vitrines,
        largura,
        altura,
        extras,
      }),
    [membros, porUsuario, vitrines, extras],
  );
  const chaoDosEnfeites = useMemo(
    () => getChaoDosEnfeites(enfeites),
    [enfeites],
  );
  const vida = useVidaNaPraca({
    membros,
    posicoes: porUsuario,
    ocupados: chaoDosEnfeites,
    largura,
    altura,
    pausada: entrada.ativa || encenacao.ativa || emFoco || emChegada,
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

  const arremessar = (carta: CartaMao, fusao?: CartaMao) => {
    if (!alvo) return;
    const nome = primeiroNome(alvo.nomeExibicao);
    const envio = jogar.mutateAsync({
      cartaId: carta.id,
      destinatarioId: alvo.usuarioId,
      cartaFusaoId: fusao?.id,
    });
    void Promise.allSettled([
      envio,
      foco.arremessar(
        carta.codigo,
        fusao ? "Fusão" : carta.nome,
        envio,
        fusao?.codigo,
      ),
    ]).then(([resultado]) =>
      showToast(
        resultado.status === "fulfilled"
          ? `${fusao ? "Fusão" : "Carta"} entregue para ${nome}!`
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
      <div ref={ref} className="w-full" style={{ height: altura * fator }}>
        <section
          aria-label="Praça do grupo"
          className={`relative overflow-hidden rounded-xl transition-colors duration-300 ${emFoco || emChegada ? "bg-surface-muted" : ""}`}
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
                  nomeVisivel={!emFoco && emEncenacao(membro.usuarioId)}
                  posicaoClassificacao={posicaoNoPodio.get(membro.usuarioId)}
                  apagado={
                    (emFoco && !atuaNoFoco(membro.usuarioId)) ||
                    (emChegada && !atuaNaChegada(membro.usuarioId)) ||
                    // Com uma carta escolhida, quem não pode recebê-la agora fica apagado.
                    (cartaEscolhida &&
                      !emAcao &&
                      membro.usuarioId !== usuarioId &&
                      getMira(membro) === null)
                  }
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
                    if (!emAcao) foco.abrir(membro.usuarioId);
                  }}
                />
              ))}
              {enfeites.map((item) => (
                <EnfeiteNoChao
                  key={item.chave}
                  item={item}
                  reduzido={reduzido}
                  apagado={emFoco && !atuaNoFoco(item.usuarioId)}
                  aceso={isLuzAcesa(periodo)}
                  brilho={
                    encenacao.brilho &&
                    item.usuarioId === usuarioId &&
                    enfeitesNovos.includes(item.enfeite.codigo)
                  }
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
                    codigoFusao: encenacao.carta.jogada.cartaFusao?.codigo,
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
          {placar && (
            <PlacarJogo
              placar={placar}
              usuarioId={usuarioId}
              dormindo={dormindo}
              aoVivo={aoVivo}
              oculto={emFoco || emChegada || entrada.ativa || encenacao.ativa}
              onToque={(id) => {
                if (!emAcao) foco.abrir(id);
              }}
            />
          )}
          {alvo && (
            <PainelFoco
              key={alvo.usuarioId}
              grupoId={grupoId}
              usuarioId={usuarioId}
              membro={alvo}
              oculto={emAcao}
              onFechar={foco.fechar}
              onCutucado={foco.cutucar}
            />
          )}
          {(entrada.ativa || encenacao.ativa || foco.fase === "chegando") && (
            // Tocar na praça pula a entrada e as encenações: elas nunca prendem a pessoa. Na
            // Chegada, pula direto para a resposta.
            <button
              type="button"
              className="absolute inset-0 cursor-pointer"
              style={{ zIndex: 1002 }}
              onClick={
                foco.fase === "chegando"
                  ? foco.pularChegada
                  : entrada.ativa
                    ? entrada.pular
                    : encenacao.pular
              }
            >
              <span className="sr-only">Pular a encenação</span>
            </button>
          )}
        </section>
      </div>
      <CelebracaoConquista
        pronto={!aguardar && !encenacao.ativa}
        onVerVitrine={() => foco.abrir(usuarioId)}
      />
      {emChegada ? (
        <PainelChegada
          jogada={jogadaChegada}
          carregando={!jogadas.data}
          grupoId={grupoId}
          chegando={
            foco.fase === "chegando" ||
            (!respondida &&
              foco.fase === "aberto" &&
              jogadaChegada?.recebida === true &&
              jogadaChegada.situacao === "Enviada")
          }
          reduzido={reduzido}
          respondida={respondida}
          onRespondida={responderChegada}
          onConcluir={concluirChegada}
        />
      ) : focoProprio ? (
        <VitrineFoco />
      ) : (
        <MaoJogo
          key={alvo?.usuarioId ?? ""}
          grupoId={grupoId}
          alvo={alvo ?? null}
          podeRival={
            !!alvo &&
            (rivais.data?.candidatos.includes(alvo.usuarioId) ?? false)
          }
          recolhida={emAcao}
          selecao={selecao}
          onSelecao={escolher}
          onArremessar={arremessar}
          onRival={rivalizar}
          onArrasto={setArrasto}
        />
      )}
      <p aria-live="polite" className="sr-only">
        {foco.efeito?.tipo === "impacto" && alvo
          ? `${foco.efeito.texto} entregue para ${primeiroNome(alvo.nomeExibicao)}.`
          : foco.efeito?.tipo === "impacto" && jogadaChegada
            ? `${primeiroNome(jogadaChegada.remetenteNome)} te provocou: ${foco.efeito.texto}.`
            : ""}
      </p>
    </div>
  );
}
