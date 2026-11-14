export class ApiError extends Error {
  readonly status: number;
  readonly code: string | undefined;
  /** Só no erro interno: o código que a pessoa repassa e que está no log do servidor. */
  readonly referencia: string | undefined;

  constructor(
    status: number,
    code: string | undefined,
    title: string | undefined,
    referencia?: string,
  ) {
    super(title ?? "Não foi possível concluir a operação.");
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.referencia = referencia;
  }
}

async function request(path: string, init: RequestInit): Promise<Response> {
  const response = await fetch(path, {
    ...init,
    headers: { Accept: "application/json", ...init.headers },
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      code?: string;
      title?: string;
      referencia?: string;
    } | null;
    throw new ApiError(
      response.status,
      problem?.code,
      problem?.title,
      problem?.referencia,
    );
  }

  return response;
}

export async function getJson<T>(
  path: string,
  signal?: AbortSignal,
): Promise<T> {
  const response = await request(path, { signal });
  return (await response.json()) as T;
}

function jsonRequest(
  method: string,
  body: unknown,
  headers?: Record<string, string>,
): RequestInit {
  return {
    method,
    headers: { "Content-Type": "application/json", ...headers },
    body: JSON.stringify(body),
  };
}

export async function postJson<T>(
  path: string,
  body?: unknown,
  headers?: Record<string, string>,
): Promise<T> {
  const response = await request(path, jsonRequest("POST", body, headers));
  return (await response.json()) as T;
}

export async function postFormData<T>(
  path: string,
  body: FormData,
): Promise<T> {
  const response = await request(path, { method: "POST", body });
  return (await response.json()) as T;
}

/**
 * Como `postFormData`, mas informa o progresso do envio: o `fetch` não tem esse evento, o XMLHttpRequest tem.
 * Um vídeo pode ter 25 MB numa conexão móvel (ADR 0058). Mesma origem: o cookie da sessão vai sozinho.
 */
export function postFormDataComProgresso<T>(
  path: string,
  body: FormData,
  onProgresso: (fracao: number) => void,
): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", path);
    xhr.setRequestHeader("Accept", "application/json");
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgresso(event.loaded / event.total);
    };
    xhr.onload = () => {
      let corpo: unknown = null;
      try {
        corpo = JSON.parse(xhr.responseText) as unknown;
      } catch {
        // Resposta sem JSON: o status decide.
      }
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(corpo as T);
        return;
      }
      const problem = corpo as {
        code?: string;
        title?: string;
        referencia?: string;
      } | null;
      reject(
        new ApiError(
          xhr.status,
          problem?.code,
          problem?.title,
          problem?.referencia,
        ),
      );
    };
    // Sem resposta: `getErrorMessage` mostra a mensagem de conexão.
    xhr.onerror = () => reject(new TypeError("Falha de rede."));
    xhr.send(body);
  });
}

export async function putFormData<T>(path: string, body: FormData): Promise<T> {
  const response = await request(path, { method: "PUT", body });
  return (await response.json()) as T;
}

export async function postEmpty(path: string): Promise<void> {
  await request(path, { method: "POST" });
}

export async function postJsonEmpty(
  path: string,
  body: unknown,
): Promise<void> {
  await request(path, jsonRequest("POST", body));
}

export async function putJsonEmpty(path: string, body: unknown): Promise<void> {
  await request(path, jsonRequest("PUT", body));
}

export async function putJson<T>(path: string, body: unknown): Promise<T> {
  const response = await request(path, jsonRequest("PUT", body));
  return (await response.json()) as T;
}

export async function deleteEmpty(path: string): Promise<void> {
  await request(path, { method: "DELETE" });
}

/** Mensagem para o usuário: a da API, ou uma genérica quando nem houve resposta. */
export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.referencia
      ? `${error.message} Código: ${error.referencia}`
      : error.message;
  }
  return "Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.";
}
