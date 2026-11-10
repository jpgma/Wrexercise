import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { avataresKey, type Enfeite } from "@/components/folhaAvatar";
import {
  deleteEmpty,
  getJson,
  postEmpty,
  putJsonEmpty,
  putFormData,
} from "@/services/api";

export const meuAvatarKey = ["jogo", "meu-avatar"] as const;

type FolhaEnviada = { folha: string };

/** Depois de qualquer troca, o mapa de folhas e o avatar de quem vê são buscados de novo. */
function useInvalidarAvatares() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: avataresKey }),
      queryClient.invalidateQueries({ queryKey: meuAvatarKey }),
    ]);
}

export function useEnviarFolha() {
  const invalidar = useInvalidarAvatares();
  return useMutation({
    mutationFn: (folha: Blob) => {
      const formulario = new FormData();
      formulario.append("arquivo", folha, "folha.png");
      return putFormData<FolhaEnviada>("/api/jogo/avatar/folha", formulario);
    },
    onSuccess: invalidar,
  });
}

export function useRestaurarFolha() {
  const invalidar = useInvalidarAvatares();
  return useMutation({
    mutationFn: () => deleteEmpty("/api/jogo/avatar/folha"),
    onSuccess: invalidar,
  });
}

/** O `Proprietario` ou um `Administrador` volta ao padrão o avatar de um membro do grupo. */
export function useRedefinirAvatarMembro(grupoId: string) {
  const invalidar = useInvalidarAvatares();
  return useMutation({
    mutationFn: (usuarioId: string) =>
      deleteEmpty(`/api/grupos/${grupoId}/membros/${usuarioId}/avatar`),
    onSuccess: invalidar,
  });
}

export type MeuAvatar = {
  folha: string | null;
  vitrine: Enfeite[];
  liberados: Enfeite[];
  /** Códigos dos Enfeites que a pessoa ainda não viu. */
  novos: string[];
};

export function useMeuAvatar() {
  return useQuery({
    queryKey: meuAvatarKey,
    queryFn: ({ signal }) => getJson<MeuAvatar>("/api/jogo/avatar", signal),
  });
}

function useInvalidarVitrine() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: avataresKey }),
      queryClient.invalidateQueries({ queryKey: meuAvatarKey }),
    ]);
}

export function useSalvarVitrine() {
  const invalidar = useInvalidarVitrine();
  return useMutation({
    mutationFn: (enfeites: string[]) =>
      putJsonEmpty("/api/jogo/avatar/vitrine", { enfeites }),
    onSuccess: invalidar,
  });
}

/** Depois da encenação de Enfeite novo. */
export function useMarcarEnfeitesVistos() {
  const invalidar = useInvalidarVitrine();
  return useMutation({
    mutationFn: () => postEmpty("/api/jogo/enfeites/vistos"),
    onSuccess: invalidar,
  });
}
