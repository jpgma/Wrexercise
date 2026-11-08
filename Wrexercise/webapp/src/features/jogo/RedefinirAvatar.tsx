import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { getErrorMessage } from "@/services/api";
import { useRedefinirAvatarMembro } from "./useAvatares";

type RedefinirAvatarProps = {
  grupoId: string;
  usuarioId: string;
  nome: string;
};

/** Medida contra imagens impróprias: só quem administra o grupo vê este botão. */
export function RedefinirAvatar({
  grupoId,
  usuarioId,
  nome,
}: RedefinirAvatarProps) {
  const [aberto, setAberto] = useState(false);
  const redefinir = useRedefinirAvatarMembro(grupoId);

  const fechar = () => {
    redefinir.reset();
    setAberto(false);
  };

  return (
    <>
      <div className="card mb-4 overflow-hidden">
        <button
          type="button"
          className="block w-full p-4 text-left text-sm font-semibold hover:bg-surface-muted"
          onClick={() => setAberto(true)}
        >
          Voltar o avatar ao padrão
        </button>
      </div>
      <ConfirmDialog
        open={aberto}
        title={`Voltar o avatar de ${nome} ao padrão?`}
        message="O avatar importado por essa pessoa será apagado. Ela pode importar outro quando quiser."
        confirmLabel="Voltar ao padrão"
        pending={redefinir.isPending}
        error={redefinir.isError ? getErrorMessage(redefinir.error) : undefined}
        onConfirm={() => redefinir.mutate(usuarioId, { onSuccess: fechar })}
        onCancel={fechar}
      />
    </>
  );
}
