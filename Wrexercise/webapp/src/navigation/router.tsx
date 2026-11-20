import { createBrowserRouter, Navigate } from "react-router";
import { SplashScreen } from "@/components/SplashScreen";
import { AppLayout } from "@/navigation/AppLayout";
import { RequireAnonimo, RequireSessao } from "@/navigation/guards";

// Toda página é carregada sob demanda para manter o bundle inicial pequeno.
export const router = createBrowserRouter([
  {
    HydrateFallback: SplashScreen,
    children: [
      {
        Component: RequireAnonimo,
        children: [
          {
            path: "/entrar",
            lazy: async () => {
              const { EntrarPage } = await import("@/pages/EntrarPage");
              return { Component: EntrarPage };
            },
          },
          {
            path: "/configuracao-inicial",
            lazy: async () => {
              const { ConfiguracaoInicialPage } =
                await import("@/pages/ConfiguracaoInicialPage");
              return { Component: ConfiguracaoInicialPage };
            },
          },
          {
            path: "/criar-conta",
            lazy: async () => {
              const { CriarContaPage } = await import("@/pages/CriarContaPage");
              return { Component: CriarContaPage };
            },
          },
        ],
      },
      {
        Component: RequireSessao,
        children: [
          {
            Component: AppLayout,
            children: [
              {
                path: "/",
                lazy: async () => {
                  const { RaizPage } = await import("@/pages/RaizPage");
                  return { Component: RaizPage };
                },
              },
              {
                path: "/perfil",
                lazy: async () => {
                  const { PerfilPage } = await import("@/pages/PerfilPage");
                  return { Component: PerfilPage };
                },
              },
              {
                path: "/perfil/configuracoes",
                lazy: async () => {
                  const { ConfiguracoesPage } =
                    await import("@/pages/ConfiguracoesPage");
                  return { Component: ConfiguracoesPage };
                },
              },
              {
                path: "/perfil/novidades",
                lazy: async () => {
                  const { NovidadesPage } =
                    await import("@/pages/NovidadesPage");
                  return { Component: NovidadesPage };
                },
              },
              {
                path: "/perfil/avatar",
                lazy: async () => {
                  const { AvatarPage } = await import("@/pages/AvatarPage");
                  return { Component: AvatarPage };
                },
              },
              {
                path: "/jogo/loja",
                lazy: async () => {
                  const { LojaPage } = await import("@/pages/LojaPage");
                  return { Component: LojaPage };
                },
              },
              {
                path: "/notificacoes",
                lazy: async () => {
                  const { NotificacoesPage } =
                    await import("@/pages/NotificacoesPage");
                  return { Component: NotificacoesPage };
                },
              },
              {
                path: "/grupos/novo",
                lazy: async () => {
                  const { CriarGrupoPage } =
                    await import("@/pages/CriarGrupoPage");
                  return { Component: CriarGrupoPage };
                },
              },
              {
                path: "/grupos/:id",
                lazy: async () => {
                  const { GrupoPage } = await import("@/pages/GrupoPage");
                  return { Component: GrupoPage };
                },
              },
              {
                path: "/grupos/:id/inicio",
                lazy: async () => {
                  const { InicioPage } = await import("@/pages/InicioPage");
                  return { Component: InicioPage };
                },
              },
              {
                path: "/grupos/:id/atividades",
                lazy: async () => {
                  const { AtividadesPage } =
                    await import("@/pages/AtividadesPage");
                  return { Component: AtividadesPage };
                },
              },
              {
                path: "/grupos/:id/atividades/nova",
                lazy: async () => {
                  const { RegistrarAtividadePage } =
                    await import("@/pages/RegistrarAtividadePage");
                  return { Component: RegistrarAtividadePage };
                },
              },
              {
                path: "/grupos/:id/treino",
                lazy: async () => {
                  const { TreinoPage } = await import("@/pages/TreinoPage");
                  return { Component: TreinoPage };
                },
              },
              {
                path: "/grupos/:id/membros/:usuarioId",
                lazy: async () => {
                  const { MembroPage } = await import("@/pages/MembroPage");
                  return { Component: MembroPage };
                },
              },
              {
                path: "/grupos/:id/revisar/:atividadeId",
                lazy: async () => {
                  const { RevisarPage } = await import("@/pages/RevisarPage");
                  return { Component: RevisarPage };
                },
              },
              {
                path: "/grupos/:id/convidar",
                lazy: async () => {
                  const { ConvidarPage } = await import("@/pages/ConvidarPage");
                  return { Component: ConvidarPage };
                },
              },
              {
                path: "/grupos/:id/desafios/novo",
                lazy: async () => {
                  const { CriarDesafioPage } =
                    await import("@/pages/CriarDesafioPage");
                  return { Component: CriarDesafioPage };
                },
              },
              {
                path: "/desafios/:id",
                lazy: async () => {
                  const { DesafioPage } = await import("@/pages/DesafioPage");
                  return { Component: DesafioPage };
                },
              },
              {
                path: "/atividades/:id",
                lazy: async () => {
                  const { AtividadePage } =
                    await import("@/pages/AtividadePage");
                  return { Component: AtividadePage };
                },
              },
              {
                // ADR 0065: o link que o Movr abre ao postar um treino.
                path: "/importar/:id",
                lazy: async () => {
                  const { ImportarRascunhoPage } =
                    await import("@/pages/ImportarRascunhoPage");
                  return { Component: ImportarRascunhoPage };
                },
              },
            ],
          },
          {
            // Sem as abas: a tela de consentimento é uma só decisão.
            path: "/integracoes/movr/autorizar",
            lazy: async () => {
              const { IntegracaoAutorizarPage } =
                await import("@/pages/IntegracaoAutorizarPage");
              return { Component: IntegracaoAutorizarPage };
            },
          },
        ],
      },
      {
        // Fora dos dois guards: serve tanto a quem tem conta quanto a quem ainda vai criar.
        path: "/convite",
        lazy: async () => {
          const { ConvitePage } = await import("@/pages/ConvitePage");
          return { Component: ConvitePage };
        },
      },
      {
        // Fora dos guards: o link funciona com ou sem uma sessão aberta neste navegador.
        path: "/redefinir-senha",
        lazy: async () => {
          const { RedefinirSenhaPage } =
            await import("@/pages/RedefinirSenhaPage");
          return { Component: RedefinirSenhaPage };
        },
      },
      { path: "*", element: <Navigate to="/" replace /> },
    ],
  },
]);
