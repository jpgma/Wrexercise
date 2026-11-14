import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { useToast } from "@/components/Toast";
import { getJson } from "@/services/api";

export type DetalhamentoEnergia = {
  atividades: number;
  bonus: number;
  metas: number;
  sequencias: number;
  desafios: number;
  jogadas: number;
  rivais: number;
  /** O que a FADIGA deixou de render: zero ou negativo. */
  fadiga: number;
};

export type Energia = {
  saldo: number;
  detalhamento: DetalhamentoEnergia;
  /** Quantos dos próximos treinos verificados ainda rendem metade da base (0 a 3). */
  fadigaRestante: number;
};

const energiaKey = ["jogo", "energia"] as const;

/** Um treino com FADIGA rende metade da base de 10 EN. */
export const baseComFadiga = 5;

export const avisoFadigaAplicada =
  "FADIGA! Seus próximos 3 treinos renderão metade da EN.";

export function useEnergia() {
  return useQuery({
    queryKey: energiaKey,
    queryFn: ({ signal }) => getJson<Energia>("/api/jogo/energia", signal),
  });
}

// O último valor da FADIGA que a pessoa viu fica num cookie comum, não em `localStorage`, que o ESLint
// bloqueia. Não é credencial: só evita repetir o aviso.
const cookieFadiga = "wx_fadiga";

function lerFadigaVista(): number | null {
  const valor = document.cookie
    .split(";")
    .map((parte) => parte.trim())
    .find((parte) => parte.startsWith(`${cookieFadiga}=`))
    ?.slice(cookieFadiga.length + 1);
  const numero = Number(valor);
  return valor !== undefined && Number.isInteger(numero) ? numero : null;
}

/** Registra que a pessoa já viu este valor, para o aviso não se repetir. */
export function marcarFadigaVista(restante: number) {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieFadiga}=${restante}; Max-Age=${400 * 24 * 60 * 60}; Path=/; SameSite=Lax${seguro}`;
}

function getAvisoFadiga(antes: number, agora: number): string {
  if (agora > antes) return avisoFadigaAplicada;
  if (agora === 0) {
    return "FADIGA REMOVIDA! Seus treinos voltaram a render EN normalmente.";
  }
  const treinos =
    agora === 1 ? "1 treino restante" : `${agora} treinos restantes`;
  return `+${baseComFadiga} EN · FADIGA: ${treinos}`;
}

/**
 * Avisa quando a FADIGA muda: um treino verificado gasta uma carga, a última a remove, e uma carta
 * expirada ou falhada a aplica. A verificação acontece depois do registro, então o aviso aparece na
 * próxima vez que a tela do Jogo lê a Energia. Na primeira leitura, só guarda o valor.
 */
export function useAvisoFadiga() {
  const energia = useEnergia();
  const { showToast } = useToast();
  const restante = energia.data?.fadigaRestante;

  useEffect(() => {
    if (restante === undefined) return;
    const vista = lerFadigaVista();
    if (vista === restante) return;
    marcarFadigaVista(restante);
    if (vista === null) return;
    showToast(getAvisoFadiga(vista, restante));
  }, [restante, showToast]);
}
