import { Route } from "lucide-react";

/**
 * A imagem do Percurso que o app gerou ao terminar o treino (ADR 0084). Mostra o trajeto e os números do
 * GPS do aparelho: é um sinal, não prova, e não muda a comprovação.
 */
export function SeloPercurso({ className = "" }: { className?: string }) {
  return (
    <span
      title="Imagem do percurso gravado no app: o GPS é um sinal, não prova."
      className={`inline-flex items-center gap-1 rounded-full bg-lightbox/60 px-2 py-1 text-xs font-semibold text-on-lightbox ${className}`}
    >
      <Route aria-hidden="true" className="size-3.5" />
      Percurso
    </span>
  );
}
