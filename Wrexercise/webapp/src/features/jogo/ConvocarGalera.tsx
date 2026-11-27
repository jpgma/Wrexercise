import { useState } from "react";
import { Dialog } from "@/components/Dialog";
import { useToast } from "@/components/Toast";
import {
  type TipoAtividade,
  TipoAtividadeIcone,
  tipoAtividadeLabels,
} from "@/features/atividades";
import { getErrorMessage } from "@/services/api";
import { CartaJogo } from "./CartaJogo";
import type { CartaMao } from "./useCartas";
import { useConvocar, usePreviaConvocacao } from "./useConvocacoes";

/**
 * Configura e joga o TODO MUNDO (decisão 0091): o anfitrião escolhe o tipo de treino e convoca a galera
 * do grupo por 7 dias. O servidor diz quantos serão convidados e o que impede convocar agora.
 */
export function ConvocarGalera({
  grupoId,
  carta,
  onClose,
}: {
  grupoId: string;
  carta: CartaMao | null;
  onClose: () => void;
}) {
  return (
    <Dialog open={carta !== null} title="Convocar a galera" onClose={onClose}>
      {carta && <Conteudo grupoId={grupoId} carta={carta} onClose={onClose} />}
    </Dialog>
  );
}

function Conteudo({
  grupoId,
  carta,
  onClose,
}: {
  grupoId: string;
  carta: CartaMao;
  onClose: () => void;
}) {
  const previa = usePreviaConvocacao(grupoId, carta.id);
  const convocar = useConvocar(grupoId);
  const { showToast } = useToast();
  const [tipo, setTipo] = useState<TipoAtividade | null>(null);

  if (previa.isPending)
    return (
      <p role="status" className="py-6 text-center text-sm text-muted">
        Carregando…
      </p>
    );
  if (previa.isError)
    return (
      <p role="alert" className="form-error">
        {getErrorMessage(previa.error)}
      </p>
    );

  const p = previa.data;
  const nomeTipo = tipo ? tipoAtividadeLabels[tipo] : null;

  return (
    <div className="space-y-4">
      <div className="flex gap-3">
        <CartaJogo carta={carta} />
        <div className="min-w-0 text-sm">
          <p className="font-semibold">
            "Essa semana todo mundo vai de {nomeTipo ?? "…"}!"
          </p>
          <ul className="mt-2 space-y-1 text-muted">
            <li>
              Vale por {p.duracaoDias} dias. Quem cumpre um treino verificado do
              tipo ganha +{p.recompensa} EN, você também.
            </li>
            <li>
              {p.convidados === 1
                ? "1 pessoa da galera vai ser convidada."
                : `${p.convidados} pessoas da galera vão ser convidadas.`}{" "}
              Recusar ou não cumprir dá FADIGA.
            </li>
            {p.convidados > 0 && (
              <li>
                Você recebe +{p.devolucaoPorPessoa} EN de volta por pessoa da
                galera que cumprir ou recusar, e +
                {Math.floor(p.devolucaoPorPessoa / 2)} EN de quem aceitar e não
                cumprir ou deixar passar.
              </li>
            )}
          </ul>
        </div>
      </div>

      {p.bloqueio ? (
        <p role="alert" className="form-error">
          {p.mensagem}
        </p>
      ) : (
        <>
          <fieldset>
            <legend className="mb-2 text-sm font-semibold">
              Escolha o treino
            </legend>
            <div className="grid grid-cols-3 gap-2">
              {p.tipos.map((t) => (
                <button
                  key={t}
                  type="button"
                  aria-pressed={tipo === t}
                  onClick={() => setTipo(t)}
                  className={`flex min-h-11 flex-col items-center gap-1 rounded-lg border-2 p-2 text-xs tap ${tipo === t ? "border-outline bg-reward text-on-reward" : "border-border bg-surface"}`}
                >
                  <TipoAtividadeIcone tipo={t} className="size-6" />
                  {tipoAtividadeLabels[t]}
                </button>
              ))}
            </div>
          </fieldset>

          {nomeTipo && (
            <p aria-live="polite" className="text-sm">
              Você vai convocar a galera para fazer pelo menos um treino de{" "}
              <strong>{nomeTipo}</strong> nos próximos {p.duracaoDias} dias.
            </p>
          )}

          {convocar.isError && (
            <p role="alert" className="form-error">
              {getErrorMessage(convocar.error)}
            </p>
          )}

          <button
            type="button"
            className="btn-primary h-12 w-full"
            disabled={!tipo || convocar.isPending}
            onClick={() =>
              tipo &&
              convocar.mutate(
                { cartaId: carta.id, tipo },
                {
                  onSuccess: () => {
                    showToast("Galera convocada! 📣");
                    onClose();
                  },
                },
              )
            }
          >
            Convocar a galera
          </button>
        </>
      )}
    </div>
  );
}
