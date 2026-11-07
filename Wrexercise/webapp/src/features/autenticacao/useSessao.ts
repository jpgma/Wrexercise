import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ApiError,
  getJson,
  postEmpty,
  postJson,
  postJsonEmpty,
  putJson,
} from "@/services/api";

export type ModoInicio = "App" | "Jogo";

export type Usuario = {
  id: string;
  nomeExibicao: string;
  email: string;
  modoInicio: ModoInicio;
};

export type EntrarData = {
  email: string;
  senha: string;
};

export type RegistrarData = EntrarData & {
  nomeExibicao: string;
  conviteToken?: string;
};

type SessaoResponse = {
  usuario: Usuario;
};

const basePath = "/api/autenticacao";
const sessaoKey = ["autenticacao", "sessao"] as const;

/** O usuário logado, ou `null` quando não há sessão. */
export function useSessao() {
  return useQuery({
    queryKey: sessaoKey,
    queryFn: async ({ signal }): Promise<Usuario | null> => {
      try {
        const { usuario } = await getJson<SessaoResponse>(
          `${basePath}/sessao`,
          signal,
        );
        return usuario;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
    staleTime: 5 * 60 * 1000,
    retry: 1,
  });
}

type ConfiguracaoInicialResponse = {
  pendente: boolean;
};

const configuracaoInicialKey = [
  "autenticacao",
  "configuracao-inicial",
] as const;

/**
 * Diz se o app ainda não tem nenhum usuário. Só vale a pena consultar sem sessão.
 * Depois que a configuração termina ela nunca volta a ficar pendente, então o valor não expira.
 */
export function useConfiguracaoInicial(enabled = true) {
  return useQuery({
    queryKey: configuracaoInicialKey,
    queryFn: ({ signal }) =>
      getJson<ConfiguracaoInicialResponse>(
        `${basePath}/configuracao-inicial`,
        signal,
      ),
    enabled,
    select: (data) => data.pendente,
    staleTime: (query) => (query.state.data?.pendente === false ? Infinity : 0),
    retry: 1,
  });
}

export function useConfigurarPrimeiroUsuario() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Omit<RegistrarData, "conviteToken">) =>
      postJson<SessaoResponse>(`${basePath}/configuracao-inicial`, data),
    onSuccess: ({ usuario }) => {
      queryClient.setQueryData<ConfiguracaoInicialResponse>(
        configuracaoInicialKey,
        { pendente: false },
      );
      queryClient.setQueryData(sessaoKey, usuario);
    },
  });
}

export function useEntrar() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: EntrarData) =>
      postJson<SessaoResponse>(`${basePath}/entrar`, data),
    onSuccess: ({ usuario }) => queryClient.setQueryData(sessaoKey, usuario),
  });
}

export function useRegistrar() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: RegistrarData) =>
      postJson<SessaoResponse>(`${basePath}/registrar`, data),
    onSuccess: ({ usuario }) => queryClient.setQueryData(sessaoKey, usuario),
  });
}

export function useSair() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => postEmpty(`${basePath}/sair`),
    onSuccess: () => {
      queryClient.setQueryData(sessaoKey, null);
      // Nada da conta anterior pode continuar disponível para a próxima.
      queryClient.removeQueries({
        predicate: (query) => query.queryKey[0] !== sessaoKey[0],
      });
    },
  });
}

export function useAlterarNome() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (nomeExibicao: string) =>
      putJson<SessaoResponse>("/api/usuarios/eu", { nomeExibicao }),
    onSuccess: ({ usuario }) => {
      queryClient.setQueryData(sessaoKey, usuario);
      // O nome aparece nas listas e na classificação de todos os grupos.
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] !== sessaoKey[0],
      });
    },
  });
}

export function useAlterarModoInicio() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (modoInicio: ModoInicio) =>
      putJson<SessaoResponse>("/api/usuarios/eu/modo-inicio", { modoInicio }),
    // O modo só muda a tela inicial: não há nada a recarregar além da sessão.
    onSuccess: ({ usuario }) => queryClient.setQueryData(sessaoKey, usuario),
  });
}

export function useAlterarSenha() {
  return useMutation({
    mutationFn: (data: { senhaAtual: string; novaSenha: string }) =>
      postJsonEmpty("/api/usuarios/eu/senha", data),
  });
}

export function useRedefinirSenha() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { token: string; novaSenha: string }) =>
      postJsonEmpty(`${basePath}/redefinir-senha`, data),
    // Todas as sessões da conta foram encerradas, inclusive uma aberta neste navegador.
    onSuccess: () => {
      queryClient.setQueryData(sessaoKey, null);
      queryClient.removeQueries({
        predicate: (query) => query.queryKey[0] !== sessaoKey[0],
      });
    },
  });
}
