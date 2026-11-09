import { createHash } from "node:crypto";
import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, type Plugin } from "vite";

const backend = "http://localhost:8080";

/**
 * Gera o sw.js com a lista de pré-cache e a versão do build. Ver sw.js.
 * O pré-cache cobre só o necessário para abrir o app offline; as páginas entram no cache ao serem visitadas.
 */
function serviceWorker(): Plugin {
  const publicDir = fileURLToPath(new URL("./public", import.meta.url));
  const origem = fileURLToPath(new URL("./sw.js", import.meta.url));

  return {
    name: "wrexercise-service-worker",
    apply: "build",
    generateBundle(_, bundle) {
      const arquivos = new Set<string>(["index.html"]);
      const versao = createHash("sha256");

      for (const item of Object.values(bundle)) {
        versao.update(item.fileName);
        if (item.type === "chunk" && item.isEntry) {
          arquivos.add(item.fileName);
          item.imports.forEach((nome) => arquivos.add(nome));
          item.viteMetadata?.importedCss.forEach((nome) => arquivos.add(nome));
        } else if (item.fileName.endsWith(".woff2")) {
          arquivos.add(item.fileName);
        }
      }

      for (const nome of readdirSync(publicDir)) {
        arquivos.add(nome);
        versao.update(readFileSync(`${publicDir}/${nome}`));
      }

      const precache = [...arquivos].sort().map((nome) => `/${nome}`);
      const codigo = readFileSync(origem, "utf8");
      // Uma mudança só no sw.js também cria um cache novo.
      versao.update(codigo);
      const gerado = codigo
        .replace(
          '"__VERSAO__"',
          JSON.stringify(versao.digest("hex").slice(0, 12)),
        )
        .replace("[/* __PRECACHE__ */]", JSON.stringify(precache));
      if (gerado.includes("__VERSAO__") || gerado.includes("__PRECACHE__"))
        this.error("Marcadores do sw.js não encontrados.");

      this.emitFile({ type: "asset", fileName: "sw.js", source: gerado });
    },
  };
}

export default defineConfig({
  plugins: [react(), tailwindcss(), serviceWorker()],
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  server: {
    proxy: {
      // Mantém o Host do navegador, como o Caddy em produção. A API compara
      // Origin com Host para recusar escritas de outra origem (CSRF).
      "/api": { target: backend, changeOrigin: false },
      "/health": backend,
      "/ready": backend,
    },
  },
  build: {
    // O backend serve a SPA a partir do próprio wwwroot.
    outDir: "../wwwroot",
    emptyOutDir: true,
  },
});
