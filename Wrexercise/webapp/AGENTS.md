# Frontend — React + TypeScript

Complementa o `AGENTS.md` da raiz. Os comandos `npm` rodam nesta pasta, e o build gera `../wwwroot`, de onde o backend serve a SPA.

## Estrutura

```text
src/
├── components/   # componentes reutilizáveis, sem regra de negócio
├── features/     # uma pasta por funcionalidade, com index.ts como interface pública
├── pages/        # telas, sempre carregadas sob demanda
├── navigation/   # rotas
├── services/     # cliente HTTP
└── styles/
```

- Uma feature importa outra apenas pelo `index.ts` (`@/features/grupos`). O ESLint verifica isso.
- Use o alias `@/` em importações entre pastas.

## Estado e dados

- Dados remotos: TanStack Query. Estado de interação: estado local do React. Sem Redux.
- Toda chamada à API passa por `services/api.ts`, na mesma origem. Sem CORS.
- Evite polling. Pagine listas.

## Interface

- Mobile-first, com o Apple Human Interface Guidelines como inspiração, sem copiar componentes proprietários.
- As telas seguem o mockup do projeto. Antes de portar uma, confira a tabela de divergências em `docs/decisoes/0014-novo-mockup-como-referencia-visual.md`.
- Tailwind e componentes próprios. Sem bibliotecas de UI pesadas.
- Cores só por tokens semânticos (`bg-surface`, `text-muted`, `bg-game`, `bg-reward`, `bg-progress`), definidos em `styles/index.css` para os dois temas. Não use `dark:` nem as paletas diretamente em páginas e componentes. A única exceção é a matiz do `Avatar`, que vem do identificador do usuário.
- Nada carregado de servidores externos: fontes e ícones ficam no bundle ou são do sistema. Fontes só com o subconjunto latino.
- Cada reação tem sempre o mesmo símbolo (`tipoReacaoEmojis`): 💪 `Kudos`, 🤨 `Suspeito`, ❌ `Rejeitar`. Use-o ao lado do texto ou sozinho quando faltar espaço; sozinho, inclua um texto para leitores de tela.
- Destaques de adesivo (`card-sticker`, `badge-*`, `shadow-sticker`) só no que é importante: um ou dois por tela. Anton (`font-display`, `font-score`) só em títulos e números, nunca com `font-bold`.
- Todo texto visível em pt-BR.

### Skill `ui-ux-pro-max`

Todo trabalho de UI/UX (telas, componentes, layout, cores, tipografia, animação, acessibilidade, revisão visual) começa carregando a skill `ui-ux-pro-max` (`.claude/skills/ui-ux-pro-max/`). Use as buscas por `--domain` e `--stack react`/`--stack html-tailwind` e, antes de entregar, o checklist de `references/pro-rules.md`.

A cópia versionada no repositório é a única fonte. O plugin global de mesmo nome fica desligado em `.claude/settings.json`, para não carregar descrições de skills duplicadas em toda sessão.

As recomendações dela não se sobrepõem às regras deste repositório. Em caso de conflito, valem estas regras, o mockup e as ADRs 0014 e 0027. Em particular:

- Não use `--design-system --persist`: os tokens ficam em `styles/index.css`, sem pasta `design-system/`.
- Não adote fontes do Google Fonts nem ícones externos. Os símbolos de reação continuam sendo emojis.
- Não troque a identidade visual por uma paleta ou estilo sugerido pela skill. A paleta, as fontes e a linguagem gráfica são as da identidade Wrexercise (decisão 0027).

## Desempenho

Pensado para conexões móveis lentas: bundles pequenos, páginas com `lazy`, evidências carregadas só quando solicitadas. O feed mostra apenas miniaturas, geradas no navegador e com `loading="lazy"`; a imagem completa só aparece ao abrir a atividade ou ao tocar na miniatura, que abre a galeria em tela cheia.

## Sessão e PWA

- A sessão é um cookie `HttpOnly`: o JavaScript nunca vê o token. O ESLint bloqueia o uso de `localStorage`.
- Preferências que não são credenciais, como a dispensa do aviso de instalação, ficam num cookie comum (`wx_instalacao_dispensada`). Não libere `localStorage` para isso.
- O Service Worker não guarda respostas autenticadas nem imagens privadas.
- O Service Worker fica em `sw.js`, escrito à mão. O plugin `serviceWorker()` de `vite.config.ts` injeta a versão e o pré-cache no build. Ele nunca intercepta `/api`, `/health` nem `/ready`, e só é registrado no build de produção. Ver `docs/decisoes/0021-pwa-e-service-worker.md`.
- Escritas não têm fila offline: as mutações usam `networkMode: "always"` e falham na hora sem rede.
