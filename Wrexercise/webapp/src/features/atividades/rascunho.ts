import type { TipoAtividade } from "./types";

/** O formulário de registro enquanto a pessoa avalia a fila do grupo. */
export type RascunhoAtividade = {
  tipo?: TipoAtividade;
  duracaoMinutos: number;
  startedAt: string;
  descricao: string;
  arquivos: File[];
  /** A pessoa tocou em "Avaliar e enviar": ao voltar com a fila vazia, o envio é automático. */
  enviarAoVoltar: boolean;
};

// Só em memória: sobrevive à navegação dentro do aplicativo, não a um recarregamento.
const rascunhos = new Map<string, RascunhoAtividade>();

export function guardarRascunho(grupoId: string, rascunho: RascunhoAtividade) {
  rascunhos.set(grupoId, rascunho);
}

export function lerRascunho(grupoId: string): RascunhoAtividade | undefined {
  return rascunhos.get(grupoId);
}

export function descartarRascunho(grupoId: string) {
  rascunhos.delete(grupoId);
}
