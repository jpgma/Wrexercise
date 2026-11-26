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

`Usuario`, `Sessao`, `Autenticacao`, `Cadastro`, `Redefinicao`, `Grupo`, `Membro`, `Papel`, `Proprietario`, `Administrador`, `Convite`, `Desafio`, `Participante`, `Meta`, `MetaPorTipo`, `Semana`, `Atividade`, `Evidencia`, `Comprovacao`, `Revisor`, `Revisao`, `Reacao`, `Comentario`, `Kudos`, `Suspeito`, `Rejeitar`, `Rejeicao`, `Progresso`, `Classificacao`, `Sequencia`, `Conquista`, `Feed`, `Notificacao`, `Jogo`, `Cena`, `Avatar`, `Energia`, `Loja`, `Carta`, `Provocacao`, `Resposta`, `Jogada`, `Mao`, `Mesa`, `Enfeite`, `Vitrine`, `Missao`, `Rival`, `ConfiguracaoInicial`, `Foco`, `Aviso`, `Foto`, `Video`, `Fadiga`, `Cutucada`, `Integracao`, `AppConectado`, `Rascunho`, `Correcao`, `Fusao`, `Chegada`, `Versao`, `Novidade`, `EmAndamento`, `Torcida`, `Parceria`, `AoVivo`, `Marco`, `Percurso`, `Recorde`, `Janela`, `Devolucao`, `Revanche`, `Prazo`, `Global`, `Convocacao`.

`Global` e `Convocacao` (ADR 0091): uma carta `Global` vale para o grupo inteiro; a primeira é o TODO MUNDO (150 EN). Jogada da Mão fora do Foco, ela cria a `Convocacao` do grupo: o anfitrião escolhe o tipo de treino (menos Outro e Outro esporte), e por 7 dias quem foi convidado (os membros no modo Jogo naquele instante) cumpre com um treino verificado desse tipo e ganha +30 EN. Uma Convocação ativa por grupo. Para o convidado, funciona como uma Provocação recebida: recusar, deixar sem resposta, sair ou não cumprir dá FADIGA, e cumprir tira 1 carga; o convite não trava o app. O preço volta para o anfitrião dividido entre os outros convidados, uma parte por resultado final. Na interface: "Globais", "Convocar a galera", "Tô dentro!", "Dessa vez não · FADIGA", "Sair · FADIGA", "Missão cumprida! +30 EN", "Convocações anteriores".

Treino ao vivo (ADR 0078): a pessoa começa o treino agora ("Começar treino") e termina depois ("Terminar treino" ou "Cancelar treino"). Enquanto isso, a atividade fica `EmAndamento`: não conta para nada, ninguém avalia, e o grupo vê o Avatar treinando na praça com a bolha "AO VIVO". O cronômetro fica em `/grupos/{id}/treino`. `Torcida`: um colega torce por quem treina ao vivo ("Torcer", uma vez a cada 10 minutos). `Parceria`: o bônus por treinar ao vivo junto com um colega ("Treinar junto"). `AoVivo`: o bônus do treino começado ao vivo. `Marco`: os 15, 30, 45, 60, 90, 120 e 180 minutos do cronômetro, quando o Avatar comemora. Cada tipo tem a sua animação, e na praça o Avatar usa o Enfeite do tipo que está no chão, dá voltas (Corrida, Caminhada, Ciclismo), reveza na barra (Calistenia) ou troca passes (Futebol, Vôlei) (ADR 0082). Na interface: "Começar agora", "Já treinei", "AO VIVO", "Manter a tela acesa".

`Fusao` (ADR 0070): duas Provocações diferentes jogadas juntas, numa Jogada só. Um mesmo treino cumpre os dois objetivos, e é tudo ou nada. Quem cumpre ganha a soma dos preços mais 20%. Na interface: o selo "FUSÃO" e o botão "Jogar fusão".

