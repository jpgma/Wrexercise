import { type CSSProperties, useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import {
  formatoFolha,
  hashFnv1a,
  useFolhaAvatar,
  useVitrine,
} from "@/components/folhaAvatar";
import { getDirecao, variacoesDeParado, type NomeAnimacao } from "./animacoes";
import { IconeCarta } from "./CartaJogo";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { MenuAvatar } from "./MenuAvatar";
import { ObjetosPraca, vaoDaPorta } from "./ObjetosPraca";
import { SaldoEnergia } from "./SaldoEnergia";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMarcarEnfeitesVistos, useMeuAvatar } from "./useAvatares";
import { useJogadas, useMarcarJogadasVistas } from "./useCartas";
import { useCena, type Cena, type MembroCena } from "./useCena";
import {
  type Atuacao,
  type CartaEmCena,
  type RivalEmCena,
  useEncenacoes,
} from "./useEncenacoes";
import { useEntrada } from "./useEntrada";
import { useMovimentoReduzido } from "./useMovimentoReduzido";
import { useVidaNaPraca } from "./useVidaNaPraca";
import { usePeriodoDoDia } from "./periodoDoDia";
import { Bola, LuzDaPraca, Pombo } from "./VidaNaPraca";
import {
  getVencedorId,
  type Rival,
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

/** Prefixo da provocação de quem acabou de escolher: só existe na tela, o servidor não a conhece. */
const idLocal = "agora-";

/**
 * O que ainda não foi visto do Rival: a provocação, para quem foi escolhido; o resultado, para quem
 * escolheu. A provocação de quem acabou de escolher toca na hora e não tem o que registrar.
 */
function getRivaisEmCena(
  rivais: Rival[] | undefined,
  usuarioId: string,
  escolhidoAgora: string | null,
): RivalEmCena[] {
  const daApi: RivalEmCena[] = (rivais ?? [])
    .filter((r) => r.nova)
    .map((r) => ({
      id: r.id,
      escolhedorId: r.escolhedorId,
      rivalId: r.rivalId,
      efeito: r.escolhido ? "resultado" : "provocacao",
      vencedorId: r.escolhido ? getVencedorId(r) : null,
    }));
  if (!escolhidoAgora) return daApi;
  return [
    {
      id: `${idLocal}${escolhidoAgora}`,
      escolhedorId: usuarioId,
      rivalId: escolhidoAgora,
      efeito: "provocacao",
      vencedorId: null,
    },
    ...daApi,
  ];
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
    transition: `transform ${atuacao?.passoMs ?? 0}ms linear`,
    // Atrás da porta, antes de sair na entrada do dia.
    visibility: atuacao?.oculto ? "hidden" : undefined,
  } as const;
  const primeiroNome = membro.nomeExibicao.trim().split(/\s+/)[0];
  const medalha = posicaoClassificacao
    ? medalhas[posicaoClassificacao]
    : undefined;

  return (
    <>
      <button
        type="button"
        aria-label={
          medalha
            ? `${membro.nomeExibicao}, ${posicaoClassificacao}º lugar no desafio`
            : membro.nomeExibicao
        }
        aria-haspopup="dialog"
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
            estatico={reduzido}
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
      {(nomeVisivel || medalha) && (
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
          {medalha && nomeVisivel && " "}
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
function CartaVoando({ carta }: { carta: CartaEmCena }) {
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
      <IconeCarta codigo={carta.jogada.carta.codigo} />
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
  const [escolhidoAgora, setEscolhidoAgora] = useState<string | null>(null);
  const rivaisEmCena = useMemo(
    () => getRivaisEmCena(rivais.data?.rivais, usuarioId, escolhidoAgora),
    [rivais.data, usuarioId, escolhidoAgora],
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
  // As encenações esperam a entrada terminar.
  const encenacao = useEncenacoes({
    jogadas: entrada.ativa ? undefined : jogadas.data,
    posicoes: porUsuario,
    centro,
    reduzido,
    onVistas: marcarVistas,
    usuarioId,
    enfeitesNovos: entrada.ativa
      ? semNovos
      : (meuAvatar.data?.novos ?? semNovos),
    onEnfeitesVistos: marcarEnfeitesVistos,
    rivais: entrada.ativa ? semRivais : rivaisEmCena,
    onRivaisVistos: (ids) => {
      const doServidor = ids.filter((id) => !id.startsWith(idLocal));
      if (doServidor.length > 0) marcarRivaisVistos(doServidor);
      if (doServidor.length < ids.length) setEscolhidoAgora(null);
    },
  });
  const vida = useVidaNaPraca({
    membros,
    posicoes: porUsuario,
    largura,
    altura,
    pausada: entrada.ativa || encenacao.ativa,
    reduzido,
    comPombos: periodo !== "noite",
  });
  const getAtuacao = (id: string) =>
    entrada.atuacoes.get(id) ??
    encenacao.atuacoes.get(id) ??
    vida.atuacoes.get(id);
  /** Quem atua na encenação, ou a carta que voa até alguém, mostra o nome. */
  const emEncenacao = (id: string) =>
    encenacao.atuacoes.has(id) ||
    encenacao.carta?.jogada.remetenteId === id ||
    encenacao.carta?.jogada.destinatarioId === id;

  return (
    <div className="mx-auto w-full" style={{ maxWidth: largura }}>
      <div className="mb-2 flex min-h-6 justify-end">
        <Link to="/jogo/loja" aria-label="Energia e Loja" className="tap">
          <SaldoEnergia />
        </Link>
      </div>
      <div ref={ref} className="w-full" style={{ height: altura * fator }}>
        <section
          aria-label="Praça do grupo"
          className="relative overflow-hidden rounded-xl"
          style={{
            width: largura,
            height: altura,
            transform: `scale(${fator})`,
            transformOrigin: "top left",
            backgroundImage: "url(/praca.png)",
            backgroundSize: `${largura}px ${altura}px`,
            imageRendering: "pixelated",
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
                nomeTocado === membro.usuarioId || emEncenacao(membro.usuarioId)
              }
              posicaoClassificacao={posicaoNoPodio.get(membro.usuarioId)}
              onToque={() => {
                setTocado(membro);
                setNomeTocado(membro.usuarioId);
              }}
            />
          ))}
          <ObjetosPraca portaAberta={entrada.portaAberta} />
          {vida.pombo && <Pombo pombo={vida.pombo} />}
          {vida.bola && <Bola bola={vida.bola} />}
          <LuzDaPraca periodo={periodo} />
          {encenacao.carta && <CartaVoando carta={encenacao.carta} />}
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
      <MenuAvatar
        grupoId={grupoId}
        membro={tocado}
        usuarioId={usuarioId}
        onClose={() => setTocado(null)}
        onRival={setEscolhidoAgora}
      />
    </div>
  );
}
