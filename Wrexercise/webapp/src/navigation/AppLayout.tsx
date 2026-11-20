import { Home, ListChecks, Plus, User, Users } from "lucide-react";
import { useState } from "react";
import { Link, NavLink, Outlet, useMatch, useNavigate } from "react-router";
import { Logo } from "@/components/BrandMark";
import { useGrupos } from "@/features/grupos";
import {
  PortaoChegada,
  useCartaEsperando,
  useEmChegada,
} from "@/features/jogo";
import { NovidadesDialog } from "@/features/novidades";

type ItemNavegacao = {
  to: string;
  label: string;
  Icon: typeof Home;
  end?: boolean;
};

const classesSidebar = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 rounded-lg border-2 px-3 py-2.5 text-sm font-semibold transition-colors ${isActive ? "border-outline bg-soft text-on-soft" : "border-transparent text-muted hover:bg-surface-muted"}`;

const classesTabBar = ({ isActive }: { isActive: boolean }) =>
  `relative flex min-h-12 flex-col items-center justify-center gap-0.5 py-1.5 text-2xs font-semibold before:absolute before:top-0 before:h-1 before:w-8 before:rounded-b-sm ${isActive ? "text-foreground before:bg-primary" : "text-muted before:bg-transparent"}`;

/** O grupo atual vem da URL. Nas telas sem grupo na URL, vale o último visitado nesta sessão. */
function useGrupoAtualId(): string | null {
  const match = useMatch("/grupos/:id/*");
  const daUrl = match?.params.id !== "novo" ? match?.params.id : undefined;
  const grupos = useGrupos();
  const [ultimo, setUltimo] = useState<string | null>(null);

  if (daUrl && daUrl !== ultimo) setUltimo(daUrl);

  const lista = grupos.data ?? [];
  if (daUrl) return daUrl;
  // Quem saiu de um grupo não deve voltar para ele pelas abas.
  if (ultimo && lista.some(({ id }) => id === ultimo)) return ultimo;
  return lista[0]?.id ?? null;
}

function SeletorDeGrupo({ grupoId }: { grupoId: string }) {
  const grupos = useGrupos();
  const navigate = useNavigate();

  if (!grupos.data || grupos.data.length < 2) return null;

  return (
    <label className="mb-4 block">
      <span className="sr-only">Trocar de grupo</span>
      <select
        className="input-field h-10 font-semibold"
        value={grupoId}
        onChange={(event) =>
          void navigate(`/grupos/${event.target.value}/inicio`)
        }
      >
        {grupos.data.map(({ id, nome }) => (
          <option key={id} value={id}>
            {nome}
          </option>
        ))}
      </select>
    </label>
  );
}

export function AppLayout() {
  const grupoId = useGrupoAtualId();
  const prefixo = grupoId ? `/grupos/${grupoId}` : null;

  const principais: ItemNavegacao[] = [
    { to: prefixo ? `${prefixo}/inicio` : "/", label: "Início", Icon: Home },
    {
      to: prefixo ? `${prefixo}/atividades` : "/",
      label: "Atividades",
      Icon: ListChecks,
      end: true,
    },
  ];
  const secundarios: ItemNavegacao[] = [
    { to: prefixo ?? "/", label: "Grupo", Icon: Users, end: true },
    { to: "/perfil", label: "Perfil", Icon: User },
  ];
  const registrar = prefixo ? `${prefixo}/atividades/nova` : "/";
  // Na Chegada de uma carta, a resposta vem antes de tudo: sem menu, abas nem troca de grupo (decisão 0076).
  const emChegada = useEmChegada();
  const cartaEsperando = useCartaEsperando();

  if (emChegada) {
    return (
      <div className="min-h-dvh">
        <PortaoChegada />
        <main className="pb-12">
          <div className="mx-auto w-full max-w-5xl animate-fade-in px-4 pt-4 sm:px-6">
            <Outlet />
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-dvh">
      <PortaoChegada />
      <NovidadesDialog pausado={cartaEsperando} />
      <aside className="fixed inset-y-0 left-0 hidden w-64 flex-col border-r border-border bg-surface p-5 lg:flex">
        <Logo className="mb-8 px-2" />
        <nav aria-label="Principal" className="flex flex-col gap-1">
          {[...principais, ...secundarios].map(({ to, label, Icon, end }) => (
            <NavLink key={label} to={to} end={end} className={classesSidebar}>
              <Icon aria-hidden="true" className="size-5" />
              {label}
            </NavLink>
          ))}
        </nav>
        <Link
          to={registrar}
          className="btn-primary mt-6 h-12 px-3 whitespace-nowrap"
        >
          <Plus aria-hidden="true" className="size-5" />
          Registrar atividade
        </Link>
      </aside>

      <main className="pb-safe lg:pb-12 lg:pl-64">
        <div className="mx-auto w-full max-w-5xl animate-fade-in px-4 pt-4 sm:px-6 lg:px-10 lg:pt-10">
          {grupoId && <SeletorDeGrupo grupoId={grupoId} />}
          <Outlet />
        </div>
      </main>

      <nav
        aria-label="Principal"
        className="fixed inset-x-0 bottom-0 z-40 border-t-2 border-outline bg-surface/95 backdrop-blur lg:hidden"
        style={{ paddingBottom: "env(safe-area-inset-bottom)" }}
      >
        <div className="mx-auto grid max-w-md grid-cols-5 items-center px-2 py-1.5">
          {principais.map(({ to, label, Icon, end }) => (
            <NavLink key={label} to={to} end={end} className={classesTabBar}>
              <Icon aria-hidden="true" className="size-[22px]" />
              {label}
            </NavLink>
          ))}
          <Link
            to={registrar}
            aria-label="Registrar atividade"
            className="mx-auto -mt-6 grid size-14 rotate-[-3deg] place-items-center rounded-xl border-2 border-outline bg-primary text-on-primary shadow-sticker press"
          >
            <Plus aria-hidden="true" className="size-6" strokeWidth={2.5} />
          </Link>
          {secundarios.map(({ to, label, Icon, end }) => (
            <NavLink key={label} to={to} end={end} className={classesTabBar}>
              <Icon aria-hidden="true" className="size-[22px]" />
              {label}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  );
}
