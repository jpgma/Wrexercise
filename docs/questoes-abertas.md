# Wrexercise — Open Questions

> Documento de pesquisa e decisões pendentes.
>
> Projeto: Wrexercise  
> Idioma oficial: Português brasileiro (pt-BR)  
> Arquitetura: Monólito VSA · .NET 10 Native AOT · PostgreSQL · React PWA

## 1. Objetivo deste documento

Este documento reúne questões técnicas e funcionais que precisam ser investigadas ou decididas durante a implementação do Wrexercise.

As decisões já estabelecidas em `arquitetura.md` não devem ser reabertas sem uma justificativa técnica concreta.

Cada questão deve receber:

- Contexto.
- Alternativas consideradas.
- Recomendação.
- Impacto da decisão.
- Status.

### Status possíveis

- `ABERTA`: ainda precisa de investigação.
- `PROPOSTA`: existe uma recomendação inicial.
- `DECIDIDA`: decisão aprovada.
- `ADIADA`: não necessária para o MVP.
- `BLOQUEADA`: depende de outra decisão.

---

# 2. Infraestrutura e implantação

## Q001 — Qual será a configuração definitiva da VM Oracle?

**Status:** DECIDIDA — a VM foi abandonada; a produção é o Galaxy A14 (`decisoes/0032-a14-como-producao.md`). O texto abaixo é o histórico.

Investigar:

- Disponibilidade de instâncias Ampere A1 na região escolhida.
- Configuração de 1 OCPU e 6 GB RAM.
- Limites de armazenamento Always Free.
- Requisitos de rede e IP público.
- Configuração de firewall.
- Compatibilidade do Ubuntu Server ARM64 com Docker.

**Recomendação inicial:**

Começar com 1 OCPU e 6 GB RAM, reservando margem para PostgreSQL, aplicação e sistema operacional.

**Critério de conclusão:**

Ter uma VM funcional com Docker Compose, HTTPS e armazenamento persistente.

**Situação atual (2026-11-03):** adiada. A franquia A1 caiu para 2 OCPUs e 12 GB em 2026. Em `sa-saopaulo-1`, que tem um único domínio de disponibilidade e não pode ser trocada por ser a região inicial da conta, as tentativas com 2 OCPUs e 12 GB e com 1 OCPU e 6 GB falharam por falta de capacidade. Ver a proposta em `decisoes/0005-implantacao-via-ssh.md`.

## Q002 — Como organizar os backups?

**Status:** DECIDIDA — ver `decisoes/0022-backups.md`. `pg_dump` e `tar` diários em `BACKUP_DIR`, com 7 dias de retenção; a cópia externa é feita por pasta sincronizada.

Investigar alternativas para:

- Backup diário do PostgreSQL.
- Backup das evidências.
- Retenção de versões.
- Armazenamento externo.
- Restauração completa em uma nova VM.

Questões:

- Utilizar `pg_dump` ou backup físico?
- Onde armazenar os backups externos?
- Existe uma opção de armazenamento gratuito com capacidade suficiente?
- Como verificar automaticamente a integridade dos backups?

**Requisito obrigatório:**

A perda da VM não poderá implicar perda definitiva de todos os dados do grupo.

## Q003 — Como versionar as imagens Docker?

**Status:** DECIDIDA — sem registry: `run.bat --ssh` constrói a imagem localmente, extrai o binário e o envia ao Termux (`decisoes/0029-implantacao-nativa-no-termux.md`, `decisoes/0032-a14-como-producao.md`).

Investigar:

- Build Native AOT para `linux-arm64`.
- Imagens multi-stage.
- Imagens mínimas compatíveis com aplicações AOT.
- Estratégia de atualização e rollback.

**Recomendação:**

Construir a imagem em CI, publicar em um registry e realizar pull na VM.

Não instalar SDK .NET ou Node.js no servidor de produção.

**Situação atual:** o projeto ainda não tem CI, e a imagem é construída localmente com compilação cruzada. A distribuição para a VM fica para a Fase 8. Ver `decisoes/0003-sem-ci-inicialmente.md`.

