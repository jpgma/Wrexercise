import { useState } from "react";
import { GaleriaEvidencias, type LegendaGaleria } from "./GaleriaEvidencias";
import { SeloCameraDoApp } from "./SeloCameraDoApp";
import type { Evidencia } from "./types";
import { useGaleria } from "./useGaleria";

export function EvidenciasAtividade({
  atividadeId,
  evidencias,
  legenda,
}: {
  atividadeId: string;
  evidencias: Evidencia[];
  legenda: LegendaGaleria;
}) {
  const galeria = useGaleria(atividadeId);
  const [atual, setAtual] = useState(0);
  const total = evidencias.length;

  if (total === 0) {
    return (
      <div className="grid aspect-[4/3] place-items-center bg-surface-muted text-sm text-muted">
        Sem comprovante anexado
      </div>
    );
  }

  return (
    <div className="relative">
      <div
        onScroll={(event) => {
          const trilho = event.currentTarget;
          setAtual(Math.round(trilho.scrollLeft / trilho.clientWidth));
        }}
        className="flex snap-x snap-mandatory overflow-x-auto [scrollbar-width:none]"
      >
        {evidencias.map((evidencia, i) => (
          <button
            key={evidencia.id}
            type="button"
            onClick={() => galeria.abrir(i)}
            aria-label={`Ampliar comprovante ${i + 1} de ${total}`}
            className="relative w-full shrink-0 cursor-zoom-in snap-center focus-visible:-outline-offset-4"
          >
            <img
              src={evidencia.url}
              alt=""
              className="aspect-[4/3] w-full object-cover"
              loading="lazy"
            />
            {evidencia.gravadaNoApp && (
              <SeloCameraDoApp className="absolute top-3 left-3" />
            )}
          </button>
        ))}
      </div>

      {total > 1 && (
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 bottom-3 flex justify-center"
        >
          <div className="flex gap-1.5 rounded-full bg-lightbox/45 px-2 py-1.5">
            {evidencias.map((evidencia, i) => (
              <span
                key={evidencia.id}
                className={`size-1.5 rounded-full transition-colors ${
                  i === atual ? "bg-on-lightbox" : "bg-on-lightbox/45"
                }`}
              />
            ))}
          </div>
        </div>
      )}

      <GaleriaEvidencias
        aberta={galeria.aberta}
        indiceInicial={galeria.indiceInicial}
        evidencias={evidencias}
        legenda={legenda}
        onFechar={galeria.fechar}
      />
    </div>
  );
}
