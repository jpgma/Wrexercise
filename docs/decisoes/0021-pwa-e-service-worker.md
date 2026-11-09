# 0021 — PWA e Service Worker

**Status:** DECIDIDA. Complementada pela seção Aviso de instalação abaixo.

## Contexto

A Fase 8 pede manifest, Service Worker, cache estático e instalação mobile (`arquitetura.md` §6.1). Q022 (funcionamento offline) e Q024 (atualização do PWA) estavam em aberto. Pelas regras de §6.3, o Service Worker não pode guardar respostas autenticadas nem evidências, e a primeira versão não terá fila de escritas offline.

## Alternativas consideradas

**Como gerar o Service Worker**

- **`vite-plugin-pwa` (Workbox):** é o caminho comum, mas traz uma dependência nova, configuração extensa e um código gerado mais difícil de auditar contra a regra de nunca guardar `/api`.
- **Escrito à mão:** cerca de 80 linhas, mais um plugin do Vite de poucas linhas que injeta a lista de pré-cache. Não traz dependência.

**Como atualizar**

- **Automática:** a versão nova assume sozinha, mas pode recarregar a página no meio de um formulário, como o de Registrar atividade.
- **Aviso com botão:** a pessoa decide quando recarregar.

## Decisão

- `Wrexercise/webapp/sw.js` é escrito à mão em JS puro. O plugin `serviceWorker()` em `vite.config.ts` gera `wwwroot/sw.js` com:
  - a versão: um hash dos nomes do bundle, do conteúdo de `public/` e do próprio `sw.js`;
  - o pré-cache: `index.html`, o chunk de entrada com seus imports estáticos, o CSS, as fontes e os arquivos de `public/`.
- `/api/*`, `/health` e `/ready` nunca passam pelo Service Worker, o que inclui evidências e miniaturas. Requisições que não são `GET` ou que vêm de outra origem também não.
- Navegação: tenta a rede primeiro e, sem rede, usa o `index.html` da versão instalada. Quem está online sempre recebe o HTML novo, de modo que um frontend antigo só roda offline, quando também não fala com a API. Isso resolve a incompatibilidade entre versões levantada em Q024.
- `/assets/*`: procura primeiro no cache, porque os nomes têm hash. As páginas carregadas sob demanda entram no cache conforme são visitadas.
- Atualização: a versão nova fica em espera, e o app mostra "Nova versão disponível" com o botão "Atualizar". A verificação acontece quando a página volta a ficar visível, sem polling. Ao confirmar, o Service Worker assume e a página recarrega.
- Offline: um aviso fixo "Você está sem conexão" aparece em todas as telas. Sem sessão em cache, a tela inteira de sem conexão do mockup substitui o carregamento infinito.
- Escritas usam `networkMode: "always"` no TanStack Query: sem rede, falham na hora em vez de ficarem pausadas, porque uma escrita pausada funcionaria como fila offline. As consultas continuam pausadas e voltam sozinhas com a conexão.
- Instalação: em Configurações, o botão "Instalar aplicativo" aparece quando o navegador oferece `beforeinstallprompt` (Chromium e Android). No iOS, aparece a instrução de usar Compartilhar.
- O backend responde com `no-cache` todo arquivo estático de nome fixo (`index.html`, `sw.js`, `manifest.webmanifest` e os de `public/`); só `/assets/*` é `immutable`. Assim o Cloudflare revalida na origem e não entrega uma cópia antiga depois de uma implantação.
- O pré-cache busca cada arquivo com `?v=<versão>` e o guarda com a URL sem o parâmetro. Mesmo se a CDN ainda tiver uma cópia antiga, a versão nova do Service Worker recebe a da origem. (2026-11-09: o `avatar-jpgma.png` novo não aparecia depois da implantação, porque o Cloudflare guardava o antigo por 4 h e o pré-cache o copiou.)
- O Service Worker só é registrado no build de produção. No `npm run dev`, não há cache.
- Ícones em `public/` (`favicon.ico`, `icon-192.png`, `icon-512.png`, `icon-maskable-512.png`, `apple-touch-icon.png`), gerados com o ImageMagick por `scripts/gerar-icones.sh` (no Windows, `scripts\gerar-icones.bat`) a partir de `Wrexercise/webapp/icon-source.png`, que fica fora de `public/` para não entrar no pré-cache. Para trocar o ícone, passe o PNG novo, quadrado e de preferência com 1024 px: `scripts/gerar-icones.sh novo-icone.png`. O script copia o arquivo para `icon-source.png` e gera tudo de novo:
  - `favicon.ico`, `icon-192.png` e `icon-512.png`: a arte inteira, com os cantos transparentes. O `BrandMark` da interface usa o `icon-192.png`.
  - `apple-touch-icon.png`: recorte interno sem os cantos, opaco, porque o iOS arredonda os cantos e pinta a transparência de preto.
  - `icon-maskable-512.png`: fundo cheio na cor da borda da arte e a arte a 72%, dentro da zona segura.

## Aviso de instalação (acréscimo)

Quem usa o Wrexercise pelo navegador raramente abre Configurações, então não descobre que pode instalar o aplicativo.

- Um aviso em `AvisosDoApp`, no mesmo formato do de versão nova, oferece a instalação: botão "Instalar" quando o navegador oferece `beforeinstallprompt`, e a instrução do menu Compartilhar no iOS.
- Aparece só para quem entrou na conta, com conexão e sem o aviso de versão nova na tela. Some depois da instalação e não aparece no aplicativo instalado.
- Não some sozinho, porque tem uma ação. Um botão "Dispensar" o esconde por 30 dias. A opção em Configurações continua disponível.
- A dispensa fica num cookie comum, `wx_instalacao_dispensada`, e não em `localStorage`, que o ESLint bloqueia para evitar credenciais no navegador. O cookie não é credencial, e o backend o ignora.
- **Alternativas descartadas:** liberar `localStorage` enfraqueceria a regra do ESLint; guardar a dispensa só na memória repetiria o aviso a cada visita.
- **Compromisso aceito:** Firefox e Safari no computador não oferecem instalação por script e não mostram o aviso.

## Impacto

- Q022 e Q024 ficam decididas.
- Não entra nenhuma dependência nova.
- Instalação e Service Worker exigem HTTPS ou `localhost`. O túnel e o Caddy já atendem a isso.
- **Compromisso aceito:** offline, uma página que nunca foi visitada naquela versão não abre, porque o chunk dela não está no cache. As telas principais abrem desde que tenham sido visitadas uma vez.
- **Compromisso aceito:** depois de uma implantação, uma aba aberta que ainda não percebeu a versão nova pode falhar ao carregar uma página sob demanda cujo chunk antigo já foi removido. Isso acontecia antes desta decisão, e recarregar resolve.
