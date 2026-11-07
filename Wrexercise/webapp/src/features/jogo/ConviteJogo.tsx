import { Gamepad2 } from "lucide-react";
import { useState } from "react";
import { useAlterarModoInicio } from "@/features/autenticacao";
import { getErrorMessage } from "@/services/api";

// A dispensa fica num cookie comum, não em `localStorage`, que o ESLint proíbe.
const cookieDispensa = "wx_convite_jogo_dispensado";
const segundosDispensa = 400 * 24 * 60 * 60; // o máximo que os navegadores aceitam

function foiDispensado(): boolean {
  return document.cookie
    .split(";")
    .some((parte) => parte.trim().startsWith(`${cookieDispensa}=`));
}

function dispensar() {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieDispensa}=1; Max-Age=${segundosDispensa}; Path=/; SameSite=Lax${seguro}`;
}

/** Convite único, na tela inicial do modo App, para experimentar o modo Jogo. */
export function ConviteJogo() {
  const [dispensado, setDispensado] = useState(foiDispensado);
  const alterar = useAlterarModoInicio();

  if (dispensado) return null;

  return (
    <section className="card-sticker mt-6 flex gap-3 p-4">
      <Gamepad2 aria-hidden="true" className="size-6 shrink-0 text-link" />
      <div className="min-w-0 flex-1">
        <p className="font-display text-xl uppercase">
          Experimente o modo Jogo
        </p>
        <p className="mt-1 text-sm text-muted">
          Veja o seu grupo como uma turma na praça. Dá para voltar quando
          quiser, em Configurações.
        </p>
        {alterar.isError && (
          <p role="alert" className="form-error mt-2">
            {getErrorMessage(alterar.error)}
          </p>
        )}
        <div className="mt-3 flex gap-2">
          <button
            type="button"
            className="btn-primary flex-1"
            disabled={alterar.isPending}
            onClick={() => alterar.mutate("Jogo")}
          >
            Experimentar
          </button>
          <button
            type="button"
            className="btn-secondary flex-1"
            onClick={() => {
              dispensar();
              setDispensado(true);
            }}
          >
            Agora não
          </button>
        </div>
      </div>
    </section>
  );
}
