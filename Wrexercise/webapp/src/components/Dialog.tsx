import { X } from "lucide-react";
import { type ReactNode, useEffect, useId, useRef } from "react";

type DialogProps = {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
};

/**
 * Janela com o `<dialog>` nativo, que já prende o foco, fecha com Esc e devolve o foco a quem abriu.
 * Para confirmações simples, use `ConfirmDialog`.
 */
export function Dialog({ open, title, onClose, children }: DialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const tituloId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={tituloId}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        // Toque no fundo escurecido fecha a janela.
        if (event.target === event.currentTarget) onClose();
      }}
      className="card-sticker m-auto w-[calc(100%-2rem)] max-w-sm p-0 text-foreground backdrop:bg-scrim backdrop:backdrop-blur-sm"
    >
      <div className="p-5">
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 id={tituloId} className="min-w-0 text-2xl uppercase">
            {title}
          </h2>
          <button
            type="button"
            aria-label="Fechar"
            className="-mt-1 -mr-2 grid size-11 shrink-0 place-items-center rounded-lg tap hover:bg-surface-muted"
            onClick={onClose}
          >
            <X aria-hidden="true" className="size-5" />
          </button>
        </div>
        {open && children}
      </div>
    </dialog>
  );
}
