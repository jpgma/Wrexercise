import {
  apagarRascunho,
  lerRascunho,
  salvarRascunho,
} from "@/services/rascunhos";
import type { EvidenciaPreparada } from "./comprimirImagem";
import { maximoNoPassadoMs } from "./janelaEvidencia";

/**
 * O registro em andamento, guardado no aparelho (ADR 0058). Os comprovantes vão já preparados (comprimidos e
 * sem EXIF), e a chave de idempotência junto: se o envio caiu no meio, repetir devolve a mesma atividade e o
 * servidor reconhece as fotos que já chegaram (ADR 0053).
 */
export type RascunhoRegistro<Valores> = {
  salvoEm: number;
  valores: Valores;
  evidencias: EvidenciaPreparada[];
  envio: { assinatura: string; chave: string } | null;
};

function chave(usuarioId: string, grupoId: string) {
  return `registro:${usuarioId}:${grupoId}`;
}

export async function lerRascunhoRegistro<Valores>(
  usuarioId: string,
  grupoId: string,
): Promise<RascunhoRegistro<Valores> | null> {
  const rascunho = await lerRascunho<RascunhoRegistro<Valores>>(
    chave(usuarioId, grupoId),
  );
  // Depois de 48 horas o servidor já não aceita a atividade: o rascunho não serve mais.
  if (rascunho && Date.now() - rascunho.salvoEm > maximoNoPassadoMs) {
    await apagarRascunho(chave(usuarioId, grupoId));
    return null;
  }
  return rascunho;
}

export function salvarRascunhoRegistro<Valores>(
  usuarioId: string,
  grupoId: string,
  rascunho: Omit<RascunhoRegistro<Valores>, "salvoEm">,
) {
  return salvarRascunho(chave(usuarioId, grupoId), {
    ...rascunho,
    salvoEm: Date.now(),
  });
}

export function apagarRascunhoRegistro(usuarioId: string, grupoId: string) {
  return apagarRascunho(chave(usuarioId, grupoId));
}
