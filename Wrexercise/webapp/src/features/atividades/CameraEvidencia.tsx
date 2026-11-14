import { RefreshCw, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";

type CameraEvidenciaProps = {
  aberta: boolean;
  onFechar: () => void;
  /** A foto capturada, ainda sem processar; quem recebe prepara como as da galeria. */
  onFoto: (arquivo: File) => void;
};

/**
 * Câmera dentro do aplicativo (ADR 0058). Abrir o aplicativo de câmera do sistema tira o PWA da tela, e o
 * Android pode encerrá-lo nesse meio-tempo: a foto voltava para uma página que já não existia.
 */
export function CameraEvidencia({ aberta, ...props }: CameraEvidenciaProps) {
  if (!aberta) return null;
  return <Camera {...props} />;
}

/** `mediaDevices` só existe em contexto seguro (HTTPS ou localhost). */
function temCamera(): boolean {
  return typeof navigator.mediaDevices?.getUserMedia === "function";
}

function mensagemDeErro(error: unknown): string {
  const nome = error instanceof DOMException ? error.name : "";
  if (nome === "NotAllowedError" || nome === "SecurityError") {
    return "Sem permissão para usar a câmera. Libere o acesso nas configurações do navegador ou use a Galeria.";
  }
  if (nome === "NotFoundError" || nome === "OverconstrainedError") {
    return "Nenhuma câmera encontrada neste aparelho. Use a Galeria.";
  }
  if (nome === "NotReadableError") {
    return "A câmera está em uso por outro aplicativo. Feche-o e tente de novo, ou use a Galeria.";
  }
  return "Não foi possível abrir a câmera. Use a Galeria.";
}

function capturarQuadro(video: HTMLVideoElement): Promise<Blob | null> {
  const canvas = document.createElement("canvas");
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  const ctx = canvas.getContext("2d");
  if (!ctx || canvas.width === 0) return Promise.resolve(null);
  ctx.drawImage(video, 0, 0);
  return new Promise((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.92));
}

function Camera({ onFechar, onFoto }: Omit<CameraEvidenciaProps, "aberta">) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [frontal, setFrontal] = useState(false);
  const [pronta, setPronta] = useState(false);
  // Sem `mediaDevices` (contexto inseguro ou navegador antigo) a câmera nem tenta abrir.
  const [erro, setErro] = useState<string | null>(() =>
    temCamera() ? null : mensagemDeErro(null),
  );
  const [capturando, setCapturando] = useState(false);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog && !dialog.open) dialog.showModal();
  }, []);

  useEffect(() => {
    let cancelado = false;
    let stream: MediaStream | null = null;
    if (!temCamera()) return;

    navigator.mediaDevices
      .getUserMedia({
        video: {
          facingMode: frontal ? "user" : "environment",
          width: { ideal: 1920 },
          height: { ideal: 1080 },
        },
        audio: false,
      })
      .then((obtido) => {
        if (cancelado) {
          obtido.getTracks().forEach((track) => track.stop());
          return;
        }
        stream = obtido;
        const video = videoRef.current;
        if (video) {
          video.srcObject = obtido;
          void video.play().catch(() => undefined);
        }
        setErro(null);
      })
      .catch((error: unknown) => {
        if (!cancelado) setErro(mensagemDeErro(error));
      });

    // A luz da câmera só apaga quando todas as faixas param.
    return () => {
      cancelado = true;
      stream?.getTracks().forEach((track) => track.stop());
    };
  }, [frontal]);

  async function fotografar() {
    const video = videoRef.current;
    if (!video || capturando) return;
    setCapturando(true);
    const foto = await capturarQuadro(video);
    setCapturando(false);
    if (!foto) {
      setErro("Não foi possível tirar a foto. Tente de novo ou use a Galeria.");
      return;
    }
    onFoto(
      new File([foto], `camera-${Date.now()}.jpg`, { type: "image/jpeg" }),
    );
  }

  return (
    <dialog
      ref={dialogRef}
      aria-label="Câmera"
      onCancel={(event) => {
        event.preventDefault();
        onFechar();
      }}
      className="m-0 h-dvh max-h-none w-dvw max-w-none overflow-hidden bg-lightbox p-0 text-on-lightbox backdrop:bg-lightbox"
    >
      <video
        ref={videoRef}
        muted
        playsInline
        onLoadedData={() => setPronta(true)}
        className={`h-full w-full object-contain ${frontal ? "-scale-x-100" : ""}`}
      />

      {erro && (
        <div
          role="alert"
          className="absolute inset-0 grid place-items-center px-8 text-center text-sm"
        >
          <p>{erro}</p>
        </div>
      )}
      {!erro && !pronta && (
        <p
          role="status"
          className="absolute inset-0 grid place-items-center text-sm text-on-lightbox/80"
        >
          Abrindo a câmera…
        </p>
      )}

      <div className="absolute inset-x-0 top-0 flex justify-end px-3 pt-[max(env(safe-area-inset-top),0.75rem)]">
        <button
          type="button"
          onClick={onFechar}
          aria-label="Fechar câmera"
          className="grid size-12 place-items-center rounded-full bg-lightbox/50 tap hover:bg-lightbox/80"
        >
          <X aria-hidden="true" className="size-6" />
        </button>
      </div>

      <div className="absolute inset-x-0 bottom-0 flex items-center justify-center gap-10 bg-linear-to-t from-lightbox/80 to-transparent pt-10 pb-[max(env(safe-area-inset-bottom),1.5rem)]">
        <span aria-hidden="true" className="size-12" />
        <button
          type="button"
          onClick={() => void fotografar()}
          disabled={!pronta || erro !== null || capturando}
          aria-label="Tirar foto"
          className="size-18 rounded-full border-4 border-on-lightbox bg-on-lightbox/30 tap disabled:opacity-40"
        />
        <button
          type="button"
          onClick={() => {
            setPronta(false);
            setFrontal((atual) => !atual);
          }}
          disabled={erro !== null && !frontal}
          aria-label={frontal ? "Usar câmera traseira" : "Usar câmera frontal"}
          className="grid size-12 place-items-center rounded-full bg-lightbox/50 tap hover:bg-lightbox/80 disabled:opacity-40"
        >
          <RefreshCw aria-hidden="true" className="size-6" />
        </button>
      </div>
    </dialog>
  );
}
