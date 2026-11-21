import { useEffect, useMemo, useRef, useState } from "react";
import {
  animacoes,
  type Direcao,
  getDirecao,
  type NomeAnimacao,
  quadroSoltura,
} from "./animacoes";
import type { EnfeiteColocado } from "./enfeitesNaPraca";
import { objetosPraca } from "./ObjetosPraca";
import type { MembroCena } from "./useCena";
import type { Atuacao, Ponto } from "./useEncenacoes";
import type { BolaEmCena } from "./useVidaNaPraca";

/**
 * O treino ao vivo na praça (docs/decisoes/0082-esportes-e-animacoes-por-tipo.md): quem treina usa o
 * Enfeite do tipo que está no chão, dá voltas, reveza na barra ou troca passes. Sem nada disso, o
 * Avatar treina no lugar dele, com a animação do tipo (getAnimacaoDoTreino), e este hook não faz nada.
 */

/** A praça é mostrada em 2×: tudo aqui está em px da praça mostrada. */
const escala = 2;
/** O passo de quem vai até um lugar, em px por ms. */
const VelocidadeAndar = 0.06;
/** As voltas: a velocidade de cada jeito de dar a volta, em px por ms. */
const VelocidadeVolta: Record<"correr" | "andar" | "ciclismo", number> = {
  correr: 0.09,
  andar: 0.04,
  ciclismo: 0.13,
};
const PontosPorVolta = 24;
/** Cada turno na barra dura 20 segundos (ADR 0082). */
export const TurnoBarraMs = 20_000;
/** A bola dos passes, em px por ms: a de futebol rola, a de vôlei voa num arco. */
const VelocidadeBolaChao = 0.22;
const VelocidadeBolaAr = 0.16;
const ArcoVolei = -44;
const PausaEntrePasses = 500;
/** As linhas das bolas em `ambiente.png` (pixel-art/ambiente/build.py). */
const LinhaFutebol = 7;
const LinhaVolei = 8;

/** A barra da praça (ObjetosPraca): o pé dela, onde ficam os pés de quem a usa. */
const barra = (() => {
  const o = objetosPraca.find((objeto) => objeto.nome === "barra")!;
  return {
    x: (o.x + o.largura / 2) * escala,
    y: (o.y + o.altura) * escala,
  };
})();
/**
 * Os pés de quem está pendurado: a trave fica nas linhas 4 a 6 do recorte da barra (40 px), e as mãos
 * da animação `barra`, 26 a 28 px acima da âncora da folha. Por isso os pés sobem 8 px do mestre.
 */
const naTrave = { x: barra.x, y: barra.y - 8 * escala };
/** À frente da barra, quem espera a vez faz flexões, lado a lado. */
const EsperaDaBarra = { dy: 26, entre: 34 };
/**
 * As flexões nas paralelas, em px da praça: na direção `sudoeste`, as mãos ficam uns 10 px do mestre à
 * esquerda da âncora, e as pegadas, uns 12 px acima da base do Enfeite. Os pés, mais acima, ficam atrás.
 */
const MaosNasParalelas = { dx: 10, dy: 28 };

/** A pista das voltas: uma elipse por fora da dos avatares. */
export type Pista = { cx: number; cy: number; rx: number; ry: number };

export type TreinoNaPraca = {
  atuacoes: Map<string, Atuacao>;
  /** As chaves dos Enfeites que saem do chão enquanto alguém os usa (a bicicleta). */
  ocultos: Set<string>;
  bolas: BolaEmCena[];
};

type Membro = { usuarioId: string; tipo: string; inicio: number };

/** Os Enfeites de cada tipo, pelo código (Features/Jogo/Enfeites.cs). Os 3 últimos ainda não têm arte. */
const EnfeiteDoTipo: Record<string, string> = {
  Musculacao: "haltere",
  Corrida: "largada",
  Ciclismo: "bicicleta",
  Natacao: "piscina",
  Yoga: "tapete",
  Calistenia: "paralelas",
  Esporte: "golzinho",
  Futebol: "cones",
  Volei: "rede",
  Luta: "saco",
};

/** O Enfeite do tipo do treino, se a pessoa o tem no chão agora (e não só no Foco). */
export function getEnfeiteDoTreino(
  enfeites: EnfeiteColocado[],
  usuarioId: string,
  tipo: string,
): EnfeiteColocado | undefined {
  const codigo = EnfeiteDoTipo[tipo];
  return enfeites.find(
    (e) => e.usuarioId === usuarioId && !e.extra && e.enfeite.codigo === codigo,
  );
}