Próximo treino (ADR 0075): uma Provocação aceita só vale no primeiro treino registrado depois do aceite (em +10 MIN e +20 MIN, no dia dele). Se ele não cumpre o objetivo, a Jogada fica `Falhou`, com FADIGA para quem recebeu. Na interface, o selo é "Não cumprida". A ANTES DAS 8 pede um treino que termine até as 08:00. Um treino que começou depois que a carta chegou e antes do aceite também vale, se cumpre, e nunca faz falhar (ADR 0076).

`Janela` (ADR 0086): só um treino na janela da carta pode ser o próximo treino; fora dela, ele não conta nem faz falhar. ANTES DAS 8: treinos que começam antes das 08:00. FIM DE SEMANA: treinos no sábado ou no domingo (essa carta nunca falha, só é cumprida ou expira). As outras cartas aceitam qualquer treino; na Fusão, vale a janela das duas. Na interface: "Vale o próximo treino que começar antes das 8h", "A carta continua valendo".

`Devolucao` (ADR 0087, 0089): quando a carta fica `Falhou` ou `Expirada`, quem comprou a Provocação recebe metade do preço em Energia (arredondada para baixo; na Fusão, metade da soma). Na `Recusada`, recebe o preço inteiro (a `Revanche`). A Jogada do DESAFIO DUPLO não devolve. O aviso `CartaNaoCumprida` avisa a falha e a expiração. Na interface: "+N EN de volta", "Cartas não cumpridas (metade de volta)".

`Chegada` (ADR 0076): a Provocação recebida que espera resposta toma o aplicativo, em qualquer tela e modo. A praça do grupo mostra o arremesso com a câmera do Foco, e a carta vira o painel da resposta, sem fechar. Enquanto houver carta sem resposta, o servidor recusa o registro de treino novo (`CARTA_SEM_RESPOSTA`), mas deixa começar o treino ao vivo (ADR 0086). "Responder depois do treino" suspende a Chegada no aparelho até o treino ao vivo acabar ou chegar uma carta nova (cookie `wx_chegada_adiada`, até 6 horas); o prazo não muda. Na interface: "Aceitar · +N EN", "Recusar · FADIGA: …" (um toque, sem confirmação), "Responder depois do treino", "Registrar treino", "Depois" e "Responder a carta".

`Versao` e `Novidade` (ADR 0077): o app tem uma versão `X.Y.Z`, e cada versão traz uma lista curta de novidades. Na primeira abertura depois da atualização, um diálogo "Novidades" mostra o que mudou, uma vez por aparelho. Na interface: "Novidades", "Bora!", "Versão 1.0.0" e a página Perfil › Configurações › Novidades.

`Correcao` (ADR 0068): o proprietário ou um administrador corrige a duração da atividade de outra pessoa, com justificativa. Vale também para atividade `Verificada`; não vale para `Rejeitada` nem `Anulada`. Todo o grupo vê a correção na atividade. Na interface, o botão é "Corrigir duração".

`Fadiga` (ADR 0059, 0089): debuff de quem recusa, deixa expirar ou não cumpre uma Provocação recebida. Os próximos dias verificados rendem metade da base de Energia: 3 cargas na recusa e na expiração, 2 na falha. Recusar com FADIGA soma 1, até 5. Cumprir uma carta recebida tira 1. Com FADIGA, a pessoa não joga Provocações (`FADIGA_ATIVA`), e o grupo vê a bateria fraca na praça e o motivo no Foco. Na interface: "FADIGA", "Sem gás · N treinos pra recarregar", "Quem não aguenta, não provoca".

`Revanche` (ADR 0089): quem teve a carta recusada recebe o preço inteiro de volta e pode mandar outra carta a essa pessoa sem esperar as 24 horas, uma vez. Na interface: "Revanche liberada".

`Cutucada` (ADR 0063): um membro chama um colega que dorme no grupo, com uma de 6 mensagens prontas (`Sofa`, `Frango`, `Meta`, `Halter`, `Despertador`, `Fantasma`) ou um texto escrito (tipo `Livre`, até 80 caracteres, só quem recebe lê; ADR 0064). Na interface, o botão é "Cutucar" e a notificação diz "te cutucou 👉". Quem cutucou ganha o bônus Despertador quando o colega volta a treinar.

