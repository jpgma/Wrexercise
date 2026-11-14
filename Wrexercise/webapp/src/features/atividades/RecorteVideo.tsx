import { X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { duracaoMaximaS, type TrechoVideo } from "./prepararVideo";

type RecorteVideoProps = {
  arquivo: File;
  duracao: number;
  onEscolher: (trecho: TrechoVideo) => void;
  onCancelar: () => void;
};

function formatar(segundos: number) {
  const inteiro = Math.floor(segundos);
  return `${Math.floor(inteiro / 60)}:${String(inteiro % 60).padStart(2, "0")}`;
}

/**
 * Vídeo da galeria com mais de 1 minuto: a pessoa escolhe o trecho de 1 minuto que vai como comprovante
 * (ADR 0058). Só o trecho é convertido e enviado.
 */
export function RecorteVideo({
  arquivo,
  duracao,
  onEscolher,
  onCancelar,
}: RecorteVideoProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [url] = useState(() => URL.createObjectURL(arquivo));
  const [inicio, setInicio] = useState(0);
  const maximoInicio = Math.max(0, duracao - duracaoMaximaS);
  const fim = Math.min(duracao, inicio + duracaoMaximaS);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog && !dialog.open) dialog.showModal();
    return () => URL.revokeObjectURL(url);
  }, [url]);

  // A prévia toca só dentro do trecho escolhido.
  function limitarAoTrecho() {
    const video = videoRef.current;
    if (video && (video.currentTime < inicio || video.currentTime > fim)) {
      video.currentTime = inicio;
    }
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="recorte-titulo"
      onCancel={(event) => {
        event.preventDefault();
        onCancelar();
      }}
      className="m-0 h-dvh max-h-none w-dvw max-w-none overflow-hidden bg-lightbox p-0 text-on-lightbox backdrop:bg-lightbox"
    >
      <div className="flex h-full flex-col pt-[max(env(safe-area-inset-top),0.75rem)] pb-[max(env(safe-area-inset-bottom),1rem)]">
        <div className="flex items-center justify-between gap-3 px-4">
          <h2 id="recorte-titulo" className="text-base font-semibold">
            Escolha 1 minuto do vídeo
          </h2>
          <button
            type="button"
            onClick={onCancelar}
            aria-label="Cancelar"
            className="grid size-12 place-items-center rounded-full bg-lightbox/50 tap hover:bg-lightbox/80"
          >
            <X aria-hidden="true" className="size-6" />
          </button>
        </div>

        <video
          ref={videoRef}
          src={url}
          controls
          playsInline
          preload="metadata"
          onTimeUpdate={limitarAoTrecho}
          onPlay={limitarAoTrecho}
          className="min-h-0 w-full flex-1 object-contain"
        />

        <div className="px-5 pt-4">
          <label className="block text-sm">
            <span className="mb-2 flex justify-between tabular-nums">
              <span>Início {formatar(inicio)}</span>
              <span>Fim {formatar(fim)}</span>
            </span>
            <input
              type="range"
              min={0}
              max={maximoInicio}
              step={0.5}
              value={inicio}
              onChange={(event) => {
                const novo = Number(event.target.value);
                setInicio(novo);
                if (videoRef.current) videoRef.current.currentTime = novo;
              }}
              aria-valuetext={`De ${formatar(inicio)} a ${formatar(fim)}`}
              className="h-11 w-full accent-reward"
            />
          </label>
          <p className="mt-1 text-xs text-on-lightbox/70">
            O vídeo tem {formatar(duracao)}. Só o trecho escolhido é enviado.
          </p>
          <button
            type="button"
            onClick={() => onEscolher({ inicio, fim })}
            className="btn-primary mt-4 w-full"
          >
            Usar este trecho
          </button>
        </div>
      </div>
    </dialog>
  );
}
