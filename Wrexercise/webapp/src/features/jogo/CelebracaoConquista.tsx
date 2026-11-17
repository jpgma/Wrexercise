import { useState } from "react";
import { Link } from "react-router";
import { Dialog } from "@/components/Dialog";
import { EnfeiteIcone } from "./EnfeiteIcone";
import {
  type ConquistaNova,
  useMarcarEnfeitesVistos,
  useMeuAvatar,
} from "./useAvatares";

/**
 * Comemora as conquistas que a pessoa ainda não viu (decisão 0072), uma vez. No modo Jogo, mostra o
 * Enfeite que cada uma liberou ou evoluiu e leva à Vitrine; no modo App, fala só da conquista e leva
 * ao Perfil. Fechar marca tudo como visto no servidor.
 */
export function CelebracaoConquista({
  pronto = true,
  onVerVitrine,
}: {
  /** No Jogo, a janela espera a entrada e as encenações da praça. */
  pronto?: boolean;
  /** Só no modo Jogo: abre o próprio Foco, onde fica a Vitrine. */
  onVerVitrine?: () => void;
}) {
  const avatar = useMeuAvatar();
  const { mutate: marcarVistos } = useMarcarEnfeitesVistos();
  // A praça marca os Enfeites como vistos ao fim da encenação, o que esvazia a lista do servidor:
  // as conquistas ficam guardadas aqui até a pessoa fechar a janela.
  const [guardadas, setGuardadas] = useState<ConquistaNova[]>([]);
  const [fechadas, setFechadas] = useState<string[]>([]);
  const recebidas = avatar.data?.conquistasNovas ?? [];
  const ineditas = recebidas.filter(
    (c) => !guardadas.some((g) => g.conquista === c.conquista),
  );
  if (ineditas.length > 0) setGuardadas([...guardadas, ...ineditas]);

  const novas = guardadas.filter((c) => !fechadas.includes(c.conquista));
  const naVitrine = (c: ConquistaNova) =>
    avatar.data?.vitrine.some((e) => e.codigo === c.enfeite) ?? false;
  const fechar = () => {
    setFechadas(guardadas.map((c) => c.conquista));
    if (recebidas.length > 0) marcarVistos();
  };

  return (
    <Dialog
      open={pronto && novas.length > 0}
      title={novas.length > 1 ? "Conquistas liberadas!" : "Conquista liberada!"}
      onClose={fechar}
    >
      <ul className="max-h-[50vh] space-y-3 overflow-y-auto">
        {novas.map((c) => (
          <li key={c.conquista} className="flex items-center gap-3">
            <div className="grid size-16 shrink-0 animate-pop place-items-center rounded-lg bg-surface-muted">
              <EnfeiteIcone conquista={c.conquista} tamanho={48} />
            </div>
            <div className="min-w-0">
              <p className="font-semibold">{c.titulo}</p>
              {onVerVitrine && (
                <p className="text-sm text-muted">
                  Você ganhou o Enfeite {c.nomeEnfeite}.
                  {naVitrine(c) && " Já está na sua Vitrine."}
                </p>
              )}
            </div>
          </li>
        ))}
      </ul>
      {onVerVitrine ? (
        <button
          type="button"
          className="btn-primary mt-5 w-full"
          onClick={() => {
            fechar();
            onVerVitrine();
          }}
        >
          {novas.every(naVitrine) ? "Ver na Vitrine" : "Pôr na Vitrine"}
        </button>
      ) : (
        <Link to="/perfil" className="btn-primary mt-5 w-full" onClick={fechar}>
          Ver no Perfil
        </Link>
      )}
    </Dialog>
  );
}