---

# 3. Backend e Native AOT

## Q004 — Qual estratégia de autenticação será utilizada?

**Status:** DECIDIDA — ver `decisoes/0006-autenticacao-por-sessao.md` Alteração e redefinição de senha em `decisoes/0019-conta-e-redefinicao-de-senha.md`.

O Wrexercise utilizará sessões persistentes com cookies HttpOnly.

Investigar:

- Geração criptográfica de tokens.
- Hash de tokens persistidos.
- Expiração deslizante ou absoluta.
- Revogação de sessões.
- Recuperação de acesso.
- Proteção CSRF.
- Limitação de tentativas de autenticação.

**Decisões necessárias:**

- Qual algoritmo de hash de senha utilizar?
- Qual duração máxima da sessão?
- A sessão será renovada automaticamente?
- Como tratar múltiplos dispositivos?

**Restrição:**

A implementação deve ser compatível com Native AOT e não armazenar tokens no `localStorage`.

## Q005 — Como implementar migrações SQL?

**Status:** DECIDIDA — ver `decisoes/0001-migracoes-sql.md`

Investigar uma estratégia simples de migrações versionadas sem depender de ORM.

Alternativas:

- Scripts SQL numerados.
- Ferramenta externa de migração.
- Executor próprio de migrações.

Critérios:

- Compatibilidade com Native AOT.
- Execução explícita.
- Controle de versões aplicadas.
- Segurança em caso de interrupção.
- Facilidade de rollback ou correção.

**Recomendação inicial:**

Scripts SQL numerados e uma tabela `schema_migrations`.

## Q006 — Como organizar contratos JSON?

**Status:** ABERTA

Investigar:

- DTOs explícitos.
- Source generation do System.Text.Json.
- Naming policies.
- Tratamento de enums.
- Formatação de datas.
- Versionamento futuro da API.

**Recomendação:**

Utilizar contratos explícitos e serialização gerada em tempo de compilação.

Evitar serializar diretamente estruturas internas do banco.

## Q007 — Como tratar erros da API?

**Status:** PROPOSTA

Definir um formato consistente de erros.

Investigar o uso de `ProblemDetails` e códigos de erro específicos.

Exemplos:

- `NAO_AUTENTICADO`
- `ACESSO_NEGADO`
- `ATIVIDADE_NAO_ENCONTRADA`
- `ATIVIDADE_JA_REVISADA`
- `DESAFIO_ENCERRADO`
- `EVIDENCIA_INVALIDA`

Os erros devem ser compreensíveis pelo frontend e não revelar informações internas.

---

# 4. Domínio e desafios

## Q008 — Como determinar os limites das semanas?

**Status:** DECIDIDA — ver `decisoes/0009-desafios.md`

A meta será semanal, utilizando o fuso horário do grupo.

Investigar:

- Semana de segunda a domingo.
- Tratamento de horário de verão.
- Alterações de fuso horário.
- Atividades iniciadas perto da meia-noite.
- Desafios iniciados ou encerrados no meio da semana.

**Recomendação:**

Usar `DateTimeOffset` no backend e `timestamp with time zone` no PostgreSQL.

Persistir o fuso horário IANA do grupo, como `America/Sao_Paulo`.

Uma semana parcial no início ou fim do desafio deve ter comportamento explicitamente definido.

## Q009 — Como tratar atividades que atravessam semanas?

**Status:** DECIDIDA — ver `decisoes/0011-atividades.md`. A semana é a do horário de início (`started_at`) no fuso do grupo; a atividade não é dividida entre semanas.

Exemplo:

Um treino começa domingo às 23:30 e termina segunda-feira às 00:30.

Questões:

- A atividade pertence à semana em que começou?
- O horário de término deve determinar a semana?
- Como lidar com atividades excessivamente longas?

**Recomendação:**

Atribuir a atividade à semana correspondente ao seu horário de início no fuso do grupo.

Não dividir uma atividade entre semanas.

