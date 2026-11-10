import { tipoAtividadeIcones } from "@/features/atividades";
import { addDias, getHojeNoFuso } from "./datas";
import { getRotuloMeta, type MetaPorTipo } from "./metas";

const letras = ["S", "T", "Q", "Q", "S", "S", "D"];

/**
 * As semanas de um desafio sempre começam na segunda-feira. Um dia que contou por uma meta por tipo,
 * e não pela duração mínima, mostra o ícone dela embaixo, com a legenda no fim (decisão 0043).
 */
export function FaixaSemana({
  inicio,
  dias,
  metasPorTipo,
  fusoHorario,
}: {
  inicio: string;
  dias: { data: string; metaPorTipoId: string | null }[];
  metasPorTipo: MetaPorTipo[];
  fusoHorario: string;
}) {
  const hoje = getHojeNoFuso(fusoHorario);
  const metaDoDia = (dia: string) => {
    const metaId = dias.find((d) => d.data === dia)?.metaPorTipoId;
    return metaId ? metasPorTipo.find((m) => m.id === metaId) : undefined;
  };
  const usadas = metasPorTipo.filter((m) =>
    dias.some((d) => d.metaPorTipoId === m.id),
  );

  return (
    <>
      <ul
        aria-label="Dias da semana"
        className="mt-4 grid grid-cols-7 gap-1.5 text-center text-2xs text-muted"
      >
        {letras.map((letra, indice) => {
          const dia = addDias(inicio, indice);
          const treinou = dias.some((d) => d.data === dia);
          const meta = metaDoDia(dia);
          const Icone = meta
            ? tipoAtividadeIcones[meta.tipos[0] ?? "Outro"]
            : null;
          const eHoje = dia === hoje;
          return (
            <li
              key={dia}
              aria-current={eHoje ? "date" : undefined}
              aria-label={`Dia ${Number(dia.slice(8))}${treinou ? ": treinou" : ""}${meta ? `, contou por ${getRotuloMeta(meta)}` : ""}`}
            >
              <div
                className={`relative mx-auto mb-1 grid size-8 place-items-center rounded-md text-xs font-bold ${
                  treinou
                    ? "border-2 border-outline bg-primary text-on-primary"
                    : eHoje
                      ? "border-2 border-dashed border-outline text-foreground"
                      : "bg-surface-muted"
                }`}
              >
                {Number(dia.slice(8))}
                {Icone && (
                  <span
                    aria-hidden="true"
                    className="absolute -right-1.5 -bottom-1.5 grid size-4 place-items-center rounded-full border border-outline bg-surface text-foreground"
                  >
                    <Icone className="size-2.5" />
                  </span>
                )}
              </div>
              {letra}
            </li>
          );
        })}
      </ul>
      {usadas.length > 0 && (
        <p
          aria-hidden="true"
          className="mt-2 flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-2xs text-muted"
        >
          {usadas.map((meta) => {
            const Icone = tipoAtividadeIcones[meta.tipos[0] ?? "Outro"];
            return (
              <span key={meta.id} className="inline-flex items-center gap-1">
                <Icone className="size-3" />
                Contou por {getRotuloMeta(meta)}
              </span>
            );
          })}
        </p>
      )}
    </>
  );
}
