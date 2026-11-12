import type { Enfeite } from "@/components/folhaAvatar";
import { objetosPraca, peDaMureta, vaoDaPorta } from "./ObjetosPraca";
import type { Ponto } from "./useEncenacoes";

/**
 * O atlas `enfeites.png` (pixel-art/enfeites/build.py, que imprime esta lista): uma linha por Enfeite,
 * 4 quadros de 40×40, com o objeto apoiado na base da célula. `esquerda`, `largura` e `altura` são a
 * caixa do objeto na célula, em px do mestre 1×. Se a arte mudar, rode o build.py e cole a lista nova.
 */
const atlas = {
  sequencia4: { linha: 0, esquerda: 13, largura: 13, altura: 15, ms: 320 },
  sequencia8: { linha: 1, esquerda: 11, largura: 17, altura: 26, ms: 320 },
  sequencia12: { linha: 2, esquerda: 7, largura: 25, altura: 32, ms: 320 },
  treinos10: { linha: 3, esquerda: 13, largura: 13, altura: 16, ms: 260 },
  treinos50: { linha: 4, esquerda: 12, largura: 15, altura: 18, ms: 260 },
  treinos100: { linha: 5, esquerda: 12, largura: 16, altura: 20, ms: 260 },
  desafiocompleto: { linha: 6, esquerda: 10, largura: 20, altura: 26, ms: 260 },
  ratodeacademia: { linha: 7, esquerda: 5, largura: 30, altura: 23, ms: 260 },
  corredor: { linha: 8, esquerda: 2, largura: 36, altura: 16, ms: 220 },
  andarilho: { linha: 9, esquerda: 10, largura: 20, altura: 24, ms: 200 },
  ciclista: { linha: 10, esquerda: 0, largura: 40, altura: 25, ms: 160 },
  peixe: { linha: 11, esquerda: 4, largura: 31, altura: 20, ms: 260 },
  boleiro: { linha: 12, esquerda: 3, largura: 34, altura: 23, ms: 240 },
  zen: { linha: 13, esquerda: 3, largura: 34, altura: 18, ms: 360 },
  pesodocorpo: { linha: 14, esquerda: 3, largura: 34, altura: 20, ms: 260 },
  madrugador: { linha: 15, esquerda: 7, largura: 25, altura: 25, ms: 240 },
  noturno: { linha: 16, esquerda: 15, largura: 10, altura: 33, ms: 140 },
  fimdesemana: { linha: 17, esquerda: 10, largura: 19, altura: 29, ms: 150 },
  ecletico: { linha: 18, esquerda: 4, largura: 31, altura: 21, ms: 200 },
} as const satisfies Record<string, DesenhoEnfeite>;

export type DesenhoEnfeite = {
  linha: number;
  esquerda: number;
  largura: number;
  altura: number;
  ms: number;
};

/** O lado da célula do atlas e a quantidade de quadros por linha. */
export const atlasEnfeites = { celula: 40, quadros: 4, linhas: 19 } as const;

/** O desenho de um Enfeite pela conquista do nível que aparece; `undefined` se o atlas não o tiver. */
export function getDesenhoEnfeite(
  conquista: string,
): DesenhoEnfeite | undefined {
  return (atlas as Record<string, DesenhoEnfeite>)[conquista.toLowerCase()];
}

/** O poste acende como as luminárias: o quadro 1 é o poste apagado, e os quadros 2 a 4, aceso. */
export const enfeiteAceso = "noturno";

/** Um Enfeite no chão da praça: `x` é o centro do objeto, e `y` a base, em px da praça. */
export type EnfeiteColocado = {
  chave: string;
  usuarioId: string;
  enfeite: Enfeite;
  desenho: DesenhoEnfeite;
  x: number;
  y: number;
  /** Só aparece agora: no Foco do dono ou como Enfeite novo. Ele surge no chão. */
  extra: boolean;
};

type Caixa = { x0: number; y0: number; x1: number; y1: number };

/** A praça é mostrada em 2×. */
const escala = 2;
/** Distância entre os pés do dono e a base do Enfeite. */
const Afastamento = 26 * escala;
/**
 * Os ângulos que cada vaga tenta, a partir da direção do dono: as duas primeiras em diagonal, a
 * terceira bem atrás. Sem chão livre, a vaga tenta o outro lado e ângulos mais abertos.
 */
