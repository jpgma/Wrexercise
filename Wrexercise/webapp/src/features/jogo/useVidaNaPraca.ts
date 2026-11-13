import { useEffect, useRef, useState } from "react";
import { animacoes, getDirecao, quadroSoltura } from "./animacoes";
import type { Atuacao, Ponto } from "./useEncenacoes";
import type { MembroCena } from "./useCena";

/** As fases do pombo; cada uma tem a sua linha na folha `ambiente.png`. */
export type FasePombo = "voando" | "bicando" | "andando";

/** Um pombo na praça: `x`/`y` são os pés (ou o ponto de pouso, em voo). */
export type PomboEmCena = {
  x: number;
  y: number;
  /** 1 olha para a direita, como na folha; -1 é espelhado. */
  virado: 1 | -1;
  fase: FasePombo;
  /** Duração da transição até `x`/`y`, em ms. */
  ms: number;
};

/** A bola: `x`/`y` é o ponto em que ela toca o chão. */
export type BolaEmCena = {
  x: number;
  y: number;
  /** 1 rola para a direita; -1, para a esquerda. */
  virado: 1 | -1;
  rolando: boolean;
  ms: number;
  curva: "linear" | "ease-out";
};

/**
 * Onde o pombo pousa: fora da elipse dos avatares, no chão livre entre a porta e a barra e perto da
 * borda de baixo, ao lado das floreiras. Acompanha a praça atual (pixel-art/praca/).
 */
const pousos: readonly { x: [number, number]; y: number }[] = [
  { x: [126, 150], y: 168 },
  { x: [96, 128], y: 394 },
  { x: [232, 264], y: 394 },
];
/** A bola entra pela borda de baixo, que é aberta, entre as floreiras dos cantos. */
const MargemDaBola = 60;

const VooMs = 1400;
/** A chance de o pombo pousar ao lado de quem dorme, quando há alguém dormindo. */
const ChanceJuntoDeQuemDorme = 0.6;
/** A distância entre o pombo e os pés de quem dorme, de lado. */
const AoLadoDeQuemDorme = 20;
/** A bola percorre a praça em px por ms. */
const VelocidadeBola = 0.16;
const ConversaMs = 5000;

