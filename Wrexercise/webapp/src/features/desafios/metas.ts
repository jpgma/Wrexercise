import { tipoAtividadeLabels, type TipoAtividade } from "@/features/atividades";
import { formatarDuracao } from "./datas";

/** Um mínimo próprio para estes tipos, no lugar da duração mínima do desafio (decisão 0045). */
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

/** O mínimo que vale para um tipo: o da meta por tipo que o inclui ou, sem meta, o do desafio. */
function getMinimoDoTipo(desafio: MetaDoDesafio, tipo: string) {
  return (
    desafio.metasPorTipo.find((m) => m.tipos.includes(tipo as TipoAtividade))
      ?.duracaoMinimaSegundos ?? desafio.duracaoMinimaSegundos
  );
}

/** "Pedal: 90 min, Corrida: 20 min". */
function listarMinimosPorTipo(desafio: MetaDoDesafio) {
  return desafio.metasPorTipo
    .map(
      (m) => `${getRotuloMeta(m)}: ${formatarDuracao(m.duracaoMinimaSegundos)}`,
    )
    .join(", ");
}

/** "60 min por dia (Pedal: 90 min, Corrida: 20 min)". */
export function formatarMeta(desafio: MetaDoDesafio) {
  const base = `${formatarDuracao(desafio.duracaoMinimaSegundos)} por dia`;
  return desafio.metasPorTipo.length > 0
    ? `${base} (${listarMinimosPorTipo(desafio)})`
    : base;
}

/** "60 min de treino (Pedal: 90 min, Corrida: 20 min)". */
export function descreverMeta(desafio: MetaDoDesafio) {
  const base = `${formatarDuracao(desafio.duracaoMinimaSegundos)} de treino`;
  return desafio.metasPorTipo.length > 0
    ? `${base} (${listarMinimosPorTipo(desafio)})`
    : base;
}

export type FaltaNoDia =
  | { conta: true }
  | {
      conta: false;
      /** Os minutos que faltam do tipo escolhido. */
      minutos: number;
      /** A meta por tipo do tipo escolhido, ou nulo quando ele usa o mínimo do desafio. */
      rotulo: string | null;
      /** Um caminho mais curto, com o tipo de menor mínimo, se houver. */
      alternativa: { rotulo: string; minutos: number } | null;
    };

/**
 * A parte do dia já preenchida, pela regra proporcional (decisão 0045): cada atividade preenche os
 * segundos dela divididos pelo mínimo do tipo, e o dia conta quando as partes somam 1. Arredonda como
 * o backend (`ContagensAtividadeDesafio.CompletaODiaSql`), para não perder um dia por resíduo.
 */
export function calcularParteDoDia(
  desafio: MetaDoDesafio,
  segundosPorTipo: { tipo: string; segundos: number }[],
) {
  const parte = segundosPorTipo.reduce(
    (soma, t) => soma + t.segundos / getMinimoDoTipo(desafio, t.tipo),
    0,
  );
  return Math.round(parte * 1e9) / 1e9;
}

/**
 * Os caminhos para fechar o que falta do dia, do mais curto ao mais longo: cada meta por tipo e os
 * tipos sem meta ("outro treino" ou, sem metas, "treino"). No empate, os tipos sem meta vêm antes.
 */
export function listarCaminhosDoDia(desafio: MetaDoDesafio, parte: number) {
  const falta = Math.max(0, 1 - parte);
  const minutosDe = (minimo: number) => Math.ceil((falta * minimo) / 60);
  return [
    {
      rotulo: desafio.metasPorTipo.length > 0 ? "outro treino" : "treino",
      minutos: minutosDe(desafio.duracaoMinimaSegundos),
    },
    ...desafio.metasPorTipo.map((m) => ({
      rotulo: getRotuloMeta(m),
      minutos: minutosDe(m.duracaoMinimaSegundos),
    })),
  ].sort((a, b) => a.minutos - b.minutos);
}

/** "Faltam 10 min de Corrida, 20 min de outro treino ou 30 min de Pedal". */
export function formatarCaminhosDoDia(
  caminhos: { rotulo: string; minutos: number }[],
) {
  const itens = caminhos.map((c) => `${c.minutos} min de ${c.rotulo}`);
  const ultimo = itens.pop();
  return `Faltam ${itens.length > 0 ? `${itens.join(", ")} ou ${ultimo}` : ultimo}`;
}

/** Quanto falta para o dia contar com a atividade nova somada às do dia (decisão 0045). */
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
  const parte = calcularParteDoDia(desafio, [
    ...(doDia?.segundosPorTipo ?? []),
    { tipo: nova.tipo, segundos: nova.segundos },
  ]);
  const falta = 1 - parte;
  if (falta <= 0) return { conta: true };

  const minutosDe = (minimo: number) => Math.ceil((falta * minimo) / 60);
  const metaDoTipo = desafio.metasPorTipo.find((m) =>
    m.tipos.includes(nova.tipo),
  );
  const minutos = minutosDe(getMinimoDoTipo(desafio, nova.tipo));
  const maisCurta = desafio.metasPorTipo
    .map((m) => ({
      rotulo: getRotuloMeta(m),
      minutos: minutosDe(m.duracaoMinimaSegundos),
    }))
    .reduce((menor, opcao) => (opcao.minutos < menor.minutos ? opcao : menor), {
      rotulo: "outro treino",
      minutos: minutosDe(desafio.duracaoMinimaSegundos),
    });

  return {
    conta: false,
    minutos,
    rotulo: metaDoTipo ? getRotuloMeta(metaDoTipo) : null,
    alternativa:
      desafio.metasPorTipo.length > 0 && maisCurta.minutos < minutos
        ? maisCurta
        : null,
  };
}

/** "Faltam 30 min de Pedal (ou 10 min de Corrida)". */
export function formatarFaltaNoDia(
  falta: Exclude<FaltaNoDia, { conta: true }>,
) {
  const principal = falta.rotulo
    ? `Faltam ${falta.minutos} min de ${falta.rotulo}`
    : `Faltam ${falta.minutos} min no dia`;
  return falta.alternativa
    ? `${principal} (ou ${falta.alternativa.minutos} min de ${falta.alternativa.rotulo})`
    : principal;
}