const AngulosDasVagas = [
  [40, -40, 0, 75, -75],
  [-40, 40, 0, -75, 75],
  [0, 75, -75, 40, -40],
].map((angulos) => angulos.map((g) => (g * Math.PI) / 180));
/** Tentativas de cada vaga quando a primeira posição está ocupada: mais longe, depois mais perto. */
const Recuos = [0, 14, 28, -10];
/** Altura da faixa de chão que um Enfeite ocupa: dois Enfeites nunca dividem o mesmo chão. */
const FaixaDeChao = 10 * escala;
/**
 * A cabeça e o tronco de um avatar (px da praça), acima dos pés: um Enfeite na frente de alguém pode
 * cobrir as pernas, nunca isso.
 */
const corpo = {
  meiaLargura: 12 * escala,
  topo: 36 * escala,
  cintura: 14 * escala,
};
const MargemLateral = 18;
/** O caminho de quem sai pela porta na entrada do dia fica livre. */
const caminhoDaPorta: Caixa = {
  x0: vaoDaPorta.x - 32,
  y0: peDaMureta - 4,
  x1: vaoDaPorta.x + 32,
  y1: vaoDaPorta.y + 30,
};
/** O chão sob os objetos da praça; atrás deles, a ordem pela base já esconde o que fica atrás. */
const obstaculos: Caixa[] = [
  caminhoDaPorta,
  ...objetosPraca.map((o) => ({
    x0: o.x * escala,
    y0: (o.y + o.altura) * escala - FaixaDeChao,
    x1: (o.x + o.largura) * escala,
    y1: (o.y + o.altura) * escala,
  })),
];

/** Quantos Enfeites cada pessoa mostra: com mais gente na praça, menos objetos por pessoa. */
export function getEnfeitesPorPessoa(membros: number): number {
  if (membros <= 3) return 3;
  if (membros <= 5) return 2;
  return 1;
}

function cruza(a: Caixa, b: Caixa): boolean {
  return a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;
}

/** A fração de `a` que fica sob `b`. */
function getCoberto(a: Caixa, b: Caixa): number {
  const w = Math.min(a.x1, b.x1) - Math.max(a.x0, b.x0);
  const h = Math.min(a.y1, b.y1) - Math.max(a.y0, b.y0);
  if (w <= 0 || h <= 0) return 0;
  return (w * h) / ((a.x1 - a.x0) * (a.y1 - a.y0));
}

/** A direção do cantinho do dono: para fora da elipse atrás; para o lado na metade da frente. */
function getDirecaoDoCantinho(posicao: Ponto, centro: Ponto): Ponto {
  const dx = posicao.x - centro.x;
  const dy = posicao.y - centro.y;
  if (dy > 0 || Math.hypot(dx, dy) < 1) return { x: dx < 0 ? -1 : 1, y: 0 };
  const d = Math.hypot(dx, dy);
  return { x: dx / d, y: dy / d };
}

/**
 * Onde cada Enfeite fica no chão (docs/gamificacao.md §3.1): no cantinho do dono, fora da elipse,
 * sem cobrir ninguém, sem dividir o chão com outro Enfeite e longe da porta, da barra e das floreiras.
 * As vagas são preenchidas por ordem (a primeira de todos, depois a segunda...), de quem está na frente
 * para quem está atrás: quando falta chão, quem cede é a vaga maior de quem está mais atrás.
 * `extras` mostra mais Enfeites de uma pessoa (o Foco: todos; o Enfeite novo: os códigos dados), sem
 * mudar o lugar dos que já estão no chão.
 */