## Q010 — Como tratar desafios concorrentes?

**Status:** DECIDIDA — ver `decisoes/0009-desafios.md`. Vários desafios podem coexistir, e a meta não muda depois da criação. A elegibilidade de cada atividade será calculada na Fase 5. Revista em `decisoes/0016-um-desafio-por-vez.md`: um desafio por vez em cada grupo, sem sobreposição.

Uma atividade poderá contribuir para mais de um desafio ativo.

Investigar:

- Desafios simultâneos no mesmo grupo.
- Metas independentes.
- Requisitos diferentes de duração.
- Alterações de configuração.

**Recomendação:**

Manter a atividade independente do desafio e calcular sua elegibilidade separadamente para cada desafio.

## Q011 — O que acontece quando alguém entra ou sai de um grupo?

**Status:** DECIDIDA — ver `decisoes/0008-grupos-e-convites.md`. A participação em desafios está em `decisoes/0009-desafios.md`. O efeito sobre revisões e reações segue os princípios registrados na decisão 0008 e será detalhado na Fase 6. Participação revista em `decisoes/0016-um-desafio-por-vez.md`: automática para todo membro ativo.

Definir:

- Participação em desafios existentes.
- Acesso ao histórico anterior.
- Atividades pendentes de revisão.
- Reações de membros que saíram.
- Propriedade do grupo quando o criador sair.

**Recomendação:**

Preservar o histórico de atividades e revisões.

Não permitir que a saída de um membro invalide retroativamente uma atividade.

---

# 5. Verificação comunitária

## Q012 — Qual será a regra definitiva de consenso?

**Status:** DECIDIDA — ver `decisoes/0012-verificacao-comunitaria.md`. Reações em `EmRevisao` revistas em `decisoes/0015-reacoes-em-revisao.md`.

Reações disponíveis:

- Kudos.
- Suspeito.
- Rejeitar.

Cada membro elegível terá uma reação por atividade.

Investigar a melhor implementação para:

- Atualização de reação.
- Contagem concorrente.
- Encerramento da revisão.
- Mudança de reação após o prazo.
- Revisão administrativa.

**Regra proposta:**

```text
rejeicoes_necessarias =
    min(total_revisores,
        max(2, ceil(total_revisores / 2)))
```

Uma ou mais reações suspeitas colocam a atividade em revisão.

Atingir o limite de rejeições invalida a atividade.

Uma atividade com sinalização não resolvida não deve ser aprovada automaticamente.

## Q013 — Como tratar grupos muito pequenos?

**Status:** DECIDIDA — ver `decisoes/0012-verificacao-comunitaria.md` (0 revisores → verificada imediata; demais casos, fórmula de Q012).

Investigar o comportamento com:

- 2 integrantes.
- 3 integrantes.
- 4 integrantes.
- 10 integrantes.

Questões:

- O que acontece quando só existe um revisor?
- Uma única rejeição deve ser suficiente?
- O proprietário pode resolver sozinho uma disputa?
- É possível revisar a própria atividade por meio de outra conta?

**Recomendação:**

Não permitir a participação em desafios que dependam de consenso comunitário com menos de dois revisores elegíveis.

Para grupos maiores, utilizar a regra de rejeição definida em Q012.

## Q014 — Como será feita a revisão administrativa?

**Status:** DECIDIDA — ver `decisoes/0012-verificacao-comunitaria.md`.

Investigar:

- Quem pode resolver uma disputa.
- Como registrar justificativas.
- Se a decisão administrativa pode ser revertida.
- Como tratar conflitos de interesse.

**Recomendação:**

O proprietário ou administrador do grupo poderá resolver atividades em revisão.

Toda decisão deve gerar um registro imutável de auditoria.

## Q015 — Quando as reações serão encerradas?

**Status:** DECIDIDA — ver `decisoes/0012-verificacao-comunitaria.md`. Reações em `EmRevisao` revistas em `decisoes/0015-reacoes-em-revisao.md`.

