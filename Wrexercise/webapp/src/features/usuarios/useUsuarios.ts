import { useQuery } from "@tanstack/react-query";
import { gruposKey, shouldRetry, type Papel } from "@/features/grupos";
import { getJson } from "@/services/api";

export type Conquista = {
  codigo: string;
  titulo: string;
  descricao: string;
  /** O progresso da pessoa, de 0 a `alvo`. */
  atual: number;
  alvo: number;
  liberada: boolean;
};

export type MeuPerfil = {
  membroDesde: string;
  totalTreinos: number;
  totalSegundos: number;
  melhorSequencia: number;
  semanas: {
    inicio: string;
    sessoesVerificadas: number;
    sessoesProvisorias: number;
  }[];
  conquistas: Conquista[];
};

export type PerfilMembro = {
  usuarioId: string;
  nomeExibicao: string;
  papel: Papel;
  joinedAt: string;
  totalTreinos: number;
  totalSegundos: number;
  desafio: {
    id: string;
    nome: string;
    fusoHorario: string;
    sessoesPorSemana: number;
    sessoesVerificadas: number;
    sessoesProvisorias: number;
    emAndamento: boolean;
    contaNestaSemana: boolean;
    semanasCumpridas: number;
    semanasDisputadas: number;
    sequenciaSemanas: number;
    semanas: {
      numero: number;
      inicio: string;
      sessoesVerificadas: number;
      sessoesProvisorias: number;
      participou: boolean;
    }[];
  } | null;
};

export function useMeuPerfil() {
  return useQuery({
    queryKey: ["usuarios", "eu", "perfil"],
    queryFn: ({ signal }) =>
      getJson<MeuPerfil>("/api/usuarios/eu/perfil", signal),
    retry: shouldRetry,
  });
}

export function usePerfilMembro(grupoId: string, usuarioId: string) {
  return useQuery({
    queryKey: [...gruposKey, grupoId, "membros", usuarioId, "perfil"],
    queryFn: ({ signal }) =>
      getJson<PerfilMembro>(
        `/api/grupos/${grupoId}/membros/${usuarioId}/perfil`,
        signal,
      ),
    retry: shouldRetry,
  });
}
