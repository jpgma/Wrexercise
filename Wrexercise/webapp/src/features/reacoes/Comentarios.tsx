import { ImagePlus, Video, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Avatar } from "@/components/Avatar";
import {
  formatarDataHora,
  formatarDataRelativa,
  FotoNaoProcessadaError,
  prepararMidia,
  VideoNaoProcessadoError,
} from "@/features/atividades";
import { getErrorMessage } from "@/services/api";
import type { ComentarioMidia } from "./types";
import { useComentar, useComentarios } from "./useReacoes";

const LIMITE = 500;

/**
 * Qualquer membro do grupo comenta a atividade, a qualquer momento, com texto, uma foto ou um vídeo, ou os
 * dois (ADR 0042, 0083).
 */
export function Comentarios({ atividadeId }: { atividadeId: string }) {
  const comentarios = useComentarios(atividadeId);
  const comentar = useComentar(atividadeId);
  const seletor = useRef<HTMLInputElement>(null);
  const [texto, setTexto] = useState("");
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [progresso, setProgresso] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const lista = comentarios.data?.comentarios ?? [];
  const enviando = progresso !== null;
  const podeEnviar = !enviando && (texto.trim().length > 0 || arquivo !== null);

  const enviar = async () => {
    setErro(null);
    try {
      const midia = arquivo
        ? await prepararMidia(arquivo, (f) =>
            setProgresso(`Preparando… ${Math.round(f * 100)}%`),
          )
        : undefined;
      setProgresso("Enviando…");
      await comentar.mutateAsync({
        texto: texto.trim(),
        midia,
        onProgresso: (f) => setProgresso(`Enviando… ${Math.round(f * 100)}%`),
      });
      setTexto("");
      setArquivo(null);
    } catch (error) {
      setErro(
        error instanceof VideoNaoProcessadoError ||
          error instanceof FotoNaoProcessadaError
          ? error.message
          : getErrorMessage(error),
      );
    } finally {
      setProgresso(null);
    }
  };

  return (
    <section aria-label="Comentários" className="card p-5">
      <h3 className="text-base font-bold">Comentários</h3>
      {comentarios.isError && (
        <p role="alert" className="form-error mt-3">
          {getErrorMessage(comentarios.error)}
        </p>
      )}
      {comentarios.isSuccess && lista.length === 0 && (
        <p className="mt-3 text-sm text-muted">
          Ninguém comentou ainda. Mande um incentivo, uma foto ou tire uma
          dúvida.
        </p>
      )}
      {lista.length > 0 && (
        <ul className="mt-4 space-y-4">
          {lista.map((comentario) => (
            <li key={comentario.id} className="flex gap-3">
              <Avatar
                nome={comentario.nomeExibicao}
                id={comentario.usuarioId}
                size={30}
              />
              <div className="min-w-0 flex-1 text-sm">
                <div className="flex items-baseline justify-between gap-2">
                  <p className="truncate font-semibold">
                    {comentario.nomeExibicao}
                  </p>
                  <time
                    dateTime={comentario.createdAt}
                    title={formatarDataHora(comentario.createdAt)}
                    className="shrink-0 text-xs text-muted"
                  >
                    {formatarDataRelativa(comentario.createdAt)}
                  </time>
                </div>
                {comentario.texto && (
                  <p className="mt-0.5 break-words whitespace-pre-line">
                    {comentario.texto}
                  </p>
                )}
                {comentario.midia && (
                  <MidiaComentario
                    midia={comentario.midia}
                    autor={comentario.nomeExibicao}
                  />
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
      <form
        className="mt-4"
        onSubmit={(event) => {
          event.preventDefault();
          if (podeEnviar) void enviar();
        }}
      >
        <label className="block">
          <span className="input-label">Seu comentário</span>
          <textarea
            className="input-field h-auto min-h-20 resize-y py-3"
            maxLength={LIMITE}
            value={texto}
            onChange={(event) => setTexto(event.target.value)}
          />
        </label>
        <input
          ref={seletor}
          type="file"
          accept="image/jpeg,image/png,image/webp,video/*"
          className="hidden"
          onChange={(event) => {
            setErro(null);
            setArquivo(event.target.files?.[0] ?? null);
            event.target.value = "";
          }}
        />
        {arquivo && (
          <PreviaAnexo
            arquivo={arquivo}
            desabilitado={enviando}
            onRemover={() => setArquivo(null)}
          />
        )}
        {erro && (
          <p role="alert" className="form-error mt-3">
            {erro}
          </p>
        )}
        <div className="mt-3 flex gap-2">
          <button
            type="button"
            aria-label={
              arquivo ? "Trocar a foto ou o vídeo" : "Anexar foto ou vídeo"
            }
            title="Anexar foto ou vídeo"
            className="btn-secondary shrink-0 px-3"
            disabled={enviando}
            onClick={() => seletor.current?.click()}
          >
            <ImagePlus aria-hidden="true" className="size-5" />
          </button>
          <button
            type="submit"
            className="btn-secondary flex-1"
            disabled={!podeEnviar}
          >
            {progresso ?? "Comentar"}
          </button>
        </div>
      </form>
    </section>
  );
}

/** A foto no tamanho da coluna ou o vídeo com o player do navegador, que só baixa ao tocar. */
function MidiaComentario({
  midia,
  autor,
}: {
  midia: ComentarioMidia;
  autor: string;
}) {
  if (midia.tipo === "Video") {
    return (
      <video
        src={midia.url}
        poster={midia.miniaturaUrl ?? undefined}
        controls
        playsInline
        preload="none"
        aria-label={`Vídeo de ${autor}`}
        className="mt-2 max-h-80 w-full rounded-xl bg-lightbox object-contain"
      />
    );
  }

  return (
    <img
      src={midia.url}
      alt={`Foto de ${autor}`}
      loading="lazy"
      className="mt-2 max-h-80 max-w-full rounded-xl bg-surface-muted object-contain"
    />
  );
}

/** O arquivo escolhido, antes do envio: a foto em miniatura ou o nome do vídeo. */
function PreviaAnexo({
  arquivo,
  desabilitado,
  onRemover,
}: {
  arquivo: File;
  desabilitado: boolean;
  onRemover: () => void;
}) {
  const video = arquivo.type.startsWith("video/");
  const url = useMemo(
    () => (video ? null : URL.createObjectURL(arquivo)),
    [arquivo, video],
  );

  useEffect(
    () => () => {
      if (url) URL.revokeObjectURL(url);
    },
    [url],
  );

  return (
    <div className="mt-3 flex items-center gap-3 rounded-xl bg-surface-muted p-2">
      {video ? (
        <span className="grid size-14 shrink-0 place-items-center rounded-lg bg-soft text-on-soft">
          <Video aria-hidden="true" className="size-6" />
        </span>
      ) : (
        url && (
          <img
            src={url}
            alt=""
            className="size-14 shrink-0 rounded-lg object-cover"
          />
        )
      )}
      <p className="min-w-0 flex-1 truncate text-sm">
        {video ? "Vídeo" : "Foto"} · {arquivo.name}
      </p>
      <button
        type="button"
        aria-label={video ? "Tirar o vídeo" : "Tirar a foto"}
        className="grid size-11 shrink-0 place-items-center rounded-full tap hover:bg-soft"
        disabled={desabilitado}
        onClick={onRemover}
      >
        <X aria-hidden="true" className="size-5" />
      </button>
    </div>
  );
}