Prazo inicial: 48 horas após submissão.

Investigar:

- Prazo fixo ou configurável.
- Comportamento quando ninguém reage.
- Comportamento com sinalizações pendentes.
- Alterações de reações.
- Atividades enviadas no final de um desafio.

**Recomendação:**

Não introduzir configurações complexas inicialmente.

Utilizar 48 horas e permitir resolução administrativa de disputas pendentes.

## Q016 — Como impedir abuso do sistema de reações?

**Status:** ABERTA

Investigar:

- Reações em massa.
- Rejeições coordenadas.
- Alterações repetidas.
- Contas removidas.
- Manipulação de horários.
- Reações de usuários inelegíveis.

**Recomendação:**

Utilizar restrições de banco, autorização no backend e auditoria das alterações.

Não implementar um sistema de reputação no MVP.

---

# 6. Atividades e evidências

## Q017 — Quais formatos de evidência serão aceitos?

**Status:** PROPOSTA

Métodos:

- Registro manual.
- Fotografia.
- Captura de tela de aplicativos externos.

Investigar:

- JPEG, PNG e WebP.
- Limite de tamanho.
- Dimensões máximas.
- Compressão.
- Validação de conteúdo.
- Remoção de metadados EXIF.

**Recomendação:**

Aceitar JPEG, PNG e WebP, com limite inicial de 5 MB por arquivo.

## Q018 — Como será feito o armazenamento?

**Status:** PROPOSTA

O MVP utilizará armazenamento local persistente na VM.

Investigar:

- Organização dos arquivos.
- Identificadores aleatórios.
- Acesso autorizado.
- Streaming.
- Exclusão.
- Backup.
- Recuperação.

**Recomendação:**

Nunca expor diretamente o diretório de evidências.

Toda consulta deve passar por uma operação autenticada da API.

## Q019 — Uma atividade poderá ter várias evidências?

**Status:** DECIDIDA — ver `decisoes/0011-atividades.md`. Até 3 evidências por atividade; um arquivo por requisição de upload.

Investigar:

- Uma ou várias imagens por atividade.
- Limite de imagens.
- Ordem de apresentação.
- Exclusão após envio.
- Substituição de evidências.

**Recomendação:**

Permitir até 3 evidências por atividade inicialmente.

Após o início da revisão, não permitir substituição silenciosa. Qualquer alteração deverá ser auditada.

## Q020 — Como evitar atividades duplicadas?

**Status:** ABERTA

Investigar:

- Duplicação manual.
- Mesmo screenshot enviado duas vezes.
- Atividades semelhantes.
- Reenvio por erro de conexão.

**Recomendação:**

Implementar chave de idempotência nas operações de criação.

Utilizar hash SHA-256 do arquivo como informação auxiliar para detectar possíveis duplicatas.

Não considerar hashes iguais como prova definitiva de fraude.

---

# 7. Frontend e PWA

## Q021 — Qual será a estratégia de estado?

**Status:** PROPOSTA

Investigar:

- TanStack Query.
- Estado local React.
- Context API.
- Persistência de cache.

**Recomendação:**

Utilizar TanStack Query para dados remotos e estado local React para interações.

Evitar Redux inicialmente.

## Q022 — Como implementar funcionamento offline?

**Status:** DECIDIDA — ver `decisoes/0021-pwa-e-service-worker.md`.

O MVP deverá permitir:

- Abrir o shell da aplicação.
- Navegar para telas já implementadas localmente.
- Exibir informações de indisponibilidade.
- Preservar a sessão para quando a conexão retornar.

Investigar:

- Estratégia de cache do Service Worker.
- Atualização de versões.
- Detecção de conectividade.
- Tratamento de requisições interrompidas.

**Restrição:**

Não implementar sincronização offline de atividades na primeira versão.

## Q023 — Como otimizar imagens no frontend?

**Status:** ABERTA

Investigar:

- Compressão no navegador.
- Redimensionamento.
- Conversão para WebP.
- Pré-visualização.
- Upload com progresso.
- Limites de memória em smartphones.

