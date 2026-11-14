/**
 * Rascunhos no IndexedDB do aparelho (ADR 0058): o Android pode encerrar o PWA enquanto a pessoa está na
 * galeria, e o que estava só na memória se perde. Guarda arquivos (`Blob`) sem convertê-los, o que o
 * `localStorage` não faz. Nunca guarde credenciais aqui.
 *
 * Toda falha (navegação privada, cota cheia, IndexedDB ausente) vira "sem rascunho": a tela funciona sem ele.
 */

const nomeBanco = "wrexercise-rascunhos";
const nomeStore = "rascunhos";

let banco: Promise<IDBDatabase> | null = null;

function abrir(): Promise<IDBDatabase> {
  banco ??= new Promise<IDBDatabase>((resolve, reject) => {
    const pedido = indexedDB.open(nomeBanco, 1);
    pedido.onupgradeneeded = () => pedido.result.createObjectStore(nomeStore);
    pedido.onsuccess = () => resolve(pedido.result);
    pedido.onerror = () => reject(pedido.error ?? new Error("IndexedDB"));
  }).catch((error: unknown) => {
    banco = null;
    throw error;
  });
  return banco;
}

async function executar<T>(
  modo: IDBTransactionMode,
  operacao: (store: IDBObjectStore) => IDBRequest<T>,
): Promise<T> {
  const db = await abrir();
  return new Promise<T>((resolve, reject) => {
    const transacao = db.transaction(nomeStore, modo);
    const pedido = operacao(transacao.objectStore(nomeStore));
    transacao.oncomplete = () => resolve(pedido.result);
    transacao.onerror = () => reject(transacao.error ?? new Error("IndexedDB"));
    transacao.onabort = () => reject(transacao.error ?? new Error("IndexedDB"));
  });
}

export async function lerRascunho<T>(chave: string): Promise<T | null> {
  try {
    return ((await executar("readonly", (s) => s.get(chave))) as T) ?? null;
  } catch {
    return null;
  }
}

export async function salvarRascunho(
  chave: string,
  valor: unknown,
): Promise<void> {
  try {
    await executar("readwrite", (s) => s.put(valor, chave));
  } catch {
    // Sem rascunho: a tela continua funcionando só com a memória.
  }
}

export async function apagarRascunho(chave: string): Promise<void> {
  try {
    await executar("readwrite", (s) => s.delete(chave));
  } catch {
    // Nada a fazer.
  }
}

/** Ao sair da conta: nada da conta anterior fica no aparelho. */
export async function apagarTodosRascunhos(): Promise<void> {
  try {
    await executar("readwrite", (s) => s.clear());
  } catch {
    // Nada a fazer.
  }
}
