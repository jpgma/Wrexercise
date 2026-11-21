import { useQueryClient } from "@tanstack/react-query";
import { ImagePlus } from "lucide-react";
import { useRef, useState } from "react";
import { getErrorMessage } from "@/services/api";
import { FotoNaoProcessadaError } from "./comprimirImagem";
import { prepararMidia } from "./prepararMidia";
import { VideoNaoProcessadoError } from "./prepararVideo";
import { anexarEvidencia } from "./useAtividades";

const maximoDeEvidencias = 10;

/**
 * O autor manda fotos e vídeos depois do envio, até o treino ser anulado (ADR 0083). Depois do prazo da
 * avaliação, eles entram com o selo "Adicionada depois" e não mudam a verificação. A localização não sai do
 * aparelho: só o registro pergunta se ela aparece.
 */
export function AdicionarEvidencia({
  atividadeId,
  totalEvidencias,
}: {
  atividadeId: string;
  totalEvidencias: number;
}) {
  const queryClient = useQueryClient();
  const seletor = useRef<HTMLInputElement>(null);
  const [enviando, setEnviando] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const vagas = maximoDeEvidencias - totalEvidencias;

  if (vagas <= 0) return null;

  const enviar = async (lista: FileList | null) => {
    if (!lista) return;
    setErro(null);
    const arquivos = Array.from(lista).slice(0, vagas);
    for (const [indice, arquivo] of arquivos.entries()) {
      const rotulo =
        arquivos.length > 1
          ? `Enviando ${indice + 1} de ${arquivos.length}…`
          : "Enviando…";
      setEnviando(rotulo);
      try {
        const preparada = await prepararMidia(arquivo, (f) =>
          setEnviando(`Preparando… ${Math.round(f * 100)}%`),
        );
        await anexarEvidencia(atividadeId, preparada, false, (f) =>
          setEnviando(`${rotulo} ${Math.round(f * 100)}%`),
        );
      } catch (error) {
        setErro(
          error instanceof VideoNaoProcessadoError ||
            error instanceof FotoNaoProcessadaError
            ? error.message
            : getErrorMessage(error),
        );
      }
    }
    setEnviando(null);
    // O detalhe e as miniaturas do feed.
    void queryClient.invalidateQueries({ queryKey: ["atividades"] });
  };

  return (
    <div className="mt-5">
      <input
        ref={seletor}
        type="file"
        accept="image/jpeg,image/png,image/webp,video/*"
        multiple
        className="hidden"
        onChange={(event) => {
          void enviar(event.target.files).finally(() => {
            event.target.value = "";
          });
        }}
      />
      <button
        type="button"
        className="btn-secondary w-full"
        disabled={enviando !== null}
        onClick={() => seletor.current?.click()}
      >
        <ImagePlus aria-hidden="true" className="size-5" />
        {enviando ?? "Adicionar foto ou vídeo"}
      </button>
      <p className="mt-2 text-xs text-muted">
        Esqueceu o print? Depois da avaliação, a foto entra com o selo
        “Adicionada depois” e não muda o resultado.
      </p>
      {erro && (
        <p role="alert" className="input-error">
          {erro}
        </p>
      )}
    </div>
  );
}
