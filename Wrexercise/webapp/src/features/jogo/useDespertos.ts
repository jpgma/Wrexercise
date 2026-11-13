import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { MembroCena } from "./useCena";

// Quem quem vê já viu dormindo, por grupo, fica num cookie comum, não em `localStorage`, que o
// ESLint proíbe. Só o começo de cada identificador: basta para reconhecer alguém do grupo.
const prefixoCookie = "wx_dormindo_";
const TamanhoId = 8;

const getCurto = (id: string) => id.replace(/-/g, "").slice(0, TamanhoId);

function getNomeCookie(grupoId: string) {
  return `${prefixoCookie}${getCurto(grupoId)}`;
}

function lerVistos(grupoId: string): Set<string> {
  const nome = getNomeCookie(grupoId);
  const valor = document.cookie
    .split(";")
    .map((parte) => parte.trim())
    .find((parte) => parte.startsWith(`${nome}=`))
    ?.slice(nome.length + 1);
  return new Set(valor ? valor.split(".") : []);
}

function gravarVistos(grupoId: string, dormindo: string[]) {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  const valor = dormindo.map(getCurto).join(".");
  document.cookie = `${getNomeCookie(grupoId)}=${valor}; Max-Age=${60 * 24 * 60 * 60}; Path=/; SameSite=Lax${seguro}`;
}

/**
 * Quem acordou desde a última visita: quem vê o viu dormindo e agora a pose é outra. A cena faz
 * essas pessoas `levantar`, uma vez (docs/gamificacao.md §3.3). Depois de tocar, ou de pular, a
 * lista de quem dorme agora vira a vista.
 */
export function useDespertos(grupoId: string, membros: MembroCena[]) {
  const dormindo = useMemo(
    () => membros.filter((m) => m.pose === "dormindo").map((m) => m.usuarioId),
    [membros],
  );
  // Calculado uma vez, ao abrir a cena: a encenação toca na visita, não a cada nova busca.
  const [despertos, setDespertos] = useState<string[]>(() => {
    const vistos = lerVistos(grupoId);
    return membros
      .filter((m) => m.pose !== "dormindo" && vistos.has(getCurto(m.usuarioId)))
      .map((m) => m.usuarioId);
  });
  const dormindoRef = useRef(dormindo);

  // Enquanto há quem acordar na encenação, o cookie espera: sair antes dela a faz tocar de novo.
  useEffect(() => {
    dormindoRef.current = dormindo;
    if (despertos.length === 0) gravarVistos(grupoId, dormindo);
  }, [grupoId, dormindo, despertos.length]);

  const marcarVistos = useCallback(() => {
    gravarVistos(grupoId, dormindoRef.current);
    setDespertos([]);
  }, [grupoId]);

  return { despertos, marcarVistos };
}
