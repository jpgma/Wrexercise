// Service Worker do Wrexercise. Ver docs/decisoes/0021-pwa-e-service-worker.md.
// O build (plugin em vite.config.ts) preenche os dois marcadores abaixo.
// Só guarda arquivos públicos do build: nunca respostas da API nem evidências.

const VERSAO = "__VERSAO__";
const PRECACHE = [/* __PRECACHE__ */];
const CACHE = `wrexercise-${VERSAO}`;

// Tudo o que depende da sessão vai sempre direto à rede.
const SEMPRE_REDE = ["/api/", "/health", "/ready"];

// O `?v=` faz cada versão buscar o arquivo na origem: sem ele, uma CDN (Cloudflare) ainda com a cópia
// antiga de um arquivo de nome fixo (por exemplo, `avatar-jpgma.png`) a entregaria, e ela ficaria
// no pré-cache até a versão seguinte. O cache guarda a resposta com a URL sem o `?v=`.
async function preencher() {
  const cache = await caches.open(CACHE);
  await Promise.all(
    PRECACHE.map(async (url) => {
      const resposta = await fetch(`${url}?v=${VERSAO}`, { cache: "reload" });
      if (!resposta.ok) throw new Error(`Pré-cache falhou: ${url}`);
      await cache.put(url, resposta);
    }),
  );
}

self.addEventListener("install", (event) => {
  event.waitUntil(preencher());
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((nomes) =>
        Promise.all(
          nomes
            // "easyfit-" é o prefixo de antes da troca de nome (decisão 0026).
            .filter(
              (nome) =>
                (nome.startsWith("wrexercise-") ||
                  nome.startsWith("easyfit-")) &&
                nome !== CACHE,
            )
            .map((nome) => caches.delete(nome)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

// A nova versão só assume quando a pessoa pede, para não recarregar um formulário pela metade.
self.addEventListener("message", (event) => {
  if (event.data === "SKIP_WAITING") void self.skipWaiting();
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (SEMPRE_REDE.some((prefixo) => url.pathname.startsWith(prefixo))) return;

  if (request.mode === "navigate") {
    event.respondWith(navegar(request));
    return;
  }

  if (url.pathname.startsWith("/assets/") || PRECACHE.includes(url.pathname)) {
    event.respondWith(
      doCacheOuRede(request, url.pathname.startsWith("/assets/")),
    );
  }
});

// Rede primeiro: online, sempre o index.html mais novo. Offline, o da versão instalada.
async function navegar(request) {
  try {
    return await fetch(request);
  } catch (erro) {
    const shell = await caches.match("/index.html", { cacheName: CACHE });
    if (shell) return shell;
    throw erro;
  }
}

// Arquivos de /assets têm hash no nome: o mesmo nome nunca muda de conteúdo.
async function doCacheOuRede(request, guardar) {
  const cache = await caches.open(CACHE);
  const salvo = await cache.match(request);
  if (salvo) return salvo;

  const resposta = await fetch(request);
  if (guardar && resposta.ok) void cache.put(request, resposta.clone());
  return resposta;
}
