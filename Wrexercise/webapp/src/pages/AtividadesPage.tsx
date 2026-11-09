import { ListChecks } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import {
  useAtividadesFeed,
  type FiltroAtividades,
} from "@/features/atividades";
import { FeedAtividade } from "@/features/feed";
import { getErrorMessage } from "@/services/api";

const filtros: { valor: FiltroAtividades | undefined; rotulo: string }[] = [
  { valor: undefined, rotulo: "Todas" },
  { valor: "Minhas", rotulo: "Minhas" },
  { valor: "Pendentes", rotulo: "Pendentes" },
  { valor: "Verificadas", rotulo: "Verificadas" },
];

export function AtividadesPage() {
  const { id = "" } = useParams();
  const [filtro, setFiltro] = useState<FiltroAtividades | undefined>();
  const feed = useAtividadesFeed(id, 20, filtro);
  const atividades = feed.data?.pages.flatMap((p) => p.atividades) ?? [];

  return (
    <>
      <PageHeader title="Atividades" subtitle="Todos os treinos do grupo" />

      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:px-0">
        {filtros.map(({ valor, rotulo }) => (
          <button
            key={rotulo}
            type="button"
            aria-pressed={filtro === valor}
            onClick={() => setFiltro(valor)}
            className={`h-9 shrink-0 rounded-full border px-4 text-sm font-medium transition-colors ${
              filtro === valor
                ? "border-primary bg-primary text-on-primary"
                : "border-border bg-surface text-muted"
            }`}
          >
            {rotulo}
          </button>
        ))}
      </div>

      {feed.isPending && (
        <p role="status" className="py-8 text-center text-sm text-muted">
          Carregando…
        </p>
      )}
      {feed.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(feed.error)}
        </p>
      )}
      {feed.data && atividades.length === 0 && (
        <div className="card">
          <EmptyState
            icon={ListChecks}
            title={filtro ? "Nada por aqui" : "Nenhuma atividade ainda"}
            description={
              filtro === "Pendentes"
                ? "Nenhuma atividade aguardando avaliação. Tudo em dia!"
                : filtro
                  ? "Ainda não há atividades nesse filtro."
                  : "Quando alguém do grupo registrar um treino, ele aparece aqui."
            }
            action={
              <Link
                to={`/grupos/${id}/atividades/nova`}
                className="btn-primary"
              >
                Registrar atividade
              </Link>
            }
          />
        </div>
      )}

      <div className="grid gap-3 lg:grid-cols-2">
        {atividades.map((atividade) => (
          <FeedAtividade key={atividade.id} atividade={atividade} />
        ))}
      </div>

      {feed.hasNextPage && (
        <button
          type="button"
          className="btn-secondary mt-3 w-full"
          disabled={feed.isFetchingNextPage}
          onClick={() => void feed.fetchNextPage()}
        >
          {feed.isFetchingNextPage ? "Carregando…" : "Carregar mais"}
        </button>
      )}
    </>
  );
}
