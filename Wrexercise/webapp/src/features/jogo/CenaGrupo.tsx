import { type CSSProperties, useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import { folhaPadrao, hashFnv1a } from "@/components/folhaAvatar";
import { variacoesDeParado, type NomeAnimacao } from "./animacoes";
import { IconeCarta } from "./CartaJogo";
import { MenuAvatar } from "./MenuAvatar";
import { SaldoEnergia } from "./SaldoEnergia";
import { SpriteAvatar } from "./SpriteAvatar";
import { useJogadas, useMarcarJogadasVistas } from "./useCartas";
import { useCena, type Cena, type MembroCena } from "./useCena";
import { type Atuacao, type CartaEmCena, useEncenacoes } from "./useEncenacoes";
import { useMovimentoReduzido } from "./useMovimentoReduzido";

// O chão tem 180×230 px e é mostrado em 2×.
const largura = 360;
const altura = 460;
const centroX = largura / 2;
const raioX = 130;
/** Onde os pés podem pisar: abaixo da mureta e com espaço para o nome embaixo. */
const topoDosPes = 46;
const folgaInferior = 22;

/** Elipse em pé: ocupa a altura toda da praça, deixando espaço para o avatar (31 px de altura × escala) e o nome. */
function getElipse(escala: number) {
  const minY = topoDosPes + 31 * escala;
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

function AvatarCena({
  membro,
  posicao,
  escala,
  reduzido,
  atuacao,
  onToque,
}: {
  membro: MembroCena;
  posicao: Posicao;
  escala: number;
  reduzido: boolean;
  atuacao: Atuacao | undefined;
  onToque: () => void;
}) {
  const [variacao, setVariacao] = useState<NomeAnimacao | null>(null);
  const parado = membro.pose === "parado";

  const sorteio = useMemo(
    () => mulberry32(hashFnv1a(membro.usuarioId)),
    [membro.usuarioId],
  );

  // Quem está parado de vez em quando se alonga ou olha em volta, fora de sincronia com os outros.
  useEffect(() => {
    if (!parado || reduzido) return;
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
  const espelhar = atuacao?.espelhar ?? posicao.x > centroX + 1;
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
          left: posicao.x - 16 * escala,
          top: posicao.y - 31 * escala,
          width: 32 * escala,
          zIndex: Math.round(posicao.y + (atuacao?.dy ?? 0)),
          ...deslocamento,
        }}
      >
        <SpriteAvatar
          key={animacao}
          folha={folhaPadrao(membro.usuarioId)}
          animacao={animacao}
          escala={escala}
          estatico={reduzido}
          espelhar={espelhar}
          onFim={() => setVariacao(null)}
        />
      </button>
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
  // A carta (40 px) fica na altura das mãos, centrada no avatar.
  const acima = 15 * escala + 20;
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
              atuacao={encenacao.atuacoes.get(membro.usuarioId)}
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
      />
    </div>
  );
}
