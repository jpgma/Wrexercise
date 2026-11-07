import { useNavigate } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { LojaCartas } from "@/features/jogo";

export function LojaPage() {
  const navigate = useNavigate();

  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="Loja"
        subtitle="Cartas para provocar a turma"
        action={
          <button
            type="button"
            className="btn-ghost"
            onClick={() => void navigate(-1)}
          >
            Voltar
          </button>
        }
      />
      <LojaCartas />
    </div>
  );
}