Tipos de `Aviso` (ADR 0052, 0058): `FotoForaDoPeriodo`, `FotoSemData`, `FotoParecida`, `RegistroTardio`, `VideoParado`. Os nomes `Foto*` valem também para vídeos.

Tipos de `Evidencia` (ADR 0058): `Foto`, `Video`. Evidência adicionada depois (ADR 0083): o autor manda foto ou vídeo depois do prazo da verificação (ou o Movr manda o resumo que faltou); ela aparece com o selo "Adicionada depois", mas não verifica nem muda a comprovação. Na interface: "Adicionar foto ou vídeo". Um `Comentario` pode ter uma foto ou um vídeo, que não é evidência. Origens da data da captura: `Exif`, `Arquivo`, `Camera` (a câmera do aplicativo), `Print` (a data no nome de uma captura de tela, ADR 0060), `Integracao` (o fim do treino informado por um app conectado, ADR 0065; só o servidor a atribui), `Percurso` (a imagem do percurso gerada ao terminar o treino, ADR 0084; só o servidor a atribui).

`Percurso` (ADR 0084): na Corrida, na Caminhada e no Ciclismo ao vivo, o aplicativo grava o GPS com a tela acesa e o grupo vê o trajeto no mapa, sem os primeiros e os últimos 200 m. Ao terminar, uma imagem com o mapa e os números entra como evidência com o selo "Percurso", sem mudar a comprovação. A distância nunca pontua: só aparece no mapa, nos recordes pessoais (`Recorde`: `Distancia` ou `Ritmo`) e nas conquistas `Fundista`, `Peregrino` e `Estradeiro`. "Treinaram juntos": dois percursos ao vivo lado a lado. No treino ao vivo do Movr, quem grava e manda o percurso é o Movr, e os dois aplicativos calculam os números com o mesmo algoritmo v1 (ADR 0085). Na interface: "Gravar o percurso e mostrar ao grupo", "Percurso", "Gravando", "Modo bolso", "Segure para sair", "Gravar neste aparelho", "Imagem do percurso", "Novo recorde", "Treinaram juntos".

`Integracao` (ADR 0065): um app conectado (`AppConectado`, hoje só o Movr) prepara um `Rascunho` de atividade, com a imagem do resumo do treino. Quem posta é sempre a pessoa, no formulário. Situações do rascunho: `Pendente`, `Postada`, `Expirada`. Na interface: "Conectar o Movr", "Apps conectados", "Treino importado do Movr". O Movr também começa o treino ao vivo no grupo escolhido, informa o fim como sugestão e manda o resumo; quem termina é a pessoa (ADR 0081). Na interface: "Movr: treino concluído". Na interface, a evidência que é um print (pelo nome ou pelo tamanho da tela, coluna `print`) mostra o selo "Print".

Na interface, `Kudos` aparece como "Dar moral" e "deu moral". Cada reação tem um símbolo fixo: 💪 `Kudos`, 🤨 `Suspeito`, ❌ `Rejeitar`.

Situações de verificação: `EmAndamento` (treino ao vivo, ADR 0078), `AguardandoFeedback`, `Verificada`, `EmRevisao`, `Contestada`, `Rejeitada`, `Anulada` (ADR 0054: o administrador anula um registro errado; o autor pode apagar a própria atividade nos primeiros 15 minutos, em qualquer situação menos `Verificada`, ADR 0073).

Situações de um desafio: `Agendado`, `Ativo`, `Encerrado`, `Cancelado`.

