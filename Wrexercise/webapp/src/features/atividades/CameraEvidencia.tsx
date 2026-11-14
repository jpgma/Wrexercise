import { RefreshCw, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { duracaoMaximaS, duracaoMinimaS } from "./prepararVideo";

type Modo = "Foto" | "Video";

type CameraEvidenciaProps = {
  aberta: boolean;
  onFechar: () => void;
  /** A foto capturada, ainda sem processar; quem recebe prepara como as da galeria. */
  onFoto: (arquivo: File) => void;
  /** O vídeo gravado (WebM ou MP4, conforme o navegador) e o instante em que a gravação começou. */
  onVideo: (arquivo: File, iniciadoEm: Date) => void;
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

function podeGravar(): boolean {
  return typeof MediaRecorder !== "undefined";
}

function mensagemDeErro(error: unknown): string {
  const nome = error instanceof DOMException ? error.name : "";
  if (nome === "NotAllowedError" || nome === "SecurityError") {
    return "Sem permissão para usar a câmera ou o microfone. Libere o acesso nas configurações do navegador ou use a Galeria.";
  }
  if (nome === "NotFoundError" || nome === "OverconstrainedError") {
    return "Nenhuma câmera encontrada neste aparelho. Use a Galeria.";
  }
  if (nome === "NotReadableError") {
    return "A câmera está em uso por outro aplicativo. Feche-o e tente de novo, ou use a Galeria.";
  }
  return "Não foi possível abrir a câmera. Use a Galeria.";
}

/** MP4 quando o navegador grava assim (Safari); senão WebM. A conversão depois uniformiza tudo em MP4. */
function formatoDeGravacao(): { mimeType?: string; extensao: string } {
  const opcoes = [
    { mimeType: "video/mp4;codecs=avc1,mp4a", extensao: ".mp4" },
    { mimeType: "video/mp4", extensao: ".mp4" },
    { mimeType: "video/webm;codecs=vp8,opus", extensao: ".webm" },
    { mimeType: "video/webm", extensao: ".webm" },
  ];
  return (
    opcoes.find(({ mimeType }) => MediaRecorder.isTypeSupported(mimeType)) ?? {
      extensao: ".webm",
    }
  );
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

function formatarSegundos(total: number) {
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, "0")}`;
}

type Gravacao = {
  recorder: MediaRecorder;
  iniciadoEm: Date;
  extensao: string;
  partes: Blob[];
};

function Camera({
  onFechar,
  onFoto,
  onVideo,
}: Omit<CameraEvidenciaProps, "aberta">) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const gravacao = useRef<Gravacao | null>(null);
  const [modo, setModo] = useState<Modo>("Foto");
  const [frontal, setFrontal] = useState(false);
  const [pronta, setPronta] = useState(false);
  const [erro, setErro] = useState<string | null>(() =>
    temCamera() ? null : mensagemDeErro(null),
  );
  const [capturando, setCapturando] = useState(false);
  const [segundos, setSegundos] = useState<number | null>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog && !dialog.open) dialog.showModal();
  }, []);

  useEffect(() => {
    let cancelado = false;
    if (!temCamera()) return;

    navigator.mediaDevices
      .getUserMedia({
        video: {
          facingMode: frontal ? "user" : "environment",
          width: { ideal: 1920 },
          height: { ideal: 1080 },
        },
        // O microfone só é pedido para gravar vídeo.
        audio: modo === "Video",
      })
      .then((obtido) => {
        if (cancelado) {
          obtido.getTracks().forEach((track) => track.stop());
          return;
        }
        streamRef.current = obtido;
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
      gravacao.current?.recorder.stop();
      gravacao.current = null;
      streamRef.current?.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    };
  }, [frontal, modo]);

  // Contagem da gravação; para sozinha no limite.
  useEffect(() => {
    if (segundos === null) return;
    if (segundos >= duracaoMaximaS) {
      gravacao.current?.recorder.stop();
      return;
    }
    const proximo = setTimeout(
      () => setSegundos((atual) => (atual === null ? null : atual + 1)),
      1000,
    );
    return () => clearTimeout(proximo);
  }, [segundos]);

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

  function iniciarGravacao() {
    const stream = streamRef.current;
    if (!stream || gravacao.current) return;
    const { mimeType, extensao } = formatoDeGravacao();
    let recorder: MediaRecorder;
    try {
      recorder = new MediaRecorder(stream, {
        mimeType,
        videoBitsPerSecond: 2_500_000,
        audioBitsPerSecond: 96_000,
      });
    } catch {
      setErro("Não foi possível gravar vídeo neste navegador. Use a Galeria.");
      return;
    }

    const atual: Gravacao = {
      recorder,
      iniciadoEm: new Date(),
      extensao,
      partes: [],
    };
    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) atual.partes.push(event.data);
    };
    recorder.onstop = () => {
      setSegundos(null);
      // Desmontagem (fechar, trocar de câmera): nada é entregue.
      if (gravacao.current !== atual) return;
      gravacao.current = null;
      const tipo = recorder.mimeType || "video/webm";
      onVideo(
        new File(atual.partes, `camera-${Date.now()}${atual.extensao}`, {
          type: tipo.split(";")[0],
        }),
        atual.iniciadoEm,
      );
    };
    gravacao.current = atual;
    recorder.start(1000);
    setSegundos(0);
  }

  function pararGravacao() {
    // Menos de 2 segundos o servidor recusa; a contagem continua até lá.
    if (segundos !== null && segundos < duracaoMinimaS) return;
    gravacao.current?.recorder.stop();
  }

  const gravando = segundos !== null;
  const bloqueada = !pronta || erro !== null;

  return (
    <dialog
      ref={dialogRef}
      aria-label="Câmera"
      onCancel={(event) => {
        event.preventDefault();
        if (!gravando) onFechar();
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

      <div className="absolute inset-x-0 top-0 flex items-center justify-between px-3 pt-[max(env(safe-area-inset-top),0.75rem)]">
        <span
          role="timer"
          aria-live="off"
          className={`rounded-full px-3 py-1 font-score text-lg tabular-nums ${gravando ? "bg-danger text-on-lightbox" : "invisible"}`}
        >
          {formatarSegundos(duracaoMaximaS - (segundos ?? 0))}
        </span>
        <button
          type="button"
          onClick={onFechar}
          disabled={gravando}
          aria-label="Fechar câmera"
          className="grid size-12 place-items-center rounded-full bg-lightbox/50 tap hover:bg-lightbox/80 disabled:opacity-40"
        >
          <X aria-hidden="true" className="size-6" />
        </button>
      </div>

      <div className="absolute inset-x-0 bottom-0 bg-linear-to-t from-lightbox/80 to-transparent pt-10 pb-[max(env(safe-area-inset-bottom),1.5rem)]">
        {podeGravar() && (
          <div
            role="radiogroup"
            aria-label="Modo da câmera"
            className="mb-4 flex justify-center gap-2 text-sm font-semibold"
          >
            {(["Foto", "Video"] as const).map((opcao) => (
              <button
                key={opcao}
                type="button"
                role="radio"
                aria-checked={modo === opcao}
                disabled={gravando}
                onClick={() => {
                  if (modo === opcao) return;
                  setPronta(false);
                  setModo(opcao);
                }}
                className={`h-11 rounded-full px-4 ${modo === opcao ? "bg-on-lightbox text-lightbox" : "bg-lightbox/50"}`}
              >
                {opcao === "Foto" ? "Foto" : "Vídeo"}
              </button>
            ))}
          </div>
        )}
        <div className="flex items-center justify-center gap-10">
          <span aria-hidden="true" className="size-12" />
          {modo === "Foto" ? (
            <button
              type="button"
              onClick={() => void fotografar()}
              disabled={bloqueada || capturando}
              aria-label="Tirar foto"
              className="size-18 rounded-full border-4 border-on-lightbox bg-on-lightbox/30 tap disabled:opacity-40"
            />
          ) : (
            <button
              type="button"
              onClick={gravando ? pararGravacao : iniciarGravacao}
              disabled={bloqueada}
              aria-label={gravando ? "Parar gravação" : "Gravar vídeo"}
              className="grid size-18 place-items-center rounded-full border-4 border-on-lightbox tap disabled:opacity-40"
            >
              <span
                aria-hidden="true"
                className={`bg-danger transition-all ${gravando ? "size-7 rounded-md" : "size-12 rounded-full"}`}
              />
            </button>
          )}
          <button
            type="button"
            onClick={() => {
              setPronta(false);
              setFrontal((atual) => !atual);
            }}
            disabled={gravando || (erro !== null && !frontal)}
            aria-label={
              frontal ? "Usar câmera traseira" : "Usar câmera frontal"
            }
            className="grid size-12 place-items-center rounded-full bg-lightbox/50 tap hover:bg-lightbox/80 disabled:opacity-40"
          >
            <RefreshCw aria-hidden="true" className="size-6" />
          </button>
        </div>
      </div>
    </dialog>
  );
}
