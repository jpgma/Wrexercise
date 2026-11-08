import { useMutation, useQueryClient } from "@tanstack/react-query";
import { avataresKey } from "@/components/folhaAvatar";
import { deleteEmpty, putFormData } from "@/services/api";

type FolhaEnviada = { folha: string };

/** Depois de qualquer troca, o mapa de folhas é buscado de novo. */
function useInvalidarAvatares() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: avataresKey });
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
