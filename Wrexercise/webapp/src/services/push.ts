// Notificações push neste aparelho. Ver docs/decisoes/0049-notificacoes-push.md.
// A inscrição é do aparelho, não da conta: o servidor guarda uma por aparelho e a passa para quem entrar.

import { deleteEmpty, getJson, putJsonEmpty } from "@/services/api";

/**
 * - `indisponivel`: o navegador não tem Web Push, o servidor está sem chaves ou é o build de desenvolvimento.
 * - `instalar`: iPhone ou iPad fora do app instalado; o iOS só entrega push ao app da Tela de Início.
 * - `negado`: a pessoa bloqueou as notificações nas configurações do navegador.
 */
export type EstadoPush =
  "carregando" | "indisponivel" | "instalar" | "negado" | "inativo" | "ativo";

let estado: EstadoPush = "carregando";
let chavePublica: string | null = null;
let iniciado = false;
let dispensado = false;
const ouvintes = new Set<() => void>();

function definir(novo: EstadoPush) {
  estado = novo;
  ouvintes.forEach((ouvinte) => ouvinte());
}

export function subscribePush(ouvinte: () => void): () => void {
  ouvintes.add(ouvinte);
  return () => ouvintes.delete(ouvinte);
}

export function getEstadoPush(): EstadoPush {
  return estado;
}

function isSuportado(): boolean {
  return (
    import.meta.env.PROD &&
    "serviceWorker" in navigator &&
    "PushManager" in window &&
    "Notification" in window
  );
}

function isStandalone(): boolean {
  return (
    window.matchMedia("(display-mode: standalone)").matches ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true
  );
}

function isIos(): boolean {
  return (
    /iPhone|iPad|iPod/.test(navigator.userAgent) ||
    (navigator.userAgent.includes("Macintosh") && navigator.maxTouchPoints > 1)
  );
}

function paraBase64Url(buffer: ArrayBuffer | null): string {
  if (!buffer) return "";
  return btoa(String.fromCharCode(...new Uint8Array(buffer)))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

function deBase64Url(valor: string): Uint8Array<ArrayBuffer> {
  const base64 = valor.replace(/-/g, "+").replace(/_/g, "/");
  const binario = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "="));
  return Uint8Array.from(binario, (c) => c.charCodeAt(0));
}

async function salvar(inscricao: PushSubscription) {
  await putJsonEmpty("/api/notificacoes/push", {
    endpoint: inscricao.endpoint,
    p256dh: paraBase64Url(inscricao.getKey("p256dh")),
    auth: paraBase64Url(inscricao.getKey("auth")),
  });
}

async function inscrever(registro: ServiceWorkerRegistration, chave: string) {
  const inscricao = await registro.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: deBase64Url(chave),
  });
  await salvar(inscricao);
}

/**
 * Descobre o estado ao entrar na conta e reenvia a inscrição deste aparelho, que pode ter sido de
 * outra conta ou de outro par de chaves do servidor. Roda uma vez por carregamento da página.
 */
export async function iniciarPush(): Promise<void> {
  if (iniciado) return;
  iniciado = true;
  dispensado = document.cookie
    .split(";")
    .some((parte) => parte.trim().startsWith(`${cookieDispensa}=`));

  if (!isSuportado()) {
    definir(isIos() && !isStandalone() ? "instalar" : "indisponivel");
    return;
  }

  try {
    ({ chavePublica } = await getJson<{ chavePublica: string | null }>(
      "/api/notificacoes/push",
    ));
    if (!chavePublica) return definir("indisponivel");
    if (Notification.permission === "denied") return definir("negado");

    const registro = await navigator.serviceWorker.ready;
    const atual = await registro.pushManager.getSubscription();
    if (!atual || Notification.permission !== "granted")
      return definir("inativo");

    if (paraBase64Url(atual.options.applicationServerKey) !== chavePublica) {
      await atual.unsubscribe();
      await inscrever(registro, chavePublica);
    } else {
      await salvar(atual);
    }
    definir("ativo");
  } catch {
    // Sem rede ou com a inscrição recusada, a lista do app continua funcionando.
    iniciado = false;
    definir("indisponivel");
  }
}

/** Pede a permissão (precisa vir de um toque) e inscreve este aparelho. */
export async function ativarPush(): Promise<void> {
  if (!chavePublica) return;
  const permissao = await Notification.requestPermission();
  if (permissao !== "granted") {
    definir(permissao === "denied" ? "negado" : "inativo");
    return;
  }

  const registro = await navigator.serviceWorker.ready;
  await inscrever(registro, chavePublica);
  definir("ativo");
}

/** Para de receber neste aparelho. Também roda ao sair da conta, antes de a sessão acabar. */
export async function desativarPush(): Promise<void> {
  if (!isSuportado() || estado !== "ativo") return;
  const registro = await navigator.serviceWorker.ready;
  const atual = await registro.pushManager.getSubscription();
  if (atual) {
    await deleteEmpty(
      `/api/notificacoes/push?endpoint=${encodeURIComponent(atual.endpoint)}`,
    ).catch(() => undefined);
    await atual.unsubscribe();
  }
  definir("inativo");
}

/** Ao sair da conta, o próximo a entrar descobre o estado de novo. */
export function reiniciarPush() {
  iniciado = false;
  chavePublica = null;
  definir("carregando");
}

/** O número no ícone do app instalado. Onde não há suporte, não faz nada. */
export function atualizarBadge(total: number) {
  const nav = navigator as Navigator & {
    setAppBadge?: (total?: number) => Promise<void>;
    clearAppBadge?: () => Promise<void>;
  };
  const pedido = total > 0 ? nav.setAppBadge?.(total) : nav.clearAppBadge?.();
  void pedido?.catch(() => undefined);
}

// O convite para ativar aparece uma vez no app instalado. A dispensa fica num cookie comum, como a do
// aviso de instalação (ver services/instalacao.ts).
const cookieDispensa = "wx_push_dispensado";
const segundosDispensa = 365 * 24 * 60 * 60;

/** Para o aviso de todo o app: só no app instalado, com o push ainda desligado e sem dispensa. */
export function isConviteAtivarPush(): boolean {
  return estado === "inativo" && isStandalone() && !dispensado;
}

export function dispensarConvitePush() {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookieDispensa}=1; Max-Age=${segundosDispensa}; Path=/; SameSite=Lax${seguro}`;
  dispensado = true;
  definir(estado);
}
