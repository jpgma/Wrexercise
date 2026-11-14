import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Clock,
  X,
} from "lucide-react";
import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type PointerEvent,
} from "react";
import {
  formatarDataRelativa,
  formatarDuracao,
  tipoAtividadeLabels,
} from "./labels";
import { SeloCameraDoApp } from "./SeloCameraDoApp";
import type { AtividadeResumo, Evidencia } from "./types";

export type LegendaGaleria = Pick<
  AtividadeResumo,
  "nomeExibicao" | "tipo" | "duracaoSegundos" | "startedAt" | "descricao"
>;

type GaleriaEvidenciasProps = {
  aberta: boolean;
  indiceInicial: number;
  /** Ausente enquanto as evidências ainda estão sendo carregadas. */
  evidencias: Evidencia[] | undefined;
  erro?: string;
  legenda: LegendaGaleria;
  onFechar: () => void;
};

const escala = 2;
const intervaloToqueDuplo = 300;
const distanciaToque = 10;

type Deslocamento = { x: number; y: number };

/** Visualização das evidências em tela cheia, com toque duplo para ampliar. */
export function GaleriaEvidencias({
  aberta,
  ...props
}: GaleriaEvidenciasProps) {
  if (!aberta) return null;
  return <Galeria {...props} />;
}

