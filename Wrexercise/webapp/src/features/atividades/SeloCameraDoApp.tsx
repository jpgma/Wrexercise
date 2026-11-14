import { Camera } from "lucide-react";

/** Comprovante capturado pela câmera do aplicativo (ADR 0058). Sinal do navegador, não prova. */
export function SeloCameraDoApp({ className = "" }: { className?: string }) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full bg-lightbox/60 px-2 py-1 text-xs font-semibold text-on-lightbox ${className}`}
    >
      <Camera aria-hidden="true" className="size-3.5" />
      Câmera do app
    </span>
  );
}