/**
 * Quem está na barra neste turno: os turnos seguem a ordem de quem começou primeiro. Sozinho, a pessoa
 * alterna: um turno na barra, um no chão.
 */
export function getNaBarra(fila: string[], agora: number): string | null {
  if (fila.length === 0) return null;
  const turno = Math.floor(agora / TurnoBarraMs);
  if (fila.length === 1) return turno % 2 === 0 ? fila[0]! : null;
  return fila[turno % fila.length]!;
}

/** Um ponto da pista no ângulo dado (radianos; 0 à direita, crescendo no sentido horário na tela). */
export function getPontoNaPista(pista: Pista, angulo: number): Ponto {
  return {
    x: pista.cx + pista.rx * Math.cos(angulo),
    y: pista.cy + pista.ry * Math.sin(angulo),
  };
}

/** O perímetro da elipse (Ramanujan). */
function getPerimetro({ rx, ry }: Pista): number {
  return Math.PI * (3 * (rx + ry) - Math.sqrt((3 * rx + ry) * (rx + 3 * ry)));
}

/** O lado esquerdo e o lado direito de um Enfeite, em px da praça. */
function getLados(e: EnfeiteColocado) {
  const meia = (e.desenho.largura * escala) / 2;
  return { esquerda: e.x - meia, direita: e.x + meia };
}

function distancia(a: Ponto, b: Ponto): number {
  return Math.hypot(b.x - a.x, b.y - a.y);
}

/** Uma chave que só muda quando muda quem treina, onde está ou que Enfeite tem no chão. */
function getChave(
  membros: Membro[],
  posicoes: Map<string, Ponto>,
  enfeites: EnfeiteColocado[],
  pista: Pista,
): string {
  const pessoas = membros.map((m) => {
    const p = posicoes.get(m.usuarioId);
    return `${m.usuarioId}:${m.tipo}:${m.inicio}:${Math.round(p?.x ?? 0)},${Math.round(p?.y ?? 0)}`;
  });
  const chaos = enfeites
    .filter((e) => !e.extra)
    .map((e) => `${e.chave}@${Math.round(e.x)},${Math.round(e.y)}`);
  return [...pessoas, ...chaos, `${pista.rx},${pista.ry}`].join("|");
}

