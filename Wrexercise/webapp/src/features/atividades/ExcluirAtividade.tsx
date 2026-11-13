import { Trash2 } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { useExcluirAtividade } from "./useAtividades";

const formatoHora = new Intl.DateTimeFormat("pt-BR", { timeStyle: "short" });

/**
 * O autor apaga um registro errado ou em dobro nos primeiros 15 minutos, antes de alguém contestar
 * (ADR 0054). O servidor decide o prazo; aqui só aparece enquanto `permitidaAte` não passou.
 */
export function ExcluirAtividade({
  atividadeId,
  grupoId,
  permitidaAte,
}: {
  atividadeId: string;
  grupoId: string;
  permitidaAte: string;
}) {
  const excluir = useExcluirAtividade(atividadeId, grupoId);
  const navigate = useNavigate();
  const { showToast } = useToast();
  const [confirmando, setConfirmando] = useState(false);
  // Sem relógio na tela: se o prazo passar com a página aberta, o servidor recusa e o erro aparece.
  const [aberta] = useState(
    () => new Date(permitidaAte).getTime() > Date.now(),
  );

  if (!aberta) return null;

  return (
    <div className="mt-5 flex flex-wrap items-center justify-between gap-2 border-t border-border pt-4">
      <p className="text-sm text-muted">
        Registrou errado? Dá para apagar até{" "}
        {formatoHora.format(new Date(permitidaAte))}.
      </p>
      <button
        type="button"
        className="btn-danger"
        onClick={() => setConfirmando(true)}
      >
        <Trash2 aria-hidden="true" className="size-4" />
        Apagar
      </button>
      <ConfirmDialog
        open={confirmando}
        title="Apagar atividade?"
        message="O registro, as fotos, as reações e os comentários somem para todos. Não dá para desfazer."
        confirmLabel="Apagar atividade"
        pending={excluir.isPending}
        error={excluir.isError ? getErrorMessage(excluir.error) : undefined}
        onConfirm={() =>
          excluir.mutate(undefined, {
            onSuccess: () => {
              showToast("Atividade apagada.");
              void navigate(`/grupos/${grupoId}/atividades`, { replace: true });
            },
          })
        }
        onCancel={() => {
          excluir.reset();
          setConfirmando(false);
        }}
      />
    </div>
  );
}