**Recomendação:**

Redimensionar antes do upload, sem exigir bibliotecas pesadas.

## Q024 — Como implementar atualização do PWA?

**Status:** DECIDIDA — ver `decisoes/0021-pwa-e-service-worker.md`. A navegação busca a rede primeiro, e a versão nova assume quando a pessoa toca em "Atualizar".

Investigar:

- Versionamento de assets.
- Cache invalidation.
- Atualização automática.
- Aviso de nova versão.
- Compatibilidade entre frontend e backend.

Evitar situações em que uma versão antiga do frontend faça chamadas incompatíveis com o backend atualizado.

---

# 8. Desempenho e eficiência

## Q025 — Qual será o orçamento de recursos?

**Status:** PROPOSTA

Objetivo inicial:

| Recurso | Limite de contêiner |
|---|---:|
| API | 384 MB |
| PostgreSQL | 768 MB |
| Caddy | 64 MB |

Investigar:

- RSS da aplicação após inicialização.
- Consumo do PostgreSQL.
- Consumo de memória em uploads.
- Latência de consultas.
- Tamanho dos bundles.
- Tempo de inicialização Native AOT.

A aplicação deverá ser testada em ARM64.

No Termux (0029), o app usa cerca de 25 MB de RSS ao iniciar e o `initdb` leva 1,6 s. Falta medir o consumo sob uso real e a latência das consultas do feed.

## Q026 — Quais consultas precisam de otimização?

**Status:** ABERTA

Investigar principalmente:

- Feed de atividades.
- Progresso semanal.
- Classificação.
- Reações por atividade.
- Histórico de desafios.

**Recomendação:**

Começar com SQL explícito e índices convencionais.

Não introduzir cache distribuído ou materializações complexas para dez usuários.

## Q027 — Como limitar requisições?

**Status:** ABERTA

Definir limites para:

- Login.
- Upload.
- Criação de atividades.
- Reações.
- Consultas ao feed.

Utilizar rate limiting simples no backend.

Não instalar serviços adicionais.

---

# 9. Segurança e privacidade

## Q028 — Como será o convite de novos membros?

**Status:** DECIDIDA — ver `decisoes/0008-grupos-e-convites.md` Revista em `decisoes/0017-convite-reutilizavel.md`: um link reutilizável por grupo.

Investigar:

- Tokens aleatórios.
- Validade.
- Uso único.
- Revogação.
- Limite de integrantes.
- Proteção contra enumeração.

**Recomendação:**

Convites privados, com token aleatório, uso único e validade limitada.

A participação deve exigir autenticação.

## Q029 — Qual será a política de retenção?

**Status:** ABERTA

Definir:

- Exclusão de conta.
- Exclusão de grupo.
- Exclusão de atividades.
- Retenção de evidências.
- Histórico de desafios.
- Dados presentes em backups.

A exclusão de dados deve considerar as relações históricas e as necessidades de auditoria.

## Q030 — Como tratar dados pessoais em imagens?

**Status:** ABERTA

Investigar:

- Metadados EXIF.
- Coordenadas GPS.
- Informações pessoais visíveis.
- URLs privadas.
- Acesso por usuários removidos.

**Recomendação:**

Remover metadados desnecessários durante o processamento de imagens e manter evidências privadas.

---

# 10. Interface e experiência

## Q031 — Qual será o design system inicial?

**Status:** PROPOSTA

Utilizar Apple Human Interface Guidelines como inspiração.

Investigar:

- Tipografia.
- Paleta de cores.
- Espaçamento.
- Superfícies.
- Estados de interação.
- Tema claro e escuro.
- Acessibilidade.

A interface deve ser moderna, leve e mobile-first.

Não utilizar dependências externas para carregar fontes ou recursos visuais.

**Situação atual:** tipografia e temas claro e escuro foram definidos a partir do mockup (`decisoes/0014-novo-mockup-como-referencia-visual.md`); a paleta, as fontes e a linguagem gráfica vêm da identidade Wrexercise (`decisoes/0027-identidade-visual-wrexercise.md`). Falta avaliar a necessidade de um seletor manual de tema.

