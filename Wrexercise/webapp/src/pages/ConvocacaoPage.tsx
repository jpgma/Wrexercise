import { useNavigate, useParams } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { DetalheConvocacao } from "@/features/jogo";

export function ConvocacaoPage() {
  const navigate = useNavigate();
  const { id = "", convocacaoId = "" } = useParams();

  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="TODO MUNDO"
        subtitle="A convocação da galera"
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
      <DetalheConvocacao grupoId={id} convocacaoId={convocacaoId} />
    </div>
  );
}
