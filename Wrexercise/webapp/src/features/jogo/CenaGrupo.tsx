import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import { folhaPadrao, hashFnv1a } from "@/components/folhaAvatar";
import { variacoesDeParado, type NomeAnimacao } from "./animacoes";
import { SpriteAvatar } from "./SpriteAvatar";
import { useCena, type MembroCena } from "./useCena";
import { useMovimentoReduzido } from "./useMovimentoReduzido";

// O chão tem 180×150 px e é mostrado em 2×.
const largura = 360;
const altura = 300;
const centroX = largura / 2;
const centroY = 185;
const raioX = 130;
/** Achata o círculo numa elipse: dá a sensação de visão de cima, em perspectiva. */
const fatorVertical = 0.6;

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
function posicionar(membros: MembroCena[], usuarioId: string): Posicao[] {
  const n = membros.length;
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
      y: centroY + raioX * fatorVertical * Math.sin(angulo),
    });
  });
  return membros.map(
    (m) => posicoes.get(m.usuarioId) ?? { x: centroX, y: centroY },
  );
}

function AvatarCena({
  grupoId,
  membro,
  posicao,
  escala,
  reduzido,
}: {
  grupoId: string;
  membro: MembroCena;
  posicao: Posicao;
  escala: number;
  reduzido: boolean;
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

  const animacao: NomeAnimacao = parado ? (variacao ?? "parado") : membro.pose;
  const espelhar = posicao.x > centroX + 1;
  const primeiroNome = membro.nomeExibicao.trim().split(/\s+/)[0];

  return (
    <Link
      to={`/grupos/${grupoId}/membros/${membro.usuarioId}`}
      className="absolute flex flex-col items-center"
      style={{
        left: posicao.x - 16 * escala,
        top: posicao.y - 31 * escala,
        width: 32 * escala,
        zIndex: Math.round(posicao.y),
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
      <span className="mt-0.5 rounded bg-surface/85 px-1 text-2xs leading-tight font-semibold whitespace-nowrap text-foreground">
        {primeiroNome}
      </span>
    </Link>
  );
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
        className="mx-auto grid max-w-full place-items-center rounded-xl bg-surface-muted text-sm text-muted"
        style={{ width: largura, height: altura }}
      >
        Carregando a praça…
      </div>
    );
  }

  const { membros } = cena.data;
  const posicoes = posicionar(membros, usuarioId);
  const escala = membros.length <= 5 ? 3 : 2;

  return (
    <section
      aria-label="Praça do grupo"
      className="relative mx-auto max-w-full overflow-hidden rounded-xl"
      style={{
        width: largura,
        height: altura,
        backgroundImage: "url(/praca.png)",
        backgroundSize: `${largura}px ${altura}px`,
        imageRendering: "pixelated",
      }}
    >
      {membros.map((membro, i) => (
        <AvatarCena
          key={membro.usuarioId}
          grupoId={grupoId}
          membro={membro}
          posicao={posicoes[i] ?? { x: centroX, y: centroY }}
          escala={escala}
          reduzido={reduzido}
        />
      ))}
    </section>
  );
}