export function posicionarEnfeites({
  membros,
  posicoes,
  centro,
  vitrines,
  largura,
  altura,
  extras,
}: {
  membros: { usuarioId: string }[];
  posicoes: Map<string, Ponto>;
  centro: Ponto;
  vitrines: Map<string, Enfeite[]>;
  largura: number;
  altura: number;
  extras: Map<string, "todos" | string[]>;
}): EnfeiteColocado[] {
  const porPessoa = getEnfeitesPorPessoa(membros.length);
  const corpos: { pes: Ponto; caixa: Caixa }[] = [];
  for (const m of membros) {
    const p = posicoes.get(m.usuarioId);
    if (!p) continue;
    corpos.push({
      pes: p,
      caixa: {
        x0: p.x - corpo.meiaLargura,
        y0: p.y - corpo.topo,
        x1: p.x + corpo.meiaLargura,
        y1: p.y - corpo.cintura,
      },
    });
  }
  const pessoas = membros
    .flatMap((m) => {
      const p = posicoes.get(m.usuarioId);
      return p ? [{ usuarioId: m.usuarioId, posicao: p }] : [];
    })
    .sort((a, b) => b.posicao.y - a.posicao.y);
  const chaos: Caixa[] = [];
  const colocados: EnfeiteColocado[] = [];

  const colocar = (
    usuarioId: string,
    posicao: Ponto,
    enfeite: Enfeite,
    vaga: number,
    extra: boolean,
  ) => {
    const desenho = getDesenhoEnfeite(enfeite.conquista);
    if (!desenho) return;
    const meia = (desenho.largura * escala) / 2;
    const alto = desenho.altura * escala;
    const base = getDirecaoDoCantinho(posicao, centro);
    for (const angulo of AngulosDasVagas[vaga] ?? AngulosDasVagas[0]!) {
      const cos = Math.cos(angulo);
      const sen = Math.sin(angulo);
      const direcao = {
        x: base.x * cos - base.y * sen,
        y: base.x * sen + base.y * cos,
      };
      for (const recuo of Recuos) {
        // De lado, o Enfeite também precisa sair de baixo do corpo do dono.
        const distancia = Afastamento + recuo + Math.abs(direcao.x) * meia;
        const x = Math.round(
          Math.min(
            Math.max(posicao.x + direcao.x * distancia, MargemLateral + meia),
            largura - MargemLateral - meia,
          ),
        );
        const y = Math.round(
          Math.min(
            Math.max(posicao.y + direcao.y * distancia, peDaMureta + 6),
            altura - 2,
          ),
        );
        const chao: Caixa = {
          x0: x - meia,
          y0: y - FaixaDeChao,
          x1: x + meia,
          y1: y,
        };
        const inteiro: Caixa = { ...chao, y0: y - alto };
        const livre =
          !obstaculos.some((o) => cruza(chao, o)) &&
          !chaos.some((c) => cruza(chao, c)) &&
          // Na frente de alguém (base mais abaixo que os pés), não cobre a cabeça nem o tronco.
          !corpos.some((c) =>
            y > c.pes.y - 6
              ? cruza(inteiro, c.caixa)
              : // Atrás de alguém, ao menos metade do Enfeite aparece.
                getCoberto(inteiro, { ...c.caixa, y1: c.pes.y }) > 0.5,
          );
        if (livre) {
          chaos.push(chao);
          colocados.push({
            chave: `${usuarioId}:${enfeite.codigo}`,
            usuarioId,
            enfeite,
            desenho,
            x,
            y,
            extra,
          });
          return;
        }
      }
    }
  };

  for (let vaga = 0; vaga < porPessoa; vaga++) {
    for (const { usuarioId, posicao } of pessoas) {
      const enfeite = vitrines.get(usuarioId)?.[vaga];
      if (enfeite) colocar(usuarioId, posicao, enfeite, vaga, false);
    }
  }
  for (const { usuarioId, posicao } of pessoas) {
    const quais = extras.get(usuarioId);
    if (!quais) continue;
    (vitrines.get(usuarioId) ?? []).forEach((enfeite, vaga) => {
      const mostrado = colocados.some(
        (c) => c.usuarioId === usuarioId && c.enfeite.codigo === enfeite.codigo,
      );
      if (!mostrado && (quais === "todos" || quais.includes(enfeite.codigo)))
        colocar(usuarioId, posicao, enfeite, vaga, true);
    });
  }
  return colocados;
}

/** A faixa de chão dos Enfeites colocados: o pombo não pousa nelas. */
export function getChaoDosEnfeites(enfeites: EnfeiteColocado[]): Caixa[] {
  return enfeites.map((e) => {
    const meia = (e.desenho.largura * escala) / 2;
    return { x0: e.x - meia, y0: e.y - FaixaDeChao, x1: e.x + meia, y1: e.y };
  });
}
