import { useState } from "react";
import { Dialog } from "@/components/Dialog";
import { ListaVersoes } from "./ListaVersoes";
import {
  getNovidadesPendentes,
  getVersaoVista,
  marcarVersaoVista,
} from "./versoes";

/**
 * As novidades da versão nova, uma vez por aparelho, na primeira abertura depois da atualização
 * (decisão 0077). `pausado` segura o diálogo enquanto algo mais urgente toma a tela, como a Chegada
 * de uma carta; ele aparece quando isso acaba.
 */
export function NovidadesDialog({ pausado }: { pausado: boolean }) {
  const [pendentes, setPendentes] = useState(() =>
    getNovidadesPendentes(getVersaoVista()),
  );

  const fechar = () => {
    marcarVersaoVista();
    setPendentes([]);
  };

  return (
    <Dialog
      open={pendentes.length > 0 && !pausado}
      title="Novidades"
      onClose={fechar}
    >
      <div className="max-h-[60dvh] overflow-y-auto">
        <ListaVersoes versoes={pendentes} />
      </div>
      <button
        type="button"
        className="btn-primary mt-5 w-full"
        onClick={fechar}
      >
        Bora!
      </button>
    </Dialog>
  );
}
