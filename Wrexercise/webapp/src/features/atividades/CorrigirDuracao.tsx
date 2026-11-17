import { Pencil } from "lucide-react";
import { useId, useState } from "react";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { formatarDataRelativa, formatarDuracao } from "./labels";
import type { CorrecaoDuracao as Correcao } from "./types";
import { useCorrigirDuracao } from "./useAtividades";

const maximoMinutos = 1440;

/**
 * As correções já feitas, que todo o grupo vê, e o formulário do administrador (ADR 0068).
 * O servidor decide quem pode corrigir (`permitida`); a própria atividade nunca.
 */
export function CorrigirDuracao({
  atividadeId,
  grupoId,
  duracaoSegundos,
  permitida,
  correcoes,
}: {
  atividadeId: string;
  grupoId: string;
  duracaoSegundos: number;
  permitida: boolean;
  correcoes: Correcao[];
}) {
  const corrigir = useCorrigirDuracao(atividadeId, grupoId);
  const { showToast } = useToast();
  const erroId = useId();
  const [aberto, setAberto] = useState(false);
  const [minutos, setMinutos] = useState("");
  const [justificativa, setJustificativa] = useState("");

  if (!permitida && correcoes.length === 0) return null;

  const valor = Number(minutos);
  const erroMinutos =
    minutos === ""
      ? null
      : !Number.isInteger(valor) || valor < 1 || valor > maximoMinutos
        ? `Use de 1 a ${maximoMinutos} minutos.`
        : valor * 60 === duracaoSegundos
          ? "Esta já é a duração registrada."
          : null;
  const pronto =
    minutos !== "" && erroMinutos === null && justificativa.trim().length > 0;

  const abrir = () => {
    setMinutos(String(Math.round(duracaoSegundos / 60)));
    setJustificativa("");
    setAberto(true);
  };

  const fechar = () => {
    corrigir.reset();
    setAberto(false);
  };

  return (
    <div className="mt-5 space-y-3 border-t border-border pt-4">
      {correcoes.length > 0 && (
        <ul className="space-y-2">
          {correcoes.map((correcao) => (
            <li key={correcao.criadaEm} className="text-sm text-muted">
              <b className="font-semibold text-foreground">
                {correcao.responsavelNome.trim().split(/\s+/)[0]}
              </b>{" "}
              corrigiu a duração de{" "}
              {formatarDuracao(correcao.duracaoAnteriorSegundos)} para{" "}
              {formatarDuracao(correcao.duracaoNovaSegundos)} ·{" "}
              {formatarDataRelativa(correcao.criadaEm)}
              <span className="block break-words">
                “{correcao.justificativa}”
              </span>
            </li>
          ))}
        </ul>
      )}

      {permitida && !aberto && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-muted">Duração errada?</p>
          <button type="button" className="btn-ghost" onClick={abrir}>
            <Pencil aria-hidden="true" className="size-4" />
            Corrigir duração
          </button>
        </div>
      )}

      {permitida && aberto && (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (!pronto) return;
            corrigir.mutate(
              {
                duracaoSegundos: valor * 60,
                justificativa: justificativa.trim(),
              },
              {
                onSuccess: () => {
                  showToast("Duração corrigida.");
                  setAberto(false);
                },
              },
            );
          }}
        >
          <h3 className="text-base font-bold">Corrigir duração</h3>
          <p className="mt-1 text-sm text-muted">
            O grupo vê quem corrigiu e o motivo. A meta e a Energia são
            recalculadas.
          </p>
          <label className="mt-4 block">
            <span className="input-label">Duração em minutos</span>
            <input
              type="number"
              inputMode="numeric"
              min={1}
              max={maximoMinutos}
              step={1}
              required
              className="input-field"
              value={minutos}
              aria-invalid={erroMinutos !== null}
              aria-describedby={erroMinutos ? erroId : undefined}
              onChange={(event) => setMinutos(event.target.value)}
            />
          </label>
          {erroMinutos ? (
            <p id={erroId} role="alert" className="input-error">
              {erroMinutos}
            </p>
          ) : (
            minutos !== "" && (
              <p className="mt-1 text-xs text-muted">
                {formatarDuracao(duracaoSegundos)} →{" "}
                {formatarDuracao(valor * 60)}
              </p>
            )
          )}
          <label className="mt-3 block">
            <span className="input-label">Justificativa</span>
            <textarea
              className="input-field h-auto min-h-20 resize-y py-3"
              maxLength={500}
              required
              value={justificativa}
              onChange={(event) => setJustificativa(event.target.value)}
              placeholder="Ex.: o relógio da foto mostra 1h41."
            />
          </label>
          <button
            type="submit"
            className="btn-primary mt-4 w-full"
            disabled={corrigir.isPending || !pronto}
          >
            {corrigir.isPending ? "Salvando…" : "Salvar duração"}
          </button>
          <button
            type="button"
            className="btn-ghost mt-2 w-full"
            disabled={corrigir.isPending}
            onClick={fechar}
          >
            Cancelar
          </button>
          {corrigir.isError && (
            <p role="alert" className="form-error mt-3">
              {getErrorMessage(corrigir.error)}
            </p>
          )}
        </form>
      )}
    </div>
  );
}