function sortear(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

/**
 * A vida da praça entre as encenações (docs/gamificacao.md §3.4): de vez em quando um pombo pousa,
 * dois vizinhos conversam ou uma bola rola até alguém, que a devolve. Um evento de cada vez, nunca
 * durante uma encenação, e nada com movimento reduzido. Quem dorme não conversa nem pega a bola, mas
 * o pombo gosta de pousar ao lado dele e chegar perto.
 */
export function useVidaNaPraca({
  membros,
  posicoes,
  ocupados,
  largura,
  altura,
  pausada,
  reduzido,
  comPombos,
}: {
  membros: MembroCena[];
  posicoes: Map<string, Ponto>;
  /** O chão dos Enfeites: o pombo não pousa em cima deles. */
  ocupados: { x0: number; y0: number; x1: number; y1: number }[];
  largura: number;
  altura: number;
  /** Uma encenação ou a entrada está tocando: o pombo voa embora e o resto para. */
  pausada: boolean;
  reduzido: boolean;
  /** À noite não há pombos. */
  comPombos: boolean;
}) {
  const [pombo, setPombo] = useState<PomboEmCena | null>(null);
  const [bola, setBola] = useState<BolaEmCena | null>(null);
  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(new Map());
  const temporizadores = useRef<number[]>([]);
  // A cena muda (poses, membros) sem reiniciar o agendamento.
  const cena = useRef({ membros, posicoes, ocupados, comPombos });
  useEffect(() => {
    cena.current = { membros, posicoes, ocupados, comPombos };
  }, [membros, posicoes, ocupados, comPombos]);
  const saida = useRef<number | undefined>(undefined);

  useEffect(() => {
    // Pausada (uma encenação ou a entrada está tocando) ou sem movimento: nada é agendado. O que
    // estava na praça sai na limpeza do agendamento anterior, mais abaixo.
    if (reduzido || pausada) return;
    const lista = temporizadores.current;
    const depois = (ms: number, executar: () => void) =>
      lista.push(window.setTimeout(executar, ms));
    const limpar = () => {
      lista.forEach((t) => window.clearTimeout(t));
      lista.length = 0;
      // Alguém vai andar pela praça: o pombo levanta voo, e a bola e as conversas param.
      setBola(null);
      setAtuacoes(new Map());
      setPombo((p) =>
        p && p.fase !== "voando"
          ? {
              ...p,
              fase: "voando",
              x: p.x + p.virado * 160,
              y: p.y - 220,
              ms: VooMs,
            }
          : null,
      );
      window.clearTimeout(saida.current);
      saida.current = window.setTimeout(() => setPombo(null), VooMs);
    };

    const parados = () =>
      cena.current.membros
        .filter((m) => m.pose === "parado")
        .map((m) => ({
          id: m.usuarioId,
          p: cena.current.posicoes.get(m.usuarioId),
        }))
        .filter((m): m is { id: string; p: Ponto } => m.p !== undefined);

    const atuar = (id: string, atuacao: Atuacao | null) =>
      setAtuacoes((atual) => {
        const proxima = new Map(atual);
        if (atuacao) proxima.set(id, atuacao);
        else proxima.delete(id);
        return proxima;
      });

    /** Ao lado de alguém que dorme, do lado de fora da elipse, virado para ele. */
    function pousoJuntoDeQuemDorme() {
      const dormindo = cena.current.membros
        .filter((m) => m.pose === "dormindo")
        .map((m) => cena.current.posicoes.get(m.usuarioId))
        .filter((p): p is Ponto => p !== undefined);
      const p = dormindo[Math.floor(Math.random() * dormindo.length)];
      if (!p || Math.random() >= ChanceJuntoDeQuemDorme) return null;
      const lado: 1 | -1 = p.x < largura / 2 ? -1 : 1;
      return {
        x: Math.round(p.x + lado * AoLadoDeQuemDorme),
        y: Math.round(p.y + 2),
        virado: (lado * -1) as 1 | -1,
      };
    }

    function pombear(fim: () => void): boolean {
      const junto = pousoJuntoDeQuemDorme();
      // Um ponto de pouso fora do chão dos Enfeites; com todos ocupados, o pombo não vem.
      const livres = pousos.flatMap((pouso) => {
        const x = Math.round(sortear(...pouso.x));
        const y = pouso.y;
        return cena.current.ocupados.some(
          (o) => x > o.x0 - 12 && x < o.x1 + 12 && y > o.y0 - 8 && y < o.y1 + 8,
        )
          ? []
          : [{ x, y }];
      });
      const pouso = junto ?? livres[Math.floor(Math.random() * livres.length)];
      if (!pouso) return false;
      const { x, y } = pouso;
      const virado: 1 | -1 = junto ? junto.virado : x < largura / 2 ? 1 : -1;
      // Vem de fora da praça, do alto, pelo lado de trás.
      setPombo({
        x: x - virado * 140,
        y: y - 220,
        virado,
        fase: "voando",
        ms: 0,
      });
      depois(30, () => setPombo({ x, y, virado, fase: "voando", ms: VooMs }));
      let t = 30 + VooMs;
      depois(t, () => setPombo({ x, y, virado, fase: "bicando", ms: 0 }));
      t += sortear(3000, 5000);
      // Ao lado de quem dorme, o passo é curto: chega perto sem subir nele.
      const passo =
        Math.round(junto ? sortear(4, 8) : sortear(10, 22)) * virado;
      depois(t, () =>
        setPombo({ x: x + passo, y, virado, fase: "andando", ms: 900 }),
      );
      t += 900;
      depois(t, () =>
        setPombo({ x: x + passo, y, virado, fase: "bicando", ms: 0 }),
      );
      t += sortear(2500, 4500);
      depois(t, () =>
        setPombo({
          x: x + passo + virado * 160,
          y: y - 220,
          virado,
          fase: "voando",
          ms: VooMs,
        }),
      );
      t += VooMs;
      depois(t, () => {
        setPombo(null);
        fim();
      });
      return true;
    }

    function conversar(fim: () => void): boolean {
      const candidatos = parados();
      if (candidatos.length < 2) return false;
      const a = candidatos[Math.floor(Math.random() * candidatos.length)]!;
      const b = candidatos
        .filter((c) => c.id !== a.id)
        .sort(
          (c, d) =>
            Math.hypot(c.p.x - a.p.x, c.p.y - a.p.y) -
            Math.hypot(d.p.x - a.p.x, d.p.y - a.p.y),
        )[0]!;
      const virar = (de: Ponto, para: Ponto): Atuacao => ({
        animacao: "parado",
        dx: 0,
        dy: 0,
        direcao: getDirecao(de, para),
        passoMs: 0,
      });
      atuar(a.id, virar(a.p, b.p));
      atuar(b.id, virar(b.p, a.p));
      // No meio da conversa, um deles olha em volta.
      depois(ConversaMs / 2, () =>
        atuar(b.id, { ...virar(b.p, a.p), animacao: "olhar" }),
      );
      depois(ConversaMs / 2 + 800, () => atuar(b.id, virar(b.p, a.p)));
      depois(ConversaMs, () => {
        atuar(a.id, null);
        atuar(b.id, null);
        fim();
      });
      return true;
    }

    function jogarBola(fim: () => void): boolean {
      const candidatos = parados();
      if (candidatos.length === 0) return false;
      const { id, p } =
        candidatos[Math.floor(Math.random() * candidatos.length)]!;
      const daEsquerda = p.x < largura / 2;
      const lado: 1 | -1 = daEsquerda ? -1 : 1;
      const chegada = { x: p.x + lado * 22, y: p.y + 2 };
      // Vem de baixo e um pouco de fora, na diagonal: as laterais da praça têm alambrado.
      const borda = {
        x: Math.min(
          Math.max(chegada.x + lado * 40, MargemDaBola),
          largura - MargemDaBola,
        ),
        y: altura + 20,
      };
      const ida = Math.round(
        Math.hypot(chegada.x - borda.x, chegada.y - borda.y) / VelocidadeBola,
      );
      const sentidoIda: 1 | -1 = chegada.x >= borda.x ? 1 : -1;

      const olhando: Atuacao = {
        animacao: "parado",
        dx: 0,
        dy: 0,
        direcao: getDirecao(p, borda),
        passoMs: 0,
      };
      setBola({
        ...borda,
        virado: sentidoIda,
        rolando: true,
        ms: 0,
        curva: "linear",
      });
      depois(30, () => {
        atuar(id, olhando);
        setBola({
          ...chegada,
          virado: sentidoIda,
          rolando: true,
          ms: ida,
          curva: "ease-out",
        });
      });
      let t = 30 + ida;
      depois(t, () => {
        setBola({
          ...chegada,
          virado: sentidoIda,
          rolando: false,
          ms: 0,
          curva: "linear",
        });
        atuar(id, { ...olhando, animacao: "arremessar" });
      });
      t += quadroSoltura * animacoes.arremessar.ms;
      const volta = Math.round(ida * 0.7);
      depois(t, () =>
        setBola({
          ...borda,
          virado: (sentidoIda * -1) as 1 | -1,
          rolando: true,
          ms: volta,
          curva: "linear",
        }),
      );
      depois(
        t +
          (animacoes.arremessar.quadros - quadroSoltura) *
            animacoes.arremessar.ms,
        () => atuar(id, null),
      );
      t += volta;
      depois(t, () => {
        setBola(null);
        fim();
      });
      return true;
    }

    function agendar(espera: number) {
      depois(espera, () => {
        if (document.visibilityState !== "visible") {
          agendar(5000);
          return;
        }
        const proximo = () => agendar(sortear(12_000, 25_000));
        const sorteio = Math.random();
        if (cena.current.comPombos && sorteio < 0.45 && pombear(proximo)) {
          // pombo em cena
        } else if (sorteio < 0.8 && conversar(proximo)) {
          // conversa em andamento
        } else if (!jogarBola(proximo) && !conversar(proximo)) {
          proximo();
        }
      });
    }
    agendar(sortear(3000, 6000));
    return limpar;
  }, [pausada, reduzido, largura, altura]);

  return { pombo, bola, atuacoes };
}
