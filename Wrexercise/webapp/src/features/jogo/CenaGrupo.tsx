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
import { RivalDaSemana } from "./RivalDaSemana";
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
import { useMovimentoReduzido } from "./useMovimentoReduzido";
import {
  getVencedorId,
  type Rival,
  useMarcarRivaisVistos,
  useRivais,
} from "./useRivais";

// O chão tem 180×230 px e é mostrado em 2×.
const semNovos: string[] = [];
const largura = 360;
const altura = 460;
const centroX = largura / 2;
const raioX = 130;
/** Onde os pés podem pisar: abaixo da mureta e com espaço para o nome embaixo. */
const topoDosPes = 46;
const folgaInferior = 22;
const { ancora, corpo } = formatoFolha;
/** Altura do corpo acima dos pés, em px da folha. */
const alturaCorpo = ancora.y - corpo.topo;

/** Elipse em pé: ocupa a altura toda da praça, deixando espaço para o avatar (o corpo × escala) e o nome. */
function getElipse(escala: number) {
  const minY = topoDosPes + alturaCorpo * escala;
  const maxY = altura - folgaInferior;
  return { centroY: (minY + maxY) / 2, raioY: (maxY - minY) / 2 };
}

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
function posicionar(
  membros: MembroCena[],
  usuarioId: string,
  escala: number,
): Posicao[] {
  const n = membros.length;
  const { centroY, raioY } = getElipse(escala);
  if (n === 1) return [{ x: centroX, y: centroY }];
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

/**
 * Parado na elipse, o avatar vira para o centro na horizontal, mas sempre de frente para quem vê a
 * tela: de costas, quem fica embaixo (a própria pessoa) só mostraria a nuca.
 */
function getDirecaoParado(posicao: Posicao, centro: Posicao) {
  return getDirecao(posicao, {
    x: centro.x,
    y: posicao.y + Math.abs(centro.y - posicao.y),
  });
}

function AvatarCena({
  membro,
  posicao,
  escala,
  reduzido,
  centro,
  atuacao,
  brilho,
  onToque,
}: {
  membro: MembroCena;
  posicao: Posicao;
  /** Centro da praça: para onde olha quem está parado. */
  centro: Posicao;
  escala: number;
  reduzido: boolean;
  atuacao: Atuacao | undefined;
  /** O Enfeite do avatar brilha: é o Enfeite novo de quem vê a cena. */
  brilho: boolean;
  onToque: () => void;
}) {
  const folha = useFolhaAvatar();
  const enfeite = useVitrine(membro.usuarioId)[0];
  // O Enfeite (16 px) acompanha a escala da cena sem crescer demais: 32 px, ou 16 px com muita gente.
  const escalaEnfeite = escala >= 3 ? 2 : 1;
  const [variacao, setVariacao] = useState<NomeAnimacao | null>(null);
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
  const direcao = atuacao?.direcao ?? getDirecaoParado(posicao, centro);
  const deslocamento = {
    transform: atuacao
      ? `translate(${atuacao.dx}px, ${atuacao.dy}px)`
      : undefined,
    transition: `transform ${atuacao?.passoMs ?? 0}ms linear`,
  };
  const primeiroNome = membro.nomeExibicao.trim().split(/\s+/)[0];

  return (
    <>
      <button
        type="button"
        aria-label={membro.nomeExibicao}
        aria-haspopup="dialog"
        onClick={onToque}
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
      </button>
      {enfeite && (
        <EnfeiteIcone
          conquista={enfeite.conquista}
          tamanho={16 * escalaEnfeite}
          brilho={brilho}
          className="pointer-events-none absolute block"
          style={{
            left: posicao.x + 9 * escala,
            top: posicao.y - 16 * escalaEnfeite - 2,
            zIndex: Math.round(posicao.y + (atuacao?.dy ?? 0)) + 1,
            ...deslocamento,
          }}
        />
      )}
      {/* Os nomes ficam numa camada acima de todos os avatares: ninguém esconde o nome de outro. */}
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
        {primeiroNome}
      </span>
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

export function CenaGrupo({
  grupoId,
  usuarioId,
}: {
  grupoId: string;
  usuarioId: string;
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
    />
  );
}

/** A carta da encenação: voa até quem recebe, cai no lugar ao ser aceita ou sai pelo lado. */
function CartaVoando({
  carta,
  escala,
}: {
  carta: CartaEmCena;
  escala: number;
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
      <IconeCarta codigo={carta.jogada.carta.codigo} />
    </span>
  );
}

function Praca({
  cena,
  grupoId,
  usuarioId,
  reduzido,
}: {
  cena: Cena;
  grupoId: string;
  usuarioId: string;
  reduzido: boolean;
}) {
  const { ref, fator } = useFatorDeEncaixe();
  const [tocado, setTocado] = useState<MembroCena | null>(null);
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
  const escala = membros.length <= 5 ? 3 : 2;
  const posicoes = useMemo(
    () => posicionar(membros, usuarioId, escala),
    [membros, usuarioId, escala],
  );
  const porUsuario = useMemo(
    () => new Map(membros.map((m, i) => [m.usuarioId, posicoes[i]!])),
    [membros, posicoes],
  );
  const centro = useMemo(
    () => ({ x: centroX, y: getElipse(escala).centroY }),
    [escala],
  );
  const encenacao = useEncenacoes({
    jogadas: jogadas.data,
    posicoes: porUsuario,
    centro,
    reduzido,
    onVistas: marcarVistas,
    usuarioId,
    enfeitesNovos: meuAvatar.data?.novos ?? semNovos,
    onEnfeitesVistos: marcarEnfeitesVistos,
    rivais: rivaisEmCena,
    onRivaisVistos: (ids) => {
      const doServidor = ids.filter((id) => !id.startsWith(idLocal));
      if (doServidor.length > 0) marcarRivaisVistos(doServidor);
      if (doServidor.length < ids.length) setEscolhidoAgora(null);
    },
  });

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
              escala={escala}
              reduzido={reduzido}
              centro={centro}
              atuacao={encenacao.atuacoes.get(membro.usuarioId)}
              brilho={encenacao.brilho && membro.usuarioId === usuarioId}
              onToque={() => setTocado(membro)}
            />
          ))}
          {encenacao.carta && (
            <CartaVoando carta={encenacao.carta} escala={escala} />
          )}
          {encenacao.ativa && (
            // Tocar na praça pula a encenação: ela nunca prende a pessoa.
            <button
              type="button"
              className="absolute inset-0 cursor-pointer"
              style={{ zIndex: 1002 }}
              onClick={encenacao.pular}
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
      <RivalDaSemana grupoId={grupoId} />
    </div>
  );
}
