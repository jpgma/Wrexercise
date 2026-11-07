import { useQuery } from "@tanstack/react-query";
import { getJson } from "@/services/api";

export type DetalhamentoEnergia = {
  atividades: number;
  bonus: number;
  metas: number;
  sequencias: number;
  desafios: number;
};

export type Energia = {
  saldo: number;
  detalhamento: DetalhamentoEnergia;
};

export function useEnergia() {
  return useQuery({
    queryKey: ["jogo", "energia"],
    queryFn: ({ signal }) => getJson<Energia>("/api/jogo/energia", signal),
  });
}
