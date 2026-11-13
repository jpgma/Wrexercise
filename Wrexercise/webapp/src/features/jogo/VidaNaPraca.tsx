import type { CSSProperties } from "react";
import { isLuzAcesa, type PeriodoDoDia } from "./periodoDoDia";
import type { BolaEmCena, FasePombo, PomboEmCena } from "./useVidaNaPraca";

/**
 * Sprites de `ambiente.png` (pixel-art/ambiente/): células de 16 px, mostradas em 2×, 4 colunas e
 * 7 linhas. Linhas 0 a 2: pombo bicando, andando e voando; 3: bola; 4: luminárias; 5: o "Zzz" de
 * quem dorme; 6: a bolha de sonho.
 */
const escala = 2;
const celula = 16 * escala;
const folha = { colunas: 4, linhas: 7 };
/** Cada letra do "Zzz" sobe em 4 quadros. */
const ZzzMs = 450;
/** A bolha de sonho abre em 4 quadros e some no fim deles. */
export const SonhoMs = 700;

const linhaDoPombo: Record<FasePombo, { linha: number; ms: number }> = {
  bicando: { linha: 0, ms: 260 },
  andando: { linha: 1, ms: 120 },
  voando: { linha: 2, ms: 90 },
};

function Quadro({
  linha,
  coluna = 0,
  ms,
  virado = 1,
  atraso = 0,
  uma = false,
  className = "",
}: {
  linha: number;
  coluna?: number;
  /** Com `ms`, anima os 4 quadros da linha; sem, mostra só a coluna. */
  ms?: number;
  virado?: 1 | -1;
  /** Começa a animação já adiantada, em ms. */
  atraso?: number;
  /** Toca os 4 quadros uma vez só. */
  uma?: boolean;
  className?: string;
}) {
  return (
    <span
      aria-hidden="true"
      className={`sprite-avatar ${ms ? (uma ? "sprite-uma-vez" : "sprite-repete") : ""} ${className}`}
      style={
        {
          width: celula,
          height: celula,
          backgroundImage: "url(/ambiente.png)",
          backgroundSize: `${celula * folha.colunas}px ${celula * folha.linhas}px`,
          backgroundPositionX: ms ? undefined : `${-coluna * celula}px`,
          backgroundPositionY: `${-linha * celula}px`,
          transform: virado === -1 ? "scaleX(-1)" : undefined,
          animationDelay: atraso ? `${-atraso}ms` : undefined,
          "--quadros": 4,
          "--ms": `${ms ?? 0}ms`,
          "--celula": `${celula}px`,
        } as CSSProperties
      }
    />
  );
}

/** Os pés do pombo ficam na coluna 7 da célula de 16 px (pixel-art/ambiente/build.py). */
const pesDoPombo = 7 * escala + escala / 2;

export function Pombo({ pombo }: { pombo: PomboEmCena }) {
  const { linha, ms } = linhaDoPombo[pombo.fase];
  const pes = pombo.virado === 1 ? pesDoPombo : celula - pesDoPombo;
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute top-0 left-0"
      style={{
        transform: `translate(${pombo.x - pes}px, ${pombo.y - celula}px)`,
        transition: `transform ${pombo.ms}ms ${pombo.fase === "voando" ? "ease-in-out" : "linear"}`,
        zIndex: Math.round(pombo.y),
      }}
    >
      {pombo.fase !== "voando" && (
        <span
          className="sombra-cena absolute"
          style={{ left: pes - 6, top: celula - 2, width: 12, height: 4 }}
        />
      )}
      <Quadro linha={linha} ms={ms} virado={pombo.virado} />
    </span>
  );
}

