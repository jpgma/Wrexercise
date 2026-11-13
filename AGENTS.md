# Regras do repositório — Wrexercise

Aplicação web privada para pequenos grupos manterem a rotina de exercícios por meio de desafios e validação comunitária. Arquitetura completa em `docs/arquitetura.md`; resumo de todas as funcionalidades em `docs/funcionalidades.md`; decisões pendentes em `docs/questoes-abertas.md`; decisões tomadas em `docs/decisoes/`.

Regras específicas de cada stack: `Wrexercise/AGENTS.md` e `Wrexercise/webapp/AGENTS.md`.

**Princípio:** simples o suficiente para uma única pessoa entender, implantar e manter. Entre duas alternativas que atendem aos requisitos, escolha a mais simples.

## Antes de concluir qualquer trabalho

Não há CI nem hooks de git: este passo é a única verificação.

```bash
scripts/verify.sh          # formatação, lint, build, testes unitários, build do frontend
scripts/verify.sh --full   # também testes de integração e imagem arm64 (exige Docker)
scripts/verify.sh --image  # como --full, mas sempre constrói a imagem arm64
```

Rode `--full` ao mexer em SQL, migrações, `Program.cs`, `deploy/` ou dependências. O `--full` só constrói a imagem arm64 quando muda algo que a afeta e ainda não foi enviado (`deploy/Dockerfile`, `.dockerignore`, `*.csproj`, `Directory.*.props`, `global.json`, `package*.json` do frontend); `--image` força a construção. Não declare o trabalho concluído com falhas, e não enfraqueça uma verificação para fazê-la passar.

## Idioma

- **pt-BR com acentos:** documentação, textos da interface, comentários, mensagens de erro e de commit.
- **Exceção:** `docs/funcionalidades.md` fica em inglês, no padrão ASD-STE100 (Simplified Technical English), para servir de contexto a agentes.
- **Código:** use o teste abaixo.

### Regra de nomes

> Um participante reconheceria a palavra como conceito do aplicativo? **Sim:** português sem acentos. **Não:** inglês.

| Item | Exemplo |
|---|---|
| Pastas e arquivos | `Features/Autenticacao/Registrar.cs`, `Features/Reacoes/Routes.cs`, `Infrastructure/Database/Migrations/` |
| C# | `MapReacoesRoutes()`, `ReacaoRequest`, `GetReacoesByAtividade()`, `CalculateRejeicoesNecessarias()` |
| TypeScript | `features/atividades/useAtividade.ts`, `InicioPage` |
| Tabelas e colunas de domínio | `reacoes_atividade`, `duracao_segundos`, `nome_exibicao` |
| Colunas técnicas | `id`, `created_at`, `started_at`, `expires_at`, `revoked_at`, `password_hash` |
| Rotas e JSON | `PUT /api/atividades/{id}/reacao`, `{ "tipo": "Kudos" }` |
| Códigos de erro | `ATIVIDADE_NAO_ENCONTRADA` |
| Operação | serviços `app`, `db`, `proxy`; variáveis `DB_PASSWORD` |
| Documentos | nomes em pt-BR: `docs/decisoes/0001-migracoes-sql.md` |

### Glossário do domínio

Use exatamente estas grafias:

`Usuario`, `Sessao`, `Autenticacao`, `Cadastro`, `Redefinicao`, `Grupo`, `Membro`, `Papel`, `Proprietario`, `Administrador`, `Convite`, `Desafio`, `Participante`, `Meta`, `MetaPorTipo`, `Semana`, `Atividade`, `Evidencia`, `Comprovacao`, `Revisor`, `Revisao`, `Reacao`, `Comentario`, `Kudos`, `Suspeito`, `Rejeitar`, `Rejeicao`, `Progresso`, `Classificacao`, `Sequencia`, `Conquista`, `Feed`, `Notificacao`, `Jogo`, `Cena`, `Avatar`, `Energia`, `Loja`, `Carta`, `Provocacao`, `Resposta`, `Jogada`, `Mao`, `Mesa`, `Enfeite`, `Vitrine`, `Missao`, `Rival`, `ConfiguracaoInicial`, `Foco`, `Aviso`.

Tipos de `Aviso` (ADR 0052): `FotoForaDoPeriodo`, `FotoSemData`, `FotoParecida`, `RegistroTardio`.

Na interface, `Kudos` aparece como "Dar moral" e "deu moral". Cada reação tem um símbolo fixo: 💪 `Kudos`, 🤨 `Suspeito`, ❌ `Rejeitar`.

Situações de verificação: `AguardandoFeedback`, `Verificada`, `EmRevisao`, `Contestada`, `Rejeitada`.

Situações de um desafio: `Agendado`, `Ativo`, `Encerrado`, `Cancelado`.

Modos da tela inicial: `App`, `Jogo` (ver `docs/gamificacao.md`). A tela do modo `Jogo` mostra a `Cena` do grupo; tocar num colega abre o `Foco`, a câmera aproximada nele. A `Mao` fica logo abaixo da praça, com a `Mesa` (as Jogadas de quem vê) embaixo das cartas.

