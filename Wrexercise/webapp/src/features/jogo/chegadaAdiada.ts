import { useSyncExternalStore } from "react";

/**
 * "Responder depois do treino" (decisão 0086): as cartas que a pessoa deixou para depois. Enquanto todas
 * as cartas pendentes estão aqui, o portão da Chegada não toma a tela. Uma carta nova chega e o portão
 * volta. O prazo da carta não muda, e o registro de treino continua bloqueado até a resposta.
 *
 * Fica num cookie comum, como as outras preferências: não é credencial, e o ESLint bloqueia
 * `localStorage`. Dura no máximo 6 horas; depois, a Chegada volta sozinha.
 */
const cookieAdiada = "wx_chegada_adiada";
const duracaoSegundos = 6 * 60 * 60;
const ouvintes = new Set<() => void>();

function ler(): string {
  const parte = document.cookie
    .split(";")
    .map((p) => p.trim())
    .find((p) => p.startsWith(`${cookieAdiada}=`));
  return parte ? decodeURIComponent(parte.slice(cookieAdiada.length + 1)) : "";
}

function gravar(valor: string, maxAge: number) {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieAdiada}=${encodeURIComponent(valor)}; Max-Age=${maxAge}; Path=/; SameSite=Lax${seguro}`;
  ouvintes.forEach((ouvinte) => ouvinte());
}

function assinar(ouvinte: () => void) {
  ouvintes.add(ouvinte);
  return () => {
    ouvintes.delete(ouvinte);
  };
}

/** Deixa estas cartas para depois do treino. */
export function adiarChegada(ids: string[]) {
  gravar(ids.join("."), duracaoSegundos);
}

/** O treino acabou, ou a pessoa foi responder: a Chegada volta a valer. */
export function limparChegadaAdiada() {
  if (ler()) gravar("", 0);
}

/** Se todas as cartas pendentes foram deixadas para depois do treino. */
export function useChegadaAdiada(pendentes: string[] | undefined): boolean {
  const valor = useSyncExternalStore(assinar, ler, () => "");
  if (!pendentes || pendentes.length === 0 || !valor) return false;
  const adiadas = new Set(valor.split("."));
  return pendentes.every((id) => adiadas.has(id));
}