export function Bola({ bola }: { bola: BolaEmCena }) {
  const metade = celula / 2;
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute top-0 left-0"
      style={{
        transform: `translate(${bola.x - metade}px, ${bola.y - celula}px)`,
        transition: `transform ${bola.ms}ms ${bola.curva}`,
        zIndex: Math.round(bola.y),
      }}
    >
      <span
        className="sombra-cena absolute"
        style={{ left: metade - 8, top: celula - 3, width: 16, height: 4 }}
      />
      <Quadro
        linha={3}
        ms={bola.rolando ? 90 : undefined}
        virado={bola.virado}
      />
    </span>
  );
}

/**
 * O "Zzz" de quem dorme: duas letras sobem da cabeça, meio ciclo defasadas. Com movimento reduzido,
 * fica uma letra parada. `x`/`y` é o canto de baixo à esquerda, ao lado da cabeça.
 */
export function ZzzSono({
  x,
  y,
  reduzido,
}: {
  x: number;
  y: number;
  reduzido: boolean;
}) {
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute"
      style={{ left: x, top: y - celula }}
    >
      {reduzido ? (
        <Quadro linha={5} coluna={2} />
      ) : (
        <>
          <Quadro linha={5} ms={ZzzMs} />
          <span className="absolute" style={{ left: -6, top: 4 }}>
            <Quadro linha={5} ms={ZzzMs} atraso={2 * ZzzMs} />
          </span>
        </>
      )}
    </span>
  );
}

/** A bolha de sonho, no lugar do "Zzz": abre com um halter, que vira um tênis, e some. */
export function BolhaSonho({ x, y }: { x: number; y: number }) {
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute"
      style={{ left: x, top: y - celula }}
    >
      <Quadro linha={6} ms={SonhoMs} uma />
    </span>
  );
}

/**
 * As luminárias da praça: `x` é o centro e `y` a base, em px da praça; o brilho fica no vidro.
 * As posições acompanham o chão atual (pixel-art/praca/): mude-as se a praça mudar.
 */
const luminarias = [
  // Acima da porta do fundo, sobre a marquise, espelhada: o braço sai da parede à direita e a cúpula
  // fica centrada na porta. As do chão ficam ao lado das floreiras da frente.
  {
    tipo: "parede",
    x: 96,
    y: 66,
    virada: true,
    brilho: { x: 93, y: 56, raio: 48 },
  },
  { tipo: "chao", x: 66, y: 398, brilho: { x: 66, y: 380, raio: 44 } },
  { tipo: "chao", x: 294, y: 398, brilho: { x: 294, y: 380, raio: 44 } },
] as const;

/**
 * A luz do período do dia por cima da cena e as luminárias, que acendem no entardecer. Os nomes dos
 * avatares ficam acima da luz, sempre legíveis.
 */
export function LuzDaPraca({ periodo }: { periodo: PeriodoDoDia }) {
  const acesa = isLuzAcesa(periodo);
  return (
    <>
      {luminarias.map((l) => (
        <span
          key={`${l.x}-${l.y}`}
          aria-hidden="true"
          className="pointer-events-none absolute"
          style={{
            left: l.x - celula / 2,
            top: l.y - celula,
            zIndex: Math.round(l.y),
          }}
        >
          <Quadro
            linha={4}
            coluna={(l.tipo === "parede" ? 0 : 2) + (acesa ? 1 : 0)}
            virado={"virada" in l && l.virada ? -1 : 1}
          />
        </span>
      ))}
      {periodo !== "dia" && (
        <span
          aria-hidden="true"
          className={`luz-praca luz-${periodo} pointer-events-none absolute inset-0`}
          style={{ zIndex: 998 }}
        />
      )}
      {acesa &&
        luminarias.map((l) => (
          <span
            key={`brilho-${l.x}-${l.y}`}
            aria-hidden="true"
            className="brilho-luminaria pointer-events-none absolute rounded-full"
            style={{
              left: l.brilho.x - l.brilho.raio,
              top: l.brilho.y - l.brilho.raio,
              width: l.brilho.raio * 2,
              height: l.brilho.raio * 2,
              zIndex: 999,
            }}
          />
        ))}
    </>
  );
}
