import {
  CalendarDays,
  Clock,
  Globe,
  ListChecks,
  Repeat,
  ShieldCheck,
  Trophy,
  UserPlus,
  type LucideIcon,
} from "lucide-react";
import type { ReactNode } from "react";
import { getRotuloMeta } from "./metas";
import { formatarData, formatarDuracao, getTotalSemanas } from "./datas";
import type { Desafio } from "./useDesafios";

function Regra({
  icon: Icon,
  children,
}: {
  icon: LucideIcon;
  children: ReactNode;
}) {
  return (
    <li className="flex gap-3 py-2">
      <Icon aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-link" />
      <span className="min-w-0 text-sm break-words">{children}</span>
    </li>
  );
}

/** Todas as regras do desafio, uma por linha, para ninguém precisar adivinhar como se ganha. */
export function RegrasDesafio({ desafio }: { desafio: Desafio }) {
  const semanas = getTotalSemanas(desafio.dataInicio, desafio.dataFim);

  return (
    <ul className="divide-y divide-border">
      <Regra icon={CalendarDays}>
        De <strong>{formatarData(desafio.dataInicio)}</strong> a{" "}
        <strong>{formatarData(desafio.dataFim)}</strong> · {semanas}{" "}
        {semanas === 1 ? "semana" : "semanas"}, de segunda a domingo
      </Regra>
      <Regra icon={Repeat}>
        Meta:{" "}
        <strong>{desafio.sessoesPorSemana} dias de treino por semana</strong>
      </Regra>
      <Regra icon={Clock}>
        Um dia conta com{" "}
        <strong>
          {formatarDuracao(desafio.duracaoMinimaSegundos)} de treino
        </strong>
        . Os treinos do mesmo dia somam
      </Regra>
      {desafio.metasPorTipo.length > 0 && (
        <Regra icon={ListChecks}>
          Ou com{" "}
          {desafio.metasPorTipo.map((meta, indice) => (
            <span key={meta.id}>
              {indice > 0 && ", "}
              <strong>
                {formatarDuracao(meta.duracaoMinimaSegundos)} de{" "}
                {getRotuloMeta(meta)}
              </strong>
            </span>
          ))}
          . Tipos diferentes somam em proporção
        </Regra>
      )}
      <Regra icon={ShieldCheck}>
        Só contam os treinos <strong>verificados</strong> pelo grupo
      </Regra>
      <Regra icon={Trophy}>
        Ganha quem cumprir a meta em <strong>mais semanas</strong>. No empate,
        vale quem tem mais dias verificados e, depois, a maior sequência de
        semanas
      </Regra>
      <Regra icon={UserPlus}>
        {desafio.totalParticipantes}{" "}
        {desafio.totalParticipantes === 1 ? "participante" : "participantes"}.
        Quem entra no meio conta a partir da primeira semana completa
      </Regra>
      <Regra icon={Globe}>
        Horários no fuso {desafio.fusoHorario.replaceAll("_", " ")}
      </Regra>
    </ul>
  );
}
