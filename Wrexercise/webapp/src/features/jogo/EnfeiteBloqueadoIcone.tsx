import { Lock } from "lucide-react";
import { EnfeiteIcone } from "./EnfeiteIcone";

/** O desenho de um Enfeite ainda bloqueado: cinza, esmaecido e com cadeado. */
export function EnfeiteBloqueadoIcone({
  conquista,
  tamanho = 32,
}: {
  conquista: string;
  tamanho?: number;
}) {
  return (
    <span className="relative inline-grid place-items-center">
      <EnfeiteIcone
        conquista={conquista}
        tamanho={tamanho}
        className="opacity-40 grayscale"
      />
      <Lock
        aria-hidden="true"
        className="absolute -right-1 -bottom-1 size-4 rounded-full bg-surface p-0.5 text-muted shadow-card"
      />
    </span>
  );
}
