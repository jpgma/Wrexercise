import { useQuery } from "@tanstack/react-query";
import type { TipoAtividade, TipoRecorde } from "@/features/atividades";
import { shouldRetry } from "@/features/grupos";
import { getJson, postFormData, postJson } from "@/services/api";
import type { PontoPercurso } from "./calculo";

/** Um trecho do percurso: pontos `[latitude, longitude]`. Entre dois trechos houve uma lacuna. */
export type Trecho = [number, number][];

export type Bbox = {
  latMin: number;
  lonMin: number;
  latMax: number;
  lonMax: number;
};

export type Parcial = { metros: number; segundos: number };

/** O Percurso de uma atividade (ADR 0084). Velocidades em m/s. */
export type Percurso = {
  atividadeId: string;
  tipo: TipoAtividade;
  aoVivo: boolean;
  /** É o percurso de quem vê: ele vê tudo; o grupo, sem os primeiros e os últimos 200 m. */
  voce: boolean;
  resumo: {
    distanciaMetros: number;
    duracaoSegundos: number;
    velocidadeMedia: number | null;
    velocidadeMaxima: number | null;
    /** Aproximado; falta quando o GPS não mandou altitude. */
    ganhoElevacaoMetros: number | null;
    parciais: Parcial[];
    recorde: TipoRecorde | null;
  };
  segmentos: Trecho[];
  bbox: Bbox | null;
  /** Quem treinou junto ("Treinaram juntos"), só quando os percursos andaram lado a lado. */
  parceiro: {
    usuarioId: string;
    nomeExibicao: string;
    segmentos: Trecho[];
  } | null;
  torcidas: number;
};

export const percursoKey = (atividadeId: string) =>
  ["atividades", atividadeId, "percurso"] as const;

/** O percurso; ao vivo, refeito a cada 30 s, no mesmo ritmo dos lotes do aparelho. */
export function usePercurso(
  atividadeId: string,
  aoVivo: boolean,
  enabled = true,
) {
  return useQuery({
    queryKey: percursoKey(atividadeId),
    queryFn: ({ signal }) =>
      getJson<Percurso>(`/api/atividades/${atividadeId}/percurso`, signal),
    enabled,
    refetchInterval: aoVivo ? 30_000 : false,
    refetchIntervalInBackground: false,
    retry: shouldRetry,
  });
}

export type RespostaPontos = {
  ultimoSeq: number;
  distanciaMetros: number;
  totalPontos: number;
};

export const enviarPontos = (atividadeId: string, pontos: PontoPercurso[]) =>
  postJson<RespostaPontos>(`/api/atividades/${atividadeId}/percurso/pontos`, {
    pontos,
  });

/** A imagem do percurso vira evidência com o selo "Percurso"; nunca muda a comprovação. */
export function enviarImagemPercurso(
  atividadeId: string,
  imagem: Blob,
  miniatura: Blob | null,
) {
  const form = new FormData();
  form.append("arquivo", imagem, "percurso.png");
  if (miniatura) form.append("miniatura", miniatura, "miniatura.jpg");
  return postFormData<{ id: string }>(
    `/api/atividades/${atividadeId}/percurso/imagem`,
    form,
  );
}
