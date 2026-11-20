import { PageHeader } from "@/components/PageHeader";
import { ListaVersoes, versoes } from "@/features/novidades";

/** O histórico de versões do app, da mais nova para a mais antiga (decisão 0077). */
export function NovidadesPage() {
  return (
    <div className="mx-auto max-w-xl">
      <PageHeader title="Novidades" back="/perfil/configuracoes" />
      <div className="card p-5">
        <ListaVersoes versoes={versoes} />
      </div>
    </div>
  );
}
