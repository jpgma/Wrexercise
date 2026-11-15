import { WifiOff } from "lucide-react";
import { Navigate, Outlet, useLocation } from "react-router";
import { EmptyState } from "@/components/EmptyState";
import { SplashScreen } from "@/components/SplashScreen";
import { useConfiguracaoInicial, useSessao } from "@/features/autenticacao";

const configuracaoInicialPath = "/configuracao-inicial";

type GateProps = {
  allow: "logado" | "anonimo";
  redirectTo: string;
};

// Só decide o que mostrar: quem protege os dados continua sendo o backend.
function SessaoGate({ allow, redirectTo }: GateProps) {
  const { data: usuario, isPending, fetchStatus, refetch } = useSessao();
  // Só quem não tem sessão precisa saber se o app já foi configurado.
  const configuracao = useConfiguracaoInicial(
    allow === "anonimo" && usuario === null,
  );
  const location = useLocation();

  // Com o dado já em cache, uma falha ao revalidar não tira a pessoa da tela atual.
  if (usuario === undefined) {
    // Sem rede, a consulta fica pausada até a conexão voltar: em vez de carregar para sempre, avisa.
    if (isPending && fetchStatus !== "paused") return <SplashScreen />;

    return (
      <main className="flex min-h-dvh flex-col items-center justify-center">
        <EmptyState
          icon={WifiOff}
          title="Você está sem conexão"
          description="Confira sua internet. Assim que voltar, a gente atualiza tudo."
          action={
            <button
              type="button"
              className="btn-secondary"
              onClick={() => void refetch()}
            >
              Tentar de novo
            </button>
          }
        />
      </main>
    );
  }

  const isLogado = usuario !== null;
  if (isLogado !== (allow === "logado")) {
    // Sem sessão, guarda o destino: o link do Movr (ou de uma notificação) volta para ele depois de entrar.
    const state =
      allow === "logado"
        ? { redirectTo: `${location.pathname}${location.search}` }
        : undefined;
    return (
      <Navigate
        to={getStateRedirect(location.state) ?? redirectTo}
        state={state}
        replace
      />
    );
  }

  // Sem usuários no app, as telas de entrada dão lugar à configuração inicial.
  if (!isLogado && allow === "anonimo") {
    if (configuracao.isPending && configuracao.fetchStatus !== "paused") {
      return <SplashScreen />;
    }
    const naConfiguracao = location.pathname === configuracaoInicialPath;
    if (configuracao.data === true && !naConfiguracao) {
      return <Navigate to={configuracaoInicialPath} replace />;
    }
    if (configuracao.data !== true && naConfiguracao) {
      return <Navigate to="/entrar" replace />;
    }
  }

  return <Outlet />;
}

/** Quem veio de um convite ou de um link protegido volta para ele depois de entrar. */
function getStateRedirect(state: unknown): string | undefined {
  const target = (state as { redirectTo?: unknown } | null)?.redirectTo;
  // Só caminhos internos: "//site" seria interpretado como outro domínio.
  return typeof target === "string" &&
    target.startsWith("/") &&
    !target.startsWith("//")
    ? target
    : undefined;
}

export function RequireSessao() {
  return <SessaoGate allow="logado" redirectTo="/entrar" />;
}

export function RequireAnonimo() {
  return <SessaoGate allow="anonimo" redirectTo="/" />;
}
