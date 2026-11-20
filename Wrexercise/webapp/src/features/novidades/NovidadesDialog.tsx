import { useState } from "react";
import { Dialog } from "@/components/Dialog";
import { ListaVersoes } from "./ListaVersoes";
import {
  getNovidadesPendentes,
  getVersaoVista,
  marcarVersaoVista,
} from "./versoes";

/**
 * As novidades das versões novas, uma vez por aparelho, na primeira abertura depois da atualização
 * (decisão 0077). Com mais de uma versão, cada uma é um passo, da mais antiga para a mais nova, e
 * "Bora!" só aparece no último. Cada passo visto fica gravado: quem fecha no meio vê só o resto da
 * próxima vez. `pausado` segura o diálogo enquanto algo mais urgente toma a tela, como a Chegada de
 * uma carta; ele aparece quando isso acaba.
 */
export function NovidadesDialog({ pausado }: { pausado: boolean }) {
  const [pendentes, setPendentes] = useState(() =>
    getNovidadesPendentes(getVersaoVista()),
  );
  const [passo, setPasso] = useState(0);

  const atual = pendentes[passo];
  const total = pendentes.length;
  const ultimo = passo === total - 1;

  const fechar = () => {
    if (atual) marcarVersaoVista(atual.versao);
    setPendentes([]);
  };

  const avancar = () => {
    if (atual) marcarVersaoVista(atual.versao);
    setPasso(passo + 1);
  };

  return (
    <Dialog open={total > 0 && !pausado} title="Novidades" onClose={fechar}>
      {total > 1 && (
        <p
          className="-mt-2 mb-3 text-xs font-semibold text-muted"
          aria-live="polite"
        >
          {passo + 1} de {total}
        </p>
      )}
      <div className="max-h-[60dvh] overflow-y-auto">
        {atual && <ListaVersoes key={atual.versao} versoes={[atual]} />}
      </div>
      {total > 1 && (
        <div className="mt-4 flex justify-center gap-1.5" aria-hidden="true">
          {pendentes.map(({ versao }, i) => (
            <span
              key={versao}
              className={`size-2 rounded-full ${i === passo ? "bg-primary" : "bg-muted/40"}`}
            />
          ))}
        </div>
      )}
      <div className="mt-5 flex gap-3">
        {passo > 0 && (
          <button
            type="button"
            className="btn-secondary flex-1"
            onClick={() => setPasso(passo - 1)}
          >
            Voltar
          </button>
        )}
        {ultimo ? (
          <button type="button" className="btn-primary flex-1" onClick={fechar}>
            Bora!
          </button>
        ) : (
          <button
            type="button"
            className="btn-primary flex-1"
            onClick={avancar}
          >
            Próxima
          </button>
        )}
      </div>
    </Dialog>
  );
}