export function useTreinoNaPraca({
  membros,
  posicoes,
  enfeites,
  pista,
  pausado,
  reduzido,
}: {
  membros: MembroCena[];
  posicoes: Map<string, Ponto>;
  enfeites: EnfeiteColocado[];
  pista: Pista;
  /** A entrada, o Foco ou a Chegada: todos voltam ao lugar e esperam. */
  pausado: boolean;
  reduzido: boolean;
}): TreinoNaPraca {
  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(new Map());
  const [ocultos, setOcultos] = useState<Set<string>>(new Set());
  const [bolas, setBolas] = useState<Map<string, BolaEmCena>>(new Map());

  const aoVivo = useMemo(
    () =>
      membros.flatMap((m) =>
        m.treinoAoVivo
          ? [
              {
                usuarioId: m.usuarioId,
                tipo: m.treinoAoVivo.tipo,
                inicio: new Date(m.treinoAoVivo.startedAt).getTime(),
              },
            ]
          : [],
      ),
    [membros],
  );
  const chave = getChave(aoVivo, posicoes, enfeites, pista);
  // O agendamento lê a cena atual sem recomeçar a cada render: só a chave o reinicia.
  const cena = useRef({ aoVivo, posicoes, enfeites, pista });
  useEffect(() => {
    cena.current = { aoVivo, posicoes, enfeites, pista };
  }, [aoVivo, posicoes, enfeites, pista]);

  useEffect(() => {
    if (pausado || reduzido) return;
    const { aoVivo, posicoes, enfeites, pista } = cena.current;
    if (aoVivo.length === 0) return;
    const temporizadores: number[] = [];
    const depois = (ms: number, executar: () => void) =>
      temporizadores.push(window.setTimeout(executar, Math.max(0, ms)));
    /** Onde cada um está agora: começa no lugar da elipse e acompanha cada passo. */
    const onde = new Map<string, Ponto>();

    const atuar = (
      id: string,
      para: Ponto,
      animacao: NomeAnimacao,
      direcao: Direcao,
      passoMs: number,
      z?: number,
    ) => {
      const casa = posicoes.get(id);
      if (!casa) return;
      onde.set(id, para);
      setAtuacoes((atual) =>
        new Map(atual).set(id, {
          animacao,
          dx: para.x - casa.x,
          dy: para.y - casa.y,
          direcao,
          passoMs,
          aoVivo: true,
          z,
        }),
      );
    };
    const getOnde = (id: string) => onde.get(id) ?? posicoes.get(id)!;
    /** Vai andando até `para` e, ao chegar, chama `chegou`. Devolve quanto demora. */
    const irAte = (id: string, para: Ponto, chegou: () => void): number => {
      const de = getOnde(id);
      const ms = Math.round(distancia(de, para) / VelocidadeAndar);
      if (ms < 60) {
        chegou();
        return 0;
      }
      atuar(id, para, "andar", getDirecao(de, para), ms);
      depois(ms, chegou);
      return ms;
    };
    const ocultar = (chaveEnfeite: string) =>
      setOcultos((atual) => new Set(atual).add(chaveEnfeite));
    const porBola = (chaveBola: string, bola: BolaEmCena | null) =>
      setBolas((atual) => {
        const nova = new Map(atual);
        if (bola) nova.set(chaveBola, bola);
        else nova.delete(chaveBola);
        return nova;
      });

    const porTipo = (tipo: string) =>
      aoVivo
        .filter((m) => m.tipo === tipo && posicoes.has(m.usuarioId))
        .sort((a, b) => a.inicio - b.inicio);

    // Voltas: Corrida (largando do bloco, se houver), Caminhada e Ciclismo (na bicicleta, se houver).
    const darVoltas = (
      id: string,
      animacao: "correr" | "andar" | "ciclismo",
    ) => {
      const casa = posicoes.get(id)!;
      const inicio = Math.atan2(
        (casa.y - pista.cy) / pista.ry,
        (casa.x - pista.cx) / pista.rx,
      );
      const passoMs = Math.round(
        getPerimetro(pista) / VelocidadeVolta[animacao] / PontosPorVolta,
      );
      const passo = (k: number) => {
        const de = getOnde(id);
        const para = getPontoNaPista(
          pista,
          inicio + (2 * Math.PI * (k + 1)) / PontosPorVolta,
        );
        atuar(id, para, animacao, getDirecao(de, para), passoMs);
        depois(passoMs, () => passo((k + 1) % PontosPorVolta));
      };
      // Da elipse até a pista, no ritmo da volta.
      const entrada = getPontoNaPista(pista, inicio);
      const ms = Math.round(
        distancia(getOnde(id), entrada) / VelocidadeVolta[animacao],
      );
      atuar(id, entrada, animacao, getDirecao(getOnde(id), entrada), ms);
      depois(ms, () => passo(0));
    };

    for (const m of porTipo("Corrida")) {
      const bloco = getEnfeiteDoTreino(enfeites, m.usuarioId, m.tipo);
      if (!bloco) {
        darVoltas(m.usuarioId, "correr");
        continue;
      }
      // Agacha atrás do bloco, de lado, e sai correndo para a pista.
      const atras = { x: getLados(bloco).esquerda - 4, y: bloco.y + 1 };
      irAte(m.usuarioId, atras, () => {
        atuar(m.usuarioId, atras, "largada", "leste", 0);
        depois(animacoes.largada.quadros * animacoes.largada.ms, () =>
          darVoltas(m.usuarioId, "correr"),
        );
      });
    }
    // O cachorro andando ainda não tem arte (pixel-art/enfeites/prompts-chatgpt.md, prompt 4).
    for (const m of porTipo("Caminhada")) darVoltas(m.usuarioId, "andar");
    for (const m of porTipo("Ciclismo")) {
      const bicicleta = getEnfeiteDoTreino(enfeites, m.usuarioId, m.tipo);
      if (!bicicleta) {
        darVoltas(m.usuarioId, "ciclismo");
        continue;
      }
      irAte(m.usuarioId, { x: bicicleta.x, y: bicicleta.y + 1 }, () => {
        ocultar(bicicleta.chave);
        darVoltas(m.usuarioId, "ciclismo");
      });
    }

    // No lugar do Enfeite: vai até ele e treina ali.
    const noEnfeite = (
      tipo: string,
      animacao: NomeAnimacao,
      lugar: (e: EnfeiteColocado) => {
        ponto: Ponto;
        direcao: Direcao;
        z?: number;
      },
    ) => {
      for (const m of porTipo(tipo)) {
        const enfeite = getEnfeiteDoTreino(enfeites, m.usuarioId, m.tipo);
        if (!enfeite) continue;
        const { ponto, direcao, z } = lugar(enfeite);
        irAte(m.usuarioId, ponto, () =>
          atuar(m.usuarioId, ponto, animacao, direcao, 0, z),
        );
      }
    };
    noEnfeite("Musculacao", "musculacao", (e) => ({
      ponto: { x: getLados(e).direita + 14, y: e.y + 2 },
      direcao: "sul",
    }));
    // Dentro da piscina: o Avatar fica na frente dela, e a água desenhada na animação cai sobre a da piscina.
    noEnfeite("Natacao", "natacao", (e) => ({
      ponto: { x: e.x, y: e.y - (e.desenho.altura * escala) / 3 },
      direcao: "sul",
      z: Math.round(e.y) + 1,
    }));
    // Em cima do tapete e das paralelas: na frente deles, mesmo com os pés sobre o desenho.
    noEnfeite("Yoga", "yoga", (e) => ({
      ponto: { x: e.x, y: e.y - 6 },
      direcao: "sul",
      z: Math.round(e.y) + 1,
    }));
    noEnfeite("Luta", "luta", (e) => ({
      ponto: { x: getLados(e).esquerda - 12, y: e.y + 1 },
      direcao: "leste",
    }));

    // Calistenia: um de cada vez na barra; quem espera faz flexões nas paralelas ou à frente da barra.
    const calistenia = porTipo("Calistenia").map((m) => m.usuarioId);
    if (calistenia.length > 0) {
      const turno = () => {
        const agora = Date.now();
        const naBarra = getNaBarra(calistenia, agora);
        const esperando = calistenia.filter((id) => id !== naBarra);
        calistenia.forEach((id) => {
          if (id === naBarra) {
            // Pendurado, fica atrás da barra, com as mãos na trave.
            irAte(id, barra, () =>
              atuar(id, naTrave, "barra", "sul", 0, barra.y - 1),
            );
            return;
          }
          const paralelas = getEnfeiteDoTreino(enfeites, id, "Calistenia");
          const i = esperando.indexOf(id);
          // Nas paralelas, olha para o sudoeste, de través nas duas barras, com as mãos nas pegadas.
          const ponto = paralelas
            ? {
                x: paralelas.x + MaosNasParalelas.dx,
                y: paralelas.y - MaosNasParalelas.dy,
              }
            : {
                x:
                  barra.x +
                  (i - (esperando.length - 1) / 2) * EsperaDaBarra.entre,
                y: barra.y + EsperaDaBarra.dy,
              };
          const z = paralelas ? Math.round(paralelas.y) + 1 : undefined;
          const direcao: Direcao = paralelas ? "sudoeste" : "sul";
          irAte(id, ponto, () => atuar(id, ponto, "calistenia", direcao, 0, z));
        });
        depois(TurnoBarraMs - (agora % TurnoBarraMs), turno);
      };
      turno();
    }

    // Passes: 2 ou mais no Futebol, ou no Vôlei, trocam a bola em roda, na ordem de quem começou.
    const trocarPasses = (
      ids: string[],
      esporte: "futebol" | "volei",
      lugares: Map<string, Ponto>,
    ) => {
      const chute: NomeAnimacao = esporte === "futebol" ? "chutar" : "manchete";
      // O pé (no futebol) ou os braços (no vôlei) tocam a bola neste quadro.
      const contato =
        (esporte === "futebol" ? quadroSoltura : 1) * animacoes[chute].ms;
      const chaveBola = `passe-${esporte}`;
      const passar = (i: number) => {
        const de = ids[i]!;
        const para = ids[(i + 1) % ids.length]!;
        const a = lugares.get(de)!;
        const b = lugares.get(para)!;
        ids.forEach((id) => {
          const aqui = lugares.get(id)!;
          if (id === de) atuar(id, aqui, chute, getDirecao(a, b), 0);
          else atuar(id, aqui, "parado", getDirecao(aqui, a), 0);
        });
        const virado: 1 | -1 = b.x >= a.x ? 1 : -1;
        const linha = esporte === "futebol" ? LinhaFutebol : LinhaVolei;
        // A bola fica no lado para onde cada um olha: de costas (passe para o norte), atrás do corpo.
        const d = distancia(a, b) || 1;
        const passo = { x: ((b.x - a.x) / d) * 6, y: ((b.y - a.y) / d) * 6 };
        porBola(chaveBola, {
          x: a.x + passo.x,
          y: a.y + passo.y,
          virado,
          rolando: false,
          ms: 0,
          curva: "linear",
          linha,
        });
        const voo = Math.round(
          distancia(a, b) /
            (esporte === "futebol" ? VelocidadeBolaChao : VelocidadeBolaAr),
        );
        depois(contato, () =>
          porBola(chaveBola, {
            x: b.x - passo.x,
            y: b.y - passo.y,
            virado,
            rolando: esporte === "futebol",
            ms: voo,
            curva: esporte === "futebol" ? "ease-out" : "linear",
            linha,
            arco: esporte === "volei" ? ArcoVolei : undefined,
          }),
        );
        depois(contato + voo + PausaEntrePasses, () =>
          passar((i + 1) % ids.length),
        );
      };
      // Todos vão para o lugar do passe; o primeiro chute sai quando o último chegar.
      let chegada = 0;
      ids.forEach((id) => {
        const lugar = lugares.get(id)!;
        chegada = Math.max(
          chegada,
          irAte(id, lugar, () =>
            atuar(
              id,
              lugar,
              "parado",
              getDirecao(lugar, { x: pista.cx, y: pista.cy }),
              0,
            ),
          ),
        );
      });
      depois(chegada + 100, () => passar(0));
    };

    const futebol = porTipo("Futebol").map((m) => m.usuarioId);
    if (futebol.length >= 2) {
      trocarPasses(
        futebol,
        "futebol",
        new Map(futebol.map((id) => [id, posicoes.get(id)!])),
      );
    } else {
      noEnfeite("Futebol", "futebol", (e) => ({
        ponto: { x: getLados(e).direita + 12, y: e.y + 2 },
        direcao: "sul",
      }));
    }

    const volei = porTipo("Volei").map((m) => m.usuarioId);
    if (volei.length >= 2) {
      // Com a redinha de alguém no chão, cada um fica de um lado dela; sem, cada um no seu lugar.
      const rede = volei
        .map((id) => getEnfeiteDoTreino(enfeites, id, "Volei"))
        .find(Boolean);
      const lugares = new Map(
        volei.map((id, i) => {
          if (!rede) return [id, posicoes.get(id)!];
          const lado = i % 2 === 0 ? -1 : 1;
          const fileira = Math.floor(i / 2);
          return [
            id,
            {
              x: rede.x + lado * ((rede.desenho.largura * escala) / 2 + 16),
              y: rede.y + 4 + fileira * 16,
            },
          ];
        }),
      );
      trocarPasses(volei, "volei", lugares);
    } else {
      noEnfeite("Volei", "volei", (e) => ({
        ponto: { x: getLados(e).direita + 14, y: e.y + 2 },
        direcao: "sul",
      }));
    }

    // Outro esporte com o golzinho: chuta a bola no gol, ela volta, e de novo.
    for (const m of porTipo("Esporte")) {
      const gol = getEnfeiteDoTreino(enfeites, m.usuarioId, m.tipo);
      if (!gol) continue;
      const id = m.usuarioId;
      // Na diagonal do gol, para não esconder a bola entrando: chuta para o noroeste ou o nordeste.
      const lado = gol.x < pista.cx ? 1 : -1;
      const marca = { x: gol.x + lado * 30, y: gol.y + 26 };
      const mira: Direcao = lado === 1 ? "noroeste" : "nordeste";
      const fundo = { x: gol.x, y: gol.y - 6 };
      const chaveBola = `gol-${id}`;
      const chutar = () => {
        atuar(id, marca, "chutar", mira, 0);
        porBola(chaveBola, {
          x: marca.x,
          y: marca.y - 4,
          virado: 1,
          rolando: false,
          ms: 0,
          curva: "linear",
          linha: LinhaFutebol,
        });
        const contato = quadroSoltura * animacoes.chutar.ms;
        const ida = Math.round(distancia(marca, fundo) / VelocidadeBolaChao);
        depois(contato, () =>
          porBola(chaveBola, {
            ...fundo,
            virado: 1,
            rolando: true,
            ms: ida,
            curva: "ease-out",
            linha: LinhaFutebol,
          }),
        );
        depois(animacoes.chutar.quadros * animacoes.chutar.ms, () =>
          atuar(id, marca, "comemorando", "sul", 0),
        );
        // Ela volta rolando devagar até o pé, e o chute recomeça.
        const volta = ida * 2;
        depois(contato + ida + 900, () =>
          porBola(chaveBola, {
            x: marca.x,
            y: marca.y - 4,
            virado: -1,
            rolando: true,
            ms: volta,
            curva: "ease-out",
            linha: LinhaFutebol,
          }),
        );
        depois(contato + ida + 900 + volta + 400, chutar);
      };
      irAte(id, marca, chutar);
    }

    return () => {
      temporizadores.forEach((t) => window.clearTimeout(t));
      setAtuacoes(new Map());
      setOcultos(new Set());
      setBolas(new Map());
    };
  }, [chave, pausado, reduzido]);

  const listaBolas = useMemo(() => [...bolas.values()], [bolas]);
  return { atuacoes, ocultos, bolas: listaBolas };
}
