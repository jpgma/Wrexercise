import { useQueryClient } from "@tanstack/react-query";
import { ImageIcon, RotateCcw } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useFolhaAvatar } from "@/components/folhaAvatar";
import { useSessao } from "@/features/autenticacao";
import { getErrorMessage, getJson } from "@/services/api";
import { desenharImagemPercurso } from "./imagemPercurso";
import { enviarImagemPercurso, type Percurso } from "./usePercurso";

type Estado =
  | { fase: "gerando" }
  | { fase: "pronta"; url: string }
  | { fase: "erro"; mensagem: string }
  | { fase: "cheia" };

/**
 * Depois de "Terminar treino" (ADR 0084): desenha a imagem do Percurso, como o grupo o vê, e a anexa à
 * atividade com o selo "Percurso". Nunca trava a tela: falhou, a pessoa tenta de novo ou segue sem ela.
 */
export function GerarImagemPercurso({
  atividadeId,
  inicio,
  totalEvidencias,
}: {
  atividadeId: string;
  inicio: string;
  totalEvidencias: number;
}) {
  const queryClient = useQueryClient();
  const sessao = useSessao().data;
  const folhaAvatar = useFolhaAvatar(sessao?.id);
  const [estado, setEstado] = useState<Estado>({ fase: "gerando" });
  const modo = sessao?.modoInicio === "Jogo" ? "Jogo" : "App";
  const feito = useRef(false);

  const gerar = useCallback(async () => {
    if (totalEvidencias >= 10) {
      setEstado({ fase: "cheia" });
      return;
    }
    setEstado({ fase: "gerando" });
    try {
      const percurso = await getJson<Percurso>(
        `/api/atividades/${atividadeId}/percurso?aparado=true`,
      );
      const { imagem, miniatura } = await desenharImagemPercurso(percurso, {
        modo,
        folhaAvatar,
        inicio: new Date(inicio),
      });
      await enviarImagemPercurso(atividadeId, imagem, miniatura);
      setEstado({ fase: "pronta", url: URL.createObjectURL(imagem) });
      void queryClient.invalidateQueries({ queryKey: ["atividades"] });
    } catch (error) {
      setEstado({ fase: "erro", mensagem: getErrorMessage(error) });
    }
  }, [atividadeId, folhaAvatar, inicio, modo, queryClient, totalEvidencias]);

  useEffect(() => {
    if (feito.current || !sessao) return;
    feito.current = true;
    void gerar();
  }, [gerar, sessao]);

  useEffect(
    () => () => {
      if (estado.fase === "pronta") URL.revokeObjectURL(estado.url);
    },
    [estado],
  );

  return (
    <section
      aria-labelledby="imagem-percurso-titulo"
      className="card mt-6 w-full p-3 text-left"
    >
      <h2
        id="imagem-percurso-titulo"
        className="flex items-center gap-1.5 text-sm font-semibold"
      >
        <ImageIcon aria-hidden="true" className="size-4" />
        Imagem do percurso
      </h2>
      {estado.fase === "gerando" && (
        <>
          <div
            aria-hidden="true"
            className="mt-2 aspect-[4/5] w-full animate-pulse rounded-xl bg-surface-muted"
          />
          <p role="status" className="mt-2 text-xs text-muted">
            Desenhando o mapa e os números…
          </p>
        </>
      )}
      {estado.fase === "pronta" && (
        <>
          <img
            src={estado.url}
            alt="Imagem do percurso com o mapa, a distância, o ritmo e as parciais"
            className="mt-2 w-full rounded-xl border border-border"
          />
          <p role="status" className="mt-2 text-xs text-muted">
            Já está na atividade, com o selo "Percurso". Ela não muda a
            comprovação: as fotos continuam valendo.
          </p>
        </>
      )}
      {estado.fase === "cheia" && (
        <p className="mt-2 text-sm text-muted">
          A atividade já tem 10 fotos e vídeos: a imagem do percurso ficou de
          fora. O mapa continua na atividade.
        </p>
      )}
      {estado.fase === "erro" && (
        <>
          <p role="alert" className="form-error mt-2">
            {estado.mensagem}
          </p>
          <button
            type="button"
            className="btn-ghost mt-2 w-full"
            onClick={() => void gerar()}
          >
            <RotateCcw aria-hidden="true" className="size-4" />
            Tentar de novo
          </button>
        </>
      )}
    </section>
  );
}
