import { useState } from "react";
import { Avatar } from "@/components/Avatar";
import { formatarDataHora, formatarDataRelativa } from "@/features/atividades";
import { getErrorMessage } from "@/services/api";
import { useComentar, useComentarios } from "./useReacoes";

const LIMITE = 500;

/** Qualquer membro do grupo comenta a atividade, a qualquer momento. */
export function Comentarios({ atividadeId }: { atividadeId: string }) {
  const comentarios = useComentarios(atividadeId);
  const comentar = useComentar(atividadeId);
  const [texto, setTexto] = useState("");

  const lista = comentarios.data?.comentarios ?? [];

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
          Ninguém comentou ainda. Mande um incentivo ou tire uma dúvida.
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
                <p className="mt-0.5 break-words whitespace-pre-line">
                  {comentario.texto}
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}
      <form
        className="mt-4"
        onSubmit={(event) => {
          event.preventDefault();
          comentar.mutate(texto.trim(), { onSuccess: () => setTexto("") });
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
        {comentar.isError && (
          <p role="alert" className="form-error mt-3">
            {getErrorMessage(comentar.error)}
          </p>
        )}
        <button
          type="submit"
          className="btn-secondary mt-3 w-full"
          disabled={comentar.isPending || texto.trim().length === 0}
        >
          {comentar.isPending ? "Enviando…" : "Comentar"}
        </button>
      </form>
    </section>
  );
}