## Q032 — Quais páginas serão obrigatórias no MVP?

**Status:** PROPOSTA

Páginas iniciais:

1. Entrar.
2. Início.
3. Grupo.
4. Desafio.
5. Registrar atividade.
6. Detalhes da atividade.
7. Perfil.

Evitar introduzir páginas administrativas complexas.

## Q033 — Como apresentar o progresso?

**Status:** PARCIALMENTE DECIDIDA — ver `decisoes/0013-competicao-e-progresso.md` Perfil, página do membro e conquistas decididos em `decisoes/0018-perfil-e-pagina-do-membro.md`.

Investigar formas simples de visualizar:

- Sessões concluídas.
- Sessões pendentes.
- Meta semanal.
- Sequência de semanas.
- Progresso do grupo.

A interface deve distinguir progresso confirmado de progresso provisório.

**Decidido para o MVP (Fase 7):** barras definitivo/provisório na Desafio e Início; classificação e sequência na tabela de ranking; histórico semanal por semana do desafio. A página Perfil, antes fora do escopo inicial, entrou com a decisão 0018.

## Q034 — Como será a folha de avatar em 8 direções?

**Status:** PROPOSTA — ver `decisoes/0039-avatar-em-8-direcoes.md`.

A folha atual é de lado (256×384, células de 32×32, espelhada para a esquerda). A proposta é trocar por personagens vistos de cima, em 8 direções, gerados no PixelLab.

Investigar:

- Tamanho da célula e limite de importação.
- Direções com ou sem espelho.
- Substituição das 6 folhas padrão.
- O que acontece com as folhas já importadas.
- Tamanho do avatar na cena.

---

# 11. Critérios para encerrar a pesquisa

Uma questão pode ser considerada concluída quando:

1. As alternativas relevantes foram identificadas.
2. Existe uma recomendação concreta.
3. O impacto sobre o modelo de dados foi avaliado.
4. O impacto sobre Native AOT foi avaliado quando aplicável.
5. A decisão foi documentada.
6. Os requisitos para implementação estão claros.

Questões sem impacto sobre o MVP podem permanecer adiadas.

## 12. Prioridade de investigação

### Prioridade alta — antes da implementação

- Q004: Autenticação.
- Q005: Migrações SQL.
- Q008: Limites das semanas.
- Q012: Consenso comunitário.
- Q013: Grupos pequenos.
- Q014: Revisão administrativa.
- Q015: Prazo de revisão.
- Q017: Formatos de evidência.
- Q018: Armazenamento.
- Q028: Convites privados.

### Prioridade média — durante a implementação

- Q006: Contratos JSON.
- Q007: Erros da API.
- Q009: Atividades entre semanas.
- Q011: Entrada e saída de membros.
- Q019: Múltiplas evidências.
- Q020: Duplicação.
- Q021: Estado do frontend.
- Q022: Funcionamento offline.
- Q023: Otimização de imagens.
- Q024: Atualização do PWA.
- Q025: Orçamento de recursos.
- Q026: Consultas SQL.
- Q030: Privacidade de imagens.
- Q034: Avatar em 8 direções.

### Prioridade baixa — após o MVP

- Q016: Abuso das reações.
- Q027: Rate limiting avançado.
- Q029: Política completa de retenção.
- Q031: Refinamento visual.
- Q032: Expansão de páginas.
- Q033: Visualizações adicionais.

---

## 13. Princípio de decisão

O Wrexercise deve continuar sendo uma aplicação pequena, privada e eficiente.

A existência de uma possibilidade técnica não é motivo suficiente para introduzi-la.

Cada decisão deve considerar:

- Complexidade adicionada.
- Custo de manutenção.
- Consumo de recursos.
- Benefício concreto para os participantes.
- Compatibilidade com a arquitetura existente.

Quando duas alternativas atendem aos requisitos, preferir a mais simples.