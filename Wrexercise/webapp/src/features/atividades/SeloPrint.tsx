import { Smartphone } from "lucide-react";

/**
 * Comprovante que é uma captura de tela (ADR 0060). Sinal do navegador, não prova.
 * Lembra quem avalia de conferir a data que aparece na própria imagem.
 */
export function SeloPrint({ className = "" }: { className?: string }) {
  return (
    <span
      title="Captura de tela: confira a data que aparece na imagem."
      className={`inline-flex items-center gap-1 rounded-full bg-lightbox/60 px-2 py-1 text-xs font-semibold text-on-lightbox ${className}`}
    >
      <Smartphone aria-hidden="true" className="size-3.5" />
      Print
    </span>
  );
}
