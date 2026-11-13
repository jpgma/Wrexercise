# 0055 — Questões resolvidas pela implementação

**Status:** DECIDIDA. Fecha Q006, Q007, Q017, Q018, Q021, Q023 e Q032. Registra o estado parcial de Q027 e Q031.

## Contexto

`questoes-abertas.md` ficou atrás do código. Várias questões marcadas como `ABERTA` ou `PROPOSTA` já foram resolvidas durante as fases de implementação, seguindo a recomendação da própria questão, sem um ADR que as fechasse. Isso escondia a lista do que de fato continua aberto.

Este ADR não toma decisões novas: registra o que o código já faz, com o lugar onde conferir.

## Alternativas consideradas

- **Um ADR por questão:** sete documentos curtos repetindo o código.
- **Mudar só o status em `questoes-abertas.md`:** contraria a regra de que fechar uma questão gera um ADR.
- **Um ADR único com o registro de cada questão:** escolhida.

## Decisão

| Questão | Como ficou | Onde conferir |
|---|---|---|
| Q006 — Contratos JSON | Records explícitos por operação, nunca estruturas internas. Serialização gerada em compilação (`JsonSerializerDefaults.Web`: camelCase). Enums trafegam como texto fixo (`"Kudos"`, `"AguardandoFeedback"`). Datas em `DateTimeOffset` ISO 8601. Sem versionamento: o único cliente é a SPA servida pela mesma origem e publicada junto. | `Infrastructure/Json/AppJsonContext.cs`, `Wrexercise/AGENTS.md` (Native AOT) |
| Q007 — Erros da API | `application/problem+json` com `title`, `status` e um `code` estável em `MAIUSCULAS_COM_SUBLINHADO`, título em pt-BR e sem detalhes internos. O frontend mostra o `title`. | `Infrastructure/Errors/ApiProblem.cs`, `Wrexercise/AGENTS.md` (Erros) |
| Q017 — Formatos de evidência | `RegistroManual` ou `Fotografia` (`CapturaDeTela` existe no código, mas a interface não usa). JPEG, PNG e WebP, até 5 MB, conferidos pelo conteúdo do arquivo. Miniatura JPEG até 100 KB. O arquivo enviado não tem EXIF. | `Infrastructure/Files/ValidacaoImagemEvidencia.cs`, `0011-atividades.md`, `0052-avisos-nas-evidencias.md` |
| Q018 — Armazenamento | Disco local do servidor (hoje o A14, `0032`), em pastas `AAAA/MM` com nomes aleatórios (GUID). O diretório nunca é servido diretamente: evidência e miniatura passam por rota autenticada que confere o grupo. Backup em `0022` e `0046`. A foto só é apagada junto com a atividade (`0054`). | `Infrastructure/Files/ArmazenamentoEvidencias.cs`, `Features/Atividades/ObterEvidencia.cs`, `ObterMiniatura.cs` |
| Q021 — Estado do frontend | TanStack Query para dados remotos, estado local do React para interação, sem Redux nem cache persistido. | `Wrexercise/webapp/AGENTS.md` (Estado e dados) |
| Q023 — Imagens no frontend | O navegador redimensiona para no máximo 1600 px (JPEG 0,82) e gera a miniatura de 240 px (JPEG 0,6), sem biblioteca. Não converte para WebP. O envio mostra "Enviando N de M", sem barra por arquivo. | `webapp/src/features/atividades/comprimirImagem.ts`, `funcionalidades.md` §7.2 |
| Q032 — Páginas do MVP | As sete páginas propostas existem, e outras entraram por ADRs próprios (Perfil, Membro, Loja, Avatar, Notificações, Revisar). A lista viva é `funcionalidades.md`. | `webapp/src/pages/` |

### Estado parcial

- **Q027 — Limite de requisições:** há limites por IP em janela fixa de 1 minuto para tentativas de autenticação (10), envio de evidência (30), comentários (20) e envio de folha de avatar (10). Criação de atividade, reações e consultas ao feed não têm limite próprio: a fila de avaliação (0041) e a chave de idempotência (0053) já freiam a criação, e o grupo tem até dez pessoas. Acrescentar limites só se aparecer abuso.
- **Q031 — Design system:** tipografia, paleta e temas claro e escuro vêm de `0014` e `0027`. O tema segue o sistema; falta decidir se vale um seletor manual.

## Impacto

- `questoes-abertas.md` passa a apontar para este ADR nas questões acima, e a §12 ganha a lista do que continua aberto.
- Nada muda no código.