function Galeria({
  indiceInicial,
  evidencias,
  erro,
  legenda,
  onFechar,
}: Omit<GaleriaEvidenciasProps, "aberta">) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const trilhoRef = useRef<HTMLDivElement>(null);
  const [indice, setIndice] = useState(indiceInicial);
  const [zoom, setZoom] = useState<Deslocamento | null>(null);
  const [arrastando, setArrastando] = useState(false);
  const arraste = useRef<{
    id: number;
    x: number;
    y: number;
    inicio: Deslocamento;
    moveu: boolean;
  } | null>(null);
  const ultimoToque = useRef<{ x: number; y: number; t: number } | null>(null);
  const total = evidencias?.length ?? 0;

  // O `<dialog>` modal já cuida do foco, da tecla Esc e de bloquear o resto da página.
  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog && !dialog.open) dialog.showModal();
    const overflow = document.documentElement.style.overflow;
    document.documentElement.style.overflow = "hidden";
    return () => {
      document.documentElement.style.overflow = overflow;
    };
  }, []);

  useLayoutEffect(() => {
    const trilho = trilhoRef.current;
    if (trilho && total > 0) {
      trilho.scrollLeft = indiceInicial * trilho.clientWidth;
    }
    // Só ao chegar as evidências: depois disso, a posição é de quem rola.
  }, [total]); // eslint-disable-line react-hooks/exhaustive-deps

  // Trocar de slide pausa o vídeo que ficou para trás.
  useEffect(() => {
    trilhoRef.current
      ?.querySelectorAll<HTMLVideoElement>("video[data-indice]")
      .forEach((video) => {
        if (Number(video.dataset.indice) !== indice) video.pause();
      });
  }, [indice]);

  const videoAtual = evidencias?.[indice]?.tipo === "Video";

  function irPara(novo: number) {
    const trilho = trilhoRef.current;
    if (!trilho || novo < 0 || novo >= total) return;
    setZoom(null);
    const reduzirMovimento = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;
    trilho.scrollTo({
      left: novo * trilho.clientWidth,
      behavior: reduzirMovimento ? "instant" : "smooth",
    });
  }

  function limitar(alvo: Deslocamento, slide: HTMLElement): Deslocamento {
    const imagem = slide.querySelector("img");
    if (!imagem) return { x: 0, y: 0 };
    const maxX = Math.max(
      0,
      (imagem.offsetWidth * escala - slide.clientWidth) / 2,
    );
    const maxY = Math.max(
      0,
      (imagem.offsetHeight * escala - slide.clientHeight) / 2,
    );
    return {
      x: Math.min(maxX, Math.max(-maxX, alvo.x)),
      y: Math.min(maxY, Math.max(-maxY, alvo.y)),
    };
  }

  function alternarZoom(event: PointerEvent<HTMLDivElement>) {
    if (zoom) {
      setZoom(null);
      return;
    }
    // Mantém sob o dedo o ponto tocado.
    const slide = event.currentTarget;
    const rect = slide.getBoundingClientRect();
    const dx = event.clientX - (rect.left + rect.width / 2);
    const dy = event.clientY - (rect.top + rect.height / 2);
    setZoom(limitar({ x: -dx * (escala - 1), y: -dy * (escala - 1) }, slide));
  }

  function onPointerDown(event: PointerEvent<HTMLDivElement>) {
    arraste.current = {
      id: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      inicio: zoom ?? { x: 0, y: 0 },
      moveu: false,
    };
    if (zoom) event.currentTarget.setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    const atual = arraste.current;
    if (!atual || atual.id !== event.pointerId) return;
    const dx = event.clientX - atual.x;
    const dy = event.clientY - atual.y;
    if (!atual.moveu && Math.hypot(dx, dy) > distanciaToque) {
      atual.moveu = true;
      if (zoom) setArrastando(true);
    }
    if (zoom && atual.moveu) {
      setZoom(
        limitar(
          { x: atual.inicio.x + dx, y: atual.inicio.y + dy },
          event.currentTarget,
        ),
      );
    }
  }

  function onPointerUp(event: PointerEvent<HTMLDivElement>) {
    const atual = arraste.current;
    arraste.current = null;
    setArrastando(false);
    if (!atual || atual.id !== event.pointerId || atual.moveu) return;

    const anterior = ultimoToque.current;
    const agora = event.timeStamp;
    if (
      anterior &&
      agora - anterior.t < intervaloToqueDuplo &&
      Math.hypot(event.clientX - anterior.x, event.clientY - anterior.y) < 30
    ) {
      ultimoToque.current = null;
      alternarZoom(event);
    } else {
      ultimoToque.current = { x: event.clientX, y: event.clientY, t: agora };
    }
  }

  function onPointerCancel() {
    // A rolagem do trilho assumiu o gesto: não é um toque.
    arraste.current = null;
    setArrastando(false);
  }

  return (
    <dialog
      ref={dialogRef}
      aria-label="Comprovantes da atividade"
      onCancel={(event) => {
        event.preventDefault();
        onFechar();
      }}
      onKeyDown={(event) => {
        if (event.key === "ArrowLeft") irPara(indice - 1);
        if (event.key === "ArrowRight") irPara(indice + 1);
      }}
      className="m-0 h-dvh max-h-none w-dvw max-w-none animate-fade-in overflow-hidden bg-lightbox p-0 text-on-lightbox backdrop:bg-lightbox"
    >
      {evidencias === undefined || total === 0 ? (
        <div
          role={erro ? "alert" : "status"}
          className="grid h-full place-items-center px-6 text-center text-sm text-on-lightbox/80"
        >
          {erro ??
            (evidencias === undefined
              ? "Carregando comprovantes…"
              : "Sem comprovante anexado")}
        </div>
      ) : (
        <div
          ref={trilhoRef}
          onScroll={(event) => {
            const trilho = event.currentTarget;
            const novo = Math.round(trilho.scrollLeft / trilho.clientWidth);
            if (novo !== indice) {
              setIndice(novo);
              setZoom(null);
            }
          }}
          className={`flex h-full snap-x snap-mandatory overscroll-x-contain [scrollbar-width:none] ${
            zoom ? "overflow-hidden" : "overflow-x-auto"
          }`}
        >
          {evidencias.map((evidencia, i) => {
            if (evidencia.tipo === "Video") {
              // Vídeo não amplia: os gestos são dos controles do próprio player.
              return (
                <div
                  key={evidencia.id}
                  aria-roledescription="slide"
                  aria-label={`Vídeo ${i + 1} de ${total}`}
                  className="grid h-full w-full shrink-0 snap-center place-items-center overflow-hidden px-2 pt-16 pb-6 touch-pan-x"
                >
                  <video
                    data-indice={i}
                    src={evidencia.url}
                    poster={`${evidencia.url}/miniatura`}
                    controls
                    playsInline
                    preload="metadata"
                    className="max-h-full max-w-full"
                  />
                </div>
              );
            }
            const ampliada = zoom !== null && i === indice;
            return (
              <div
                key={evidencia.id}
                onPointerDown={onPointerDown}
                onPointerMove={onPointerMove}
                onPointerUp={onPointerUp}
                onPointerCancel={onPointerCancel}
                aria-roledescription="slide"
                aria-label={`Comprovante ${i + 1} de ${total}. Toque duas vezes para ampliar.`}
                className={`grid h-full w-full shrink-0 snap-center place-items-center overflow-hidden ${
                  ampliada
                    ? "cursor-grab touch-none active:cursor-grabbing"
                    : "cursor-zoom-in touch-pan-x"
                }`}
              >
                <img
                  src={evidencia.url}
                  alt=""
                  draggable={false}
                  className={`max-h-full max-w-full object-contain select-none ${
                    arrastando
                      ? ""
                      : "transition-transform duration-200 ease-out"
                  }`}
                  style={{
                    transform: ampliada
                      ? `translate(${zoom.x}px, ${zoom.y}px) scale(${escala})`
                      : undefined,
                  }}
                />
              </div>
            );
          })}
        </div>
      )}

      <div className="pointer-events-none absolute inset-x-0 top-0 flex items-center justify-between gap-3 bg-linear-to-b from-lightbox/70 to-transparent px-3 pt-[max(env(safe-area-inset-top),0.75rem)] pb-6">
        <span className="flex items-center gap-3 pl-2">
          <span
            aria-live="polite"
            className="text-sm font-semibold tabular-nums"
          >
            {total > 1 && `${indice + 1} / ${total}`}
          </span>
          {evidencias?.[indice]?.gravadaNoApp && <SeloCameraDoApp />}
        </span>
        <button
          type="button"
          onClick={onFechar}
          aria-label="Fechar"
          className="pointer-events-auto grid size-11 place-items-center rounded-full bg-lightbox/50 tap hover:bg-lightbox/80"
        >
          <X aria-hidden="true" className="size-6" />
        </button>
      </div>

      {total > 1 && !zoom && (
        <>
          <button
            type="button"
            onClick={() => irPara(indice - 1)}
            disabled={indice === 0}
            aria-label="Comprovante anterior"
            className="absolute top-1/2 left-3 hidden size-11 -translate-y-1/2 place-items-center rounded-full bg-lightbox/50 hover:bg-lightbox/80 disabled:opacity-0 sm:grid"
          >
            <ChevronLeft aria-hidden="true" className="size-6" />
          </button>
          <button
            type="button"
            onClick={() => irPara(indice + 1)}
            disabled={indice === total - 1}
            aria-label="Próximo comprovante"
            className="absolute top-1/2 right-3 hidden size-11 -translate-y-1/2 place-items-center rounded-full bg-lightbox/50 hover:bg-lightbox/80 disabled:opacity-0 sm:grid"
          >
            <ChevronRight aria-hidden="true" className="size-6" />
          </button>
        </>
      )}

      <div
        className={`pointer-events-none absolute inset-x-0 bottom-0 bg-linear-to-t from-lightbox/90 via-lightbox/60 to-transparent px-5 pt-12 pb-[max(env(safe-area-inset-bottom),1.25rem)] transition-opacity duration-200 ${
          zoom || videoAtual ? "opacity-0" : ""
        }`}
      >
        <div className="mx-auto max-w-2xl">
          <p className="font-semibold">{legenda.nomeExibicao}</p>
          <p className="mt-0.5 flex flex-wrap items-center gap-x-3 text-sm text-on-lightbox/80">
            <span>{tipoAtividadeLabels[legenda.tipo]}</span>
            <span className="inline-flex items-center gap-1">
              <Clock aria-hidden="true" className="size-3.5" />
              {formatarDuracao(legenda.duracaoSegundos)}
            </span>
            <span className="inline-flex items-center gap-1">
              <CalendarDays aria-hidden="true" className="size-3.5" />
              {formatarDataRelativa(legenda.startedAt)}
            </span>
          </p>
          {legenda.descricao && (
            <p className="mt-2 line-clamp-2 text-sm break-words">
              {legenda.descricao}
            </p>
          )}
        </div>
      </div>
    </dialog>
  );
}
