import { History } from "lucide-react";

/**
 * Foto ou vídeo que o autor mandou depois do prazo da verificação (ADR 0083): aparece na atividade, mas não
 * contou para a avaliação.
 */
export function SeloAdicionadaDepois({
  className = "",
}: {
  className?: string;
}) {
  return (
    <span
      title="Mandada depois da avaliação: não contou para ela."
      className={`inline-flex items-center gap-1 rounded-full bg-lightbox/60 px-2 py-1 text-xs font-semibold text-on-lightbox ${className}`}
    >
      <History aria-hidden="true" className="size-3.5" />
      Adicionada depois
    </span>
  );
}
