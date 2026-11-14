import type { MetadadosFoto } from "./lerMetadadosFoto";

/**
 * Espelho de `AvisosEvidencia.CalcularAvisoData` (backend) para avisar o autor antes do envio (ADR 0052).
 * O servidor decide; esta cópia só antecipa o aviso.
 */
const margemAntesMs = 2 * 60 * 60_000;
const margemDepoisMs = 12 * 60 * 60_000;

/** A atividade pode ter começado até 48 horas antes do registro. */
export const maximoNoPassadoMs = 48 * 60 * 60_000;

export type AvisoDataFoto = "FotoForaDoPeriodo" | "FotoSemData" | null;

export function calcularAvisoDataFoto(
  inicio: Date,
  duracaoSegundos: number,
  metadados: Pick<MetadadosFoto, "capturadaEm" | "origem">,
): AvisoDataFoto {
  const { capturadaEm, origem } = metadados;
  if (!capturadaEm || !origem) return "FotoSemData";

  const instante = capturadaEm.getTime();
  const janelaInicio = inicio.getTime() - margemAntesMs;
  const janelaFim = inicio.getTime() + duracaoSegundos * 1000 + margemDepoisMs;
  if (instante < janelaInicio) return "FotoForaDoPeriodo";
  if (origem === "Exif" || origem === "Camera")
    return instante > janelaFim ? "FotoForaDoPeriodo" : null;
  return "FotoSemData";
}
