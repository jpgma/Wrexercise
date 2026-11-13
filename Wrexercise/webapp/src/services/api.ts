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
