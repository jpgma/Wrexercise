import { MapPin } from "lucide-react";
import { formatarDistancia } from "./calculo";
import { MapaPercurso } from "./MapaPercurso";
import { ResumoPercurso } from "./ResumoPercurso";
import { usePercurso } from "./usePercurso";

/**
 * O Percurso na página da atividade e na avaliação (ADR 0084): o mapa e os números. Ao vivo, o mapa se refaz
 * a cada 30 s. Quem treinou vê o percurso inteiro e um lembrete do que o grupo não vê.
 */
export function PercursoAtividade({
  atividadeId,
  aoVivo = false,
  className = "",
}: {
  atividadeId: string;
  aoVivo?: boolean;
  className?: string;
}) {
  const percurso = usePercurso(atividadeId, aoVivo);
  if (!percurso.data) {
    return percurso.isPending ? (
      <div
        aria-hidden="true"
        className={`aspect-[3/2] w-full animate-pulse rounded-xl bg-surface-muted ${className}`}
      />
    ) : null;
  }

  const dados = percurso.data;
  return (
    <section aria-labelledby={`percurso-${atividadeId}`} className={className}>
      <h2
        id={`percurso-${atividadeId}`}
        className="mb-2 flex items-center gap-1.5 text-sm font-semibold"
      >
        <MapPin aria-hidden="true" className="size-4" />
        Percurso
        {dados.aoVivo && <span className="chip chip-danger ml-1">AO VIVO</span>}
      </h2>
      <MapaPercurso
        percurso={dados}
        rotulo={`Mapa do percurso: ${formatarDistancia(dados.resumo.distanciaMetros)}`}
      />
      {dados.voce && (
        <p className="mt-1 text-xs text-muted">
          O grupo não vê os primeiros e os últimos 200 m.
        </p>
      )}
      <div className="mt-3">
        <ResumoPercurso percurso={dados} />
      </div>
    </section>
  );
}
