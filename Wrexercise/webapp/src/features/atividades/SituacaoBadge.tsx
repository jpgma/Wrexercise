import {
  AlertTriangle,
  Ban,
  CheckCircle2,
  Hourglass,
  MessageCircleWarning,
  XCircle,
  type LucideIcon,
} from "lucide-react";
import { situacaoVerificacaoLabels } from "./labels";
import type { SituacaoVerificacao } from "./types";

const estilos: Record<
  SituacaoVerificacao,
  { classe: string; Icon: LucideIcon }
> = {
  AguardandoFeedback: { classe: "", Icon: Hourglass },
  Verificada: { classe: "chip-success", Icon: CheckCircle2 },
  EmRevisao: { classe: "chip-warning", Icon: AlertTriangle },
  Contestada: { classe: "chip-warning", Icon: MessageCircleWarning },
  Rejeitada: { classe: "chip-danger", Icon: XCircle },
  Anulada: { classe: "", Icon: Ban },
};

export function SituacaoBadge({ situacao }: { situacao: SituacaoVerificacao }) {
  const { classe, Icon } = estilos[situacao];
  return (
    <span className={`chip ${classe}`}>
      <Icon aria-hidden="true" className="size-3" />
      {situacaoVerificacaoLabels[situacao]}
    </span>
  );
}