Classificação do desafio (ADR 0079): fica na página do grupo, abaixo das "Regras do desafio", com uma frase que diz por que cada pessoa está à frente da seguinte. A praça do modo `Jogo` não tem Placar, só as medalhas 🥇🥈🥉.

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
pixel-art/                  # scripts Python que geram a arte do Jogo (avatar-base/, praca/, ambiente/, jpgma/, base-masculino/, seed-avatares/, cartas/, enfeites/, fadiga/, fusao/; ver docs/avatar-kit.md)
```

Arte em pixel do Jogo: use a skill `.claude/skills/pixel-art-studio/` (ver `docs/avatar-kit.md`). Cada arte tem um `pixel-art/<slug>/build.py` versionado; nunca edite o PNG à mão. Arquivos `.aseprite`/`.ase` usam sempre o plugin `pixel-plugin` (configuração em `docs/avatar-kit.md` §6).

Especificações com Spec Kit: as skills `speckit-*` em `.claude/skills/` (`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`) usam `.specify/` e gravam em `specs/NNN-nome/`. A constituição em `.specify/memory/constitution.md` apenas aponta para este arquivo e para `docs/`. Atualize com `specify init --here --force --integration claude --script ps`.

Não recrie `src/`, `tests/` ou `web/`: projetos ficam na raiz e o frontend fica em `Wrexercise/webapp/`. Ver `docs/decisoes/0004-estrutura-do-repositorio.md`.

Fatias, criadas na fase correspondente de `docs/arquitetura.md` §18: `Autenticacao`, `Usuarios`, `Grupos`, `Convites`, `Desafios`, `Atividades`, `Reacoes`, `Progresso`, `Notificacoes`, `Integracoes`, `Jogo` (fases em `docs/gamificacao.md`) e `Health`. `Feed` não tem pasta própria: as listas de atividades ficam em `Atividades`.

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

## Versões e novidades

A versão do app e as novidades de cada versão ficam só em `Wrexercise/webapp/src/features/novidades/versoes.json`, da mais nova para a mais antiga. A primeira entrada é a versão atual. Ver `docs/decisoes/0077-versoes-e-novidades.md`.

**Quando mudar a versão.** Toda alteração que o participante percebe (funcionalidade, regra, tela, texto relevante ou correção visível) atualiza o `versoes.json` no mesmo commit:

- **Patch** (`1.0.0` → `1.0.1`): correção.
- **Minor** (`1.0.0` → `1.1.0`): funcionalidade nova ou regra que muda.
- **Major** (`1.0.0` → `2.0.0`): mudança grande, que muda o jeito de usar o app.
- Alteração que ninguém percebe (refatoração, testes, documentação, implantação) não muda a versão.
- Vários commits antes da mesma implantação entram na mesma entrada ainda não publicada: suba a versão uma vez e acrescente tópicos. Use o maior tipo entre eles.
- A data é a do dia da implantação, no formato `AAAA-MM-DD`.

**Como escrever as novidades.** Tópicos em pt-BR, curtos, claros e descontraídos, escritos para quem usa o app:

- Um assunto por tópico, de 1 a 6 tópicos por versão, até 80 caracteres e sem ponto final.
- Diga o que muda para a pessoa, não o que mudou no código. Nada de nomes de arquivo, endpoint, tabela, ADR ou termos técnicos.
- Comece pelo efeito: "Agora dá pra…", "Corrigido: …", "A carta … agora …". Emoji é opcional, no máximo um por tópico.
- Use os nomes da interface ("Dar moral", "Corrigir duração", "FADIGA"), não os do código (`Kudos`, `Correcao`).

| Bom | Ruim |
|---|---|
| Agora dá pra apagar o treino nos primeiros 15 min | Adiciona endpoint DELETE /api/atividades/{id} com janela de 15 minutos. |
| Corrigido: vídeo do iPhone não abria em alguns celulares | fix(atividades): reempacota MP4 com moov no início |
| Carta recebida agora aparece assim que você abre o app 🃏 | Implementa a Chegada (ADR 0076) com PortaoChegada no AppLayout |

**Etiquetas e implantação.** O `run.bat --ssh` grava a versão no `versao.txt` do celular e, depois de implantar sem alterações fora de commit, cria e envia a etiqueta `vX.Y.Z` no commit implantado, se ela ainda não existe. Não crie nem mova etiquetas de versão à mão.

`VersoesTests` (em `Wrexercise.UnitTests`) verifica o formato, a ordem, as datas, o número de tópicos e o limite de caracteres. Não afrouxe o teste para fazer uma entrada caber: reescreva a entrada.

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