Ao introduzir um conceito novo, acrescente-o aqui na mesma alteração.

## Organização

Monólito modular com fatias verticais. Cada funcionalidade é dona de seus endpoints, contratos, SQL, regras e testes.

```text
Wrexercise/                 # aplicação: API e frontend
├── Features/               # fatias verticais
├── Infrastructure/         # recursos compartilhados
├── webapp/                 # código-fonte do frontend (React)
└── wwwroot/                # gerado a partir de webapp/, fora do git
Wrexercise.UnitTests/       # regras puras e testes de arquitetura
Wrexercise.IntegrationTests/   # HTTP e SQL contra o PostgreSQL
deploy/  scripts/  docs/
pixel-art/                  # scripts Python que geram a arte do Jogo (avatar-base/, praca/, ambiente/, jpgma/, base-masculino/, seed-avatares/; ver docs/avatar-kit.md)
```

Arte em pixel do Jogo: use a skill `.claude/skills/pixel-art-studio/` (ver `docs/avatar-kit.md`). Cada arte tem um `pixel-art/<slug>/build.py` versionado; nunca edite o PNG à mão. Arquivos `.aseprite`/`.ase` usam sempre o plugin `pixel-plugin` (configuração em `docs/avatar-kit.md` §6).

Especificações com Spec Kit: as skills `speckit-*` em `.claude/skills/` (`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`) usam `.specify/` e gravam em `specs/NNN-nome/`. A constituição em `.specify/memory/constitution.md` apenas aponta para este arquivo e para `docs/`. Atualize com `specify init --here --force --integration claude --script ps`.

Não recrie `src/`, `tests/` ou `web/`: projetos ficam na raiz e o frontend fica em `Wrexercise/webapp/`. Ver `docs/decisoes/0004-estrutura-do-repositorio.md`.

Fatias planejadas, criadas apenas na fase correspondente de `docs/arquitetura.md` §18: `Autenticacao`, `Usuarios`, `Grupos`, `Convites`, `Desafios`, `Atividades`, `Reacoes`, `Progresso`, `Feed`, `Notificacoes`, `Jogo` (opcional, fases em `docs/gamificacao.md`).

- Não crie pastas, arquivos ou dependências antes de precisar deles.
- Uma fatia não acessa detalhes internos de outra. Para obter dados de outra fatia, faça uma consulta SQL explícita ou use um contrato pequeno e estável (ver `Wrexercise/AGENTS.md`). Um teste de arquitetura e uma regra de ESLint verificam isso.
- Testes ficam nos projetos de teste, espelhando o caminho da fatia, e nunca dentro de `Wrexercise/`.

## Proibido sem uma decisão registrada

- Repositórios genéricos ou serviços que apenas encapsulam chamadas ao banco.
- Entity Framework, Dapper, ou qualquer biblioteca que dependa de reflexão.
- Projetos separados de domínio, aplicação e infraestrutura.
- SSR, Next.js ou Node.js em produção.
- Fontes, ícones ou bibliotecas carregados de servidores externos.
- Senhas, tokens ou credenciais em `localStorage`.
- Infraestrutura distribuída: filas, cache externo, WebSockets, observabilidade dedicada.
- Itens fora do escopo em `docs/arquitetura.md` §19.

## Segurança

- Todo endpoint privado verifica autenticação **e** pertencimento ao grupo no backend. Nunca confie em identificadores vindos do frontend.
- SQL sempre parametrizado.
- Nunca registre em log senhas, cookies, tokens, evidências ou dados sensíveis.
- Erros não revelam detalhes internos.
- Segredos só por configuração externa. Nada de segredo no repositório.

## Dependências

Toda dependência nova precisa de justificativa na descrição da alteração: o que resolve, por que não dá para fazer sem ela e se é compatível com Native AOT. No backend, ela também entra na lista de `PackageAllowlistTests`.

## Decisões e documentação

- Decisões em `docs/arquitetura.md` não são reabertas sem justificativa técnica concreta.
- Decisão que muda a arquitetura ou fecha uma questão de `docs/questoes-abertas.md` gera um ADR numerado em `docs/decisoes/` (contexto, alternativas, decisão, impacto) e atualiza o status da questão.
- Se uma alteração contradiz a documentação, atualize a documentação na mesma alteração.
- Toda alteração que acrescenta, remove ou muda uma funcionalidade, regra, limite, permissão, tela ou texto relevante da interface atualiza `docs/funcionalidades.md` na mesma alteração, mantendo o padrão ASD-STE100: frases curtas, voz ativa, vocabulário simples e nomes do código entre crases.

## Commits

Conventional Commits com tipo em inglês e descrição em pt-BR:

```text
feat(reacoes): adiciona contagem de rejeições
fix(autenticacao): corrige expiração da sessão
chore(deploy): fixa versão do Caddy
```

Cada commit deve deixar `scripts/verify.sh` passando.

### Branches e worktrees

- Não crie worktrees nem branches sem pedido explícito do mantenedor.
- Trabalhe direto em `main`: faça o commit e o push para `origin/main` assim que `scripts/verify.sh` passar.
