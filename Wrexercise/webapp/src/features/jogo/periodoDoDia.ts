import { useEffect, useState } from "react";

/** A luz da praça segue a hora do aparelho de quem vê a cena (docs/gamificacao.md §3.4). */
export type PeriodoDoDia = "amanhecer" | "dia" | "entardecer" | "noite";

export function getPeriodoDoDia(hora: number): PeriodoDoDia {
  if (hora >= 5 && hora < 7) return "amanhecer";
  if (hora >= 7 && hora < 17) return "dia";
  if (hora >= 17 && hora < 19) return "entardecer";
  return "noite";
}

/** As luminárias acendem no entardecer e ficam acesas a noite toda. */
export function isLuzAcesa(periodo: PeriodoDoDia): boolean {
  return periodo === "entardecer" || periodo === "noite";
}

export function usePeriodoDoDia(): PeriodoDoDia {
  const [periodo, setPeriodo] = useState(() =>
    getPeriodoDoDia(new Date().getHours()),
  );
  useEffect(() => {
    const id = window.setInterval(
      () => setPeriodo(getPeriodoDoDia(new Date().getHours())),
      60_000,
    );
    return () => window.clearInterval(id);
  }, []);
  return periodo;
}
