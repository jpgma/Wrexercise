import { tipoAtividadeLabels, type TipoAtividade } from "@/features/atividades";
import { formatarDuracao } from "./datas";

/** Uma alternativa à duração mínima do desafio: a soma das atividades destes tipos num dia. */
export type MetaPorTipo = {
  id: string;
  nome: string | null;
  tipos: TipoAtividade[];
  duracaoMinimaSegundos: number;
};

type MetaDoDesafio = {
  duracaoMinimaSegundos: number;
  metasPorTipo: MetaPorTipo[];
};

/** O nome dado pelo criador ou, sem nome, os tipos: "Corrida + Caminhada". */
export function getRotuloMeta(meta: Pick<MetaPorTipo, "nome" | "tipos">) {
  return meta.nome ?? meta.tipos.map((t) => tipoAtividadeLabels[t]).join(" + ");
}

/** "60 min por dia · ou 20 min de Corrida · ou 30 min de Cardio". */
export function formatarMeta(desafio: MetaDoDesafio) {
  return [
    `${formatarDuracao(desafio.duracaoMinimaSegundos)} por dia`,
    ...desafio.metasPorTipo.map(
      (m) =>
        `ou ${formatarDuracao(m.duracaoMinimaSegundos)} de ${getRotuloMeta(m)}`,
    ),
  ].join(" · ");
}

/** "60 min de qualquer treino, ou 20 min de Corrida, ou 30 min de Cardio". */
export function descreverMeta(desafio: MetaDoDesafio) {
  return [
    `${formatarDuracao(desafio.duracaoMinimaSegundos)} de qualquer treino`,
    ...desafio.metasPorTipo.map(
      (m) =>
        `ou ${formatarDuracao(m.duracaoMinimaSegundos)} de ${getRotuloMeta(m)}`,
    ),
  ].join(", ");
}

export type FaltaNoDia =
  | { conta: true }
  | {
      conta: false;
      /** Os minutos que faltam para a duração mínima do desafio. */
      totalMinutos: number;
      /** A meta por tipo mais perto de ser cumprida com o tipo escolhido, se estiver mais perto que o total. */
      meta: { rotulo: string; minutos: number } | null;
    };

/**
 * Quanto falta para o dia contar (decisões 0040 e 0043): o dia conta quando a soma alcança a duração
 * mínima do desafio ou quando a soma dos tipos de uma meta por tipo alcança o mínimo dela. Só as metas
 * que incluem o tipo da atividade nova entram na sugestão, porque só elas avançam com ela.
 */
export function calcularFaltaNoDia(
  desafio: MetaDoDesafio,
  doDia:
    | {
        segundos: number;
        segundosPorTipo: { tipo: string; segundos: number }[];
      }
    | undefined,
  nova: { tipo: TipoAtividade; segundos: number },
): FaltaNoDia {
  const total = (doDia?.segundos ?? 0) + nova.segundos;
  const segundosDosTipos = (tipos: TipoAtividade[]) =>
    (doDia?.segundosPorTipo ?? [])
      .filter((t) => tipos.includes(t.tipo as TipoAtividade))
      .reduce((soma, t) => soma + t.segundos, 0) +
    (tipos.includes(nova.tipo) ? nova.segundos : 0);

  if (
    total >= desafio.duracaoMinimaSegundos ||
    desafio.metasPorTipo.some(
      (m) => segundosDosTipos(m.tipos) >= m.duracaoMinimaSegundos,
    )
  ) {
    return { conta: true };
  }

  const totalMinutos = Math.ceil((desafio.duracaoMinimaSegundos - total) / 60);
  const meta = desafio.metasPorTipo
    .filter((m) => m.tipos.includes(nova.tipo))
    .map((m) => ({
      rotulo: getRotuloMeta(m),
      minutos: Math.ceil(
        (m.duracaoMinimaSegundos - segundosDosTipos(m.tipos)) / 60,
      ),
    }))
    .sort((a, b) => a.minutos - b.minutos)[0];

  return {
    conta: false,
    totalMinutos,
    meta: meta && meta.minutos < totalMinutos ? meta : null,
  };
}

/** "Faltam 5 min de Corrida (ou 25 min no total)". */
export function formatarFaltaNoDia(
  falta: Exclude<FaltaNoDia, { conta: true }>,
) {
  return falta.meta
    ? `Faltam ${falta.meta.minutos} min de ${falta.meta.rotulo} (ou ${falta.totalMinutos} min no total)`
    : `Faltam ${falta.totalMinutos} min no dia`;
}
