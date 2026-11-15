import { ChevronRight, CloudOff, Users } from "lucide-react";
import { Link, Navigate, useParams } from "react-router";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import { useGrupos } from "@/features/grupos";
import { appIntegracaoLabels, useRascunho } from "@/features/integracoes";
import { getErrorMessage } from "@/services/api";

/**
 * Porta de entrada de um rascunho do Movr (ADR 0065). Uma atividade é sempre de um grupo: com um grupo só,
 * segue direto para o registro; com mais de um, a pessoa escolhe onde postar.
 */
export function ImportarRascunhoPage() {
  const { id: rascunhoId = "" } = useParams();
  const rascunho = useRascunho(rascunhoId);
  const grupos = useGrupos();

  if (rascunho.isPending || grupos.isPending) return <SplashScreen />;

  if (rascunho.isError || grupos.isError) {
    return (
      <div className="card mx-auto max-w-xl">
        <EmptyState
          icon={CloudOff}
          title="Não deu para abrir o treino"
          description={getErrorMessage(rascunho.error ?? grupos.error)}
          action={
            <Link to="/" className="btn-secondary">
              Ir para o início
            </Link>
          }
        />
      </div>
    );
  }

  const { data } = rascunho;
  if (data.situacao === "Postada" && data.atividadeId) {
    return <Navigate to={`/atividades/${data.atividadeId}`} replace />;
  }

  const app = appIntegracaoLabels[data.app];
  if (data.situacao === "Expirada") {
    return (
      <div className="card mx-auto max-w-xl">
        <EmptyState
          icon={CloudOff}
          title="Esse treino passou do prazo"
          description={`O registro aceita treinos das últimas 48 horas. O rascunho do ${app} não vale mais.`}
          action={
            <Link to="/" className="btn-secondary">
              Ir para o início
            </Link>
          }
        />
      </div>
    );
  }

  const destino = (grupoId: string) =>
    `/grupos/${grupoId}/atividades/nova?rascunho=${data.id}`;

  if (grupos.data.length === 1) {
    return <Navigate to={destino(grupos.data[0]!.id)} replace />;
  }

  if (grupos.data.length === 0) {
    return (
      <div className="card mx-auto max-w-xl">
        <EmptyState
          icon={Users}
          title="Você ainda não tem grupo"
          description="Uma atividade é sempre postada num grupo. Crie um ou entre com um convite."
          action={
            <Link to="/" className="btn-primary">
              Ir para o início
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl animate-slide-up">
      <PageHeader title="Postar em qual grupo?" back="/" />
      <p className="mb-4 text-muted">
        O treino do {app} vai para o grupo que você escolher.
      </p>
      <ul className="space-y-2">
        {grupos.data.map((grupo) => (
          <li key={grupo.id}>
            <Link
              to={destino(grupo.id)}
              replace
              className="card flex min-h-14 items-center gap-3 p-4 tap"
            >
              <Users
                aria-hidden="true"
                className="size-5 shrink-0 text-muted"
              />
              <span className="flex-1 font-semibold">{grupo.nome}</span>
              <ChevronRight aria-hidden="true" className="size-5 text-muted" />
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
