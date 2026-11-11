import { Link } from "react-router";
import { Dialog } from "@/components/Dialog";
import { useVitrine } from "@/components/folhaAvatar";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { primeiroNome } from "./textosCartas";
import type { MembroCena } from "./useCena";

/** A vitrine de Enfeites de quem foi tocado; sem Enfeites, não mostra nada. */
function Vitrine({ usuarioId }: { usuarioId: string }) {
  const vitrine = useVitrine(usuarioId);
  if (vitrine.length === 0) return null;
  return (
    <div>
      <p className="eyebrow mb-2">Vitrine</p>
      <ul className="flex gap-4">
        {vitrine.map((enfeite) => (
          <li key={enfeite.codigo} className="flex flex-col items-center gap-1">
            <EnfeiteIcone conquista={enfeite.conquista} tamanho={64} />
            <span className="text-2xs text-muted">{enfeite.nome}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Tocar no próprio avatar: a Vitrine e o acesso à própria página. Tocar num colega abre o Foco
 * (useFoco.ts), onde ficam as cartas e o Rival.
 */
export function MenuAvatar({
  grupoId,
  membro,
  onClose,
}: {
  grupoId: string;
  membro: MembroCena | null;
  onClose: () => void;
}) {
  return (
    <Dialog
      open={membro !== null}
      title={membro ? primeiroNome(membro.nomeExibicao) : ""}
      onClose={onClose}
    >
      {membro && (
        <div className="space-y-5">
          <Vitrine usuarioId={membro.usuarioId} />
          <Link
            to={`/grupos/${grupoId}/membros/${membro.usuarioId}`}
            className="btn-secondary w-full"
          >
            Ver a minha página
          </Link>
        </div>
      )}
    </Dialog>
  );
}
