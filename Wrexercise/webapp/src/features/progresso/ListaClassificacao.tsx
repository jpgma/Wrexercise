import { ChevronRight, Flame } from "lucide-react";
import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import type { ClassificacaoParticipante } from "./types";
import { explicarVantagem } from "./vantagem";

const primeiroNome = (nome: string) => nome.trim().split(/\s+/)[0] ?? nome;

/**
 * A classificação do desafio inteiro. Cada linha mostra os números que decidem a ordem (semanas
 * cumpridas e dias verificados) e, embaixo do nome, por que a pessoa está à frente da seguinte.
 */
export function ListaClassificacao({
  participantes,
  usuarioId,
  grupoId,
}: {
  participantes: ClassificacaoParticipante[];
  usuarioId: string | undefined;
  grupoId: string;
}) {
  return (
    <ol className="card divide-y divide-border overflow-hidden">
      {participantes.map((item, indice) => {
        const voce = item.usuarioId === usuarioId;
        const seguinte = participantes[indice + 1];
        const motivo =
          seguinte &&
          explicarVantagem(
            item,
            seguinte,
            seguinte.usuarioId === usuarioId
              ? "você"
              : primeiroNome(seguinte.nomeExibicao),
          );
        return (
          <li key={item.usuarioId}>
            <Link
              to={`/grupos/${grupoId}/membros/${item.usuarioId}`}
              className={`flex min-h-16 items-center gap-3 p-3 transition-colors hover:bg-surface-muted ${voce ? "bg-surface-muted" : ""}`}
            >
              <span
                className={`badge-rank ${item.posicao === 1 ? "badge-rank-1" : ""}`}
              >
                {item.posicao}
                <span className="sr-only">º lugar</span>
              </span>
              <Avatar nome={item.nomeExibicao} id={item.usuarioId} size={34} />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold">
                  {primeiroNome(item.nomeExibicao)}
                  {voce && (
                    <span className="font-normal text-muted"> (você)</span>
                  )}
                </p>
                {motivo && (
                  <p className="truncate text-xs text-muted">{motivo}</p>
                )}
              </div>
              <div className="shrink-0 text-right">
                <p className="text-sm">
                  <span className="font-score text-lg leading-none">
                    {item.semanasCumpridas}
                  </span>{" "}
                  <span className="text-xs text-muted">
                    {item.semanasCumpridas === 1 ? "semana" : "semanas"}
                  </span>
                </p>
                <p className="flex items-center justify-end gap-1 text-xs text-muted">
                  {item.sessoesVerificadas}{" "}
                  {item.sessoesVerificadas === 1 ? "dia" : "dias"}
                  {item.sequenciaSemanas > 1 && (
                    <>
                      <span aria-hidden="true">·</span>
                      <Flame
                        aria-hidden="true"
                        className="size-3.5 text-warning"
                      />
                      {item.sequenciaSemanas}
                      <span className="sr-only">semanas seguidas</span>
                    </>
                  )}
                </p>
              </div>
              <ChevronRight
                aria-hidden="true"
                className="size-4 shrink-0 text-muted"
              />
            </Link>
          </li>
        );
      })}
    </ol>
  );
}
