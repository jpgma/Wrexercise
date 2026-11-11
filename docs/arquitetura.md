# Arquitetura — Wrexercise

> Documento inicial de arquitetura e diretrizes de implementação.
>
> Idioma oficial do projeto: Português brasileiro (pt-BR).

## 1. Visão geral

### 1.1. Objetivo

Wrexercise é uma aplicação web privada para pequenos grupos de amigos que desejam manter uma rotina consistente de exercícios físicos por meio de desafios compartilhados, registro de atividades e validação comunitária.

O sistema deve permitir que um grupo estabeleça uma meta comum, como:

- Exercitar-se por pelo menos 60 minutos, 4 vezes por semana.
- Exercitar-se por pelo menos 30 minutos, 5 vezes por semana.

Cada participante registra suas atividades e apresenta uma comprovação compatível com o método escolhido. Os demais integrantes reagem às submissões usando três opções:

- 💪 Kudos: atividade considerada legítima.
- 🤨 Suspeito: atividade sinalizada para revisão.
- ❌ Rejeitar: atividade considerada inválida.

O resultado da validação é calculado a partir das reações da comunidade e determina quais atividades podem contribuir para o progresso do desafio.

### 1.2. Objetivos arquiteturais

Prioridades, em ordem:

1. Simplicidade de implementação e manutenção.
2. Baixo consumo de memória e CPU.
3. Baixo consumo de largura de banda.
4. Compatibilidade com .NET Native AOT.
5. Separação de responsabilidades por funcionalidade.
6. Facilidade para implementação incremental com agentes de programação.
7. Segurança adequada para uma aplicação privada.

### 1.3. Restrições

| Característica | Decisão |
|---|---|
| Usuários | Até 10 inicialmente |
| Visibilidade | Grupos exclusivamente privados |
| Backend | .NET 10 Native AOT |
| API | ASP.NET Core Minimal APIs |
| Banco de dados | PostgreSQL |
| Frontend | React + TypeScript |
| Estilização | Tailwind CSS |
| Aplicação | SPA / PWA |
| Hospedagem | Galaxy A14 com Termux, PostgreSQL e túnel da Cloudflare do `Environment` (0032, 0044) |
| Arquitetura | Monólito modular com VSA |
| Persistência | SQL explícito via Npgsql |
| Deploy | Docker Compose |
| Idioma | pt-BR |

Não implementar infraestrutura distribuída, escalabilidade horizontal ou abstrações de grande porte sem necessidade demonstrada.

---

## 2. Arquitetura de implantação

### 2.1. Infraestrutura

> **Atualização (0032, 0044):** a produção é um Galaxy A14 rodando nativo no Termux, em `wx.jpgmaraujo.com.br` (`docs/decisoes/0029-implantacao-nativa-no-termux.md`). O PostgreSQL 18 e o túnel da Cloudflare são do repositório `Environment`, compartilhados com a API do Movr (`docs/decisoes/0044-postgres-e-tunel-do-environment.md`). Esta seção e as §2.2 a §2.4 descrevem a pilha com Docker Compose e Caddy, que hoje só roda no teste local (`run.bat`). A VM Oracle foi abandonada.

O ambiente de produção seria composto por uma única VM Oracle Cloud Ampere A1.

Configuração inicial recomendada:

| Recurso | Configuração |
|---|---:|
| Arquitetura | ARM64 / AArch64 |
| CPU | 1 OCPU |
| Memória | 6 GB |
| Sistema operacional | Ubuntu Server ARM64 LTS |
| Contêineres | Docker Engine + Compose |
| Disco | Volume de inicialização persistente |

A configuração utiliza uma fração da franquia mensal Always Free e permite expansão para até 2 OCPUs e 12 GB de memória dentro da franquia aplicável.

### 2.2. Serviços

A implantação utilizará três contêineres:

1. `app`: API ASP.NET Core Native AOT e arquivos estáticos do frontend.
2. `db`: PostgreSQL.
3. `proxy`: Caddy, responsável por HTTPS e encaminhamento de requisições.

Não haverá contêiner dedicado ao frontend.

O React será compilado durante o processo de build e seus arquivos serão incorporados à imagem da aplicação.

### 2.3. Diagrama de implantação

```text
             Internet
                 |
                 v
           Caddy :443
          HTTPS / TLS
                 |
                 v
       ASP.NET Core Native AOT
              :8080
           /          \
          /            \
         v              v
   Arquivos SPA      API REST
    React/PWA            |
                         |
                         v
                     PostgreSQL
                       :5432

              Disco persistente
                /          \
               v            v
          Banco de dados  Comprovações
```

O PostgreSQL não deve expor sua porta para a internet.

Somente o proxy deverá publicar portas externas.

### 2.4. Limites iniciais de recursos

Os valores abaixo são limites de contêiner, não consumo garantido.

| Serviço | Memória máxima | CPU |
|---|---:|---:|
| Aplicação | 384 MB | 0,50 |
| PostgreSQL | 768 MB | 0,50 |
| Caddy | 64 MB | 0,10 |

A memória restante será reservada para sistema operacional, Docker, cache de arquivos e picos de utilização.

Os limites devem ser revisados após observar o consumo real.

O Galaxy A14 não usa estes limites: roda nativo no Termux (`docs/decisoes/0029-implantacao-nativa-no-termux.md`).

No mesmo aparelho, a pilha roda sem VM nem Docker, direto no Termux: o app por `run.bat --ssh`; o PostgreSQL e o `cloudflared`, compartilhados, pelo repositório `Environment`. Ver `docs/decisoes/0029-implantacao-nativa-no-termux.md` e `docs/decisoes/0044-postgres-e-tunel-do-environment.md`.

### 2.5. Configuração inicial do PostgreSQL

O banco será configurado para um ambiente pequeno, com poucas conexões simultâneas.

Parâmetros iniciais:

```conf
max_connections = 20
shared_buffers = 128MB
effective_cache_size = 2GB
work_mem = 4MB
maintenance_work_mem = 64MB
max_wal_size = 512MB
checkpoint_completion_target = 0.9
```

Observações:

- `effective_cache_size` é uma estimativa para o planejador, não uma reserva de memória.
- `work_mem` pode ser consumido por múltiplas operações simultâneas.
- Não aumentar `max_connections` sem necessidade.
- Não utilizar PgBouncer inicialmente.
- Utilizar uma única instância PostgreSQL.

O banco deve possuir volume persistente independente do ciclo de vida do contêiner.

---

## 3. Backend

### 3.1. Tecnologias

| Tecnologia | Utilização |
|---|---|
| .NET 10 | Plataforma |
| ASP.NET Core | Servidor HTTP |
| Native AOT | Compilação nativa |
| Minimal APIs | Endpoints |
| Npgsql | Acesso PostgreSQL |
| System.Text.Json | Serialização |
| System.Text.Json Source Generator | Compatibilidade AOT |
| PostgreSQL | Persistência |

### 3.2. Princípios

O backend será um monólito modular organizado por funcionalidades verticais (Vertical Slice Architecture).

Cada funcionalidade terá responsabilidade sobre:

- Seus endpoints.
- Seus contratos HTTP.
- Suas consultas SQL.
- Suas operações de escrita.
- Suas regras específicas.
- Seus testes.

Não haverá uma camada global de repositórios genéricos.

Não haverá uma camada de serviços que simplesmente encapsule chamadas de banco de dados.

O código deve favorecer operações explícitas sobre estruturas de dados simples.

### 3.3. Compatibilidade Native AOT

O projeto deve permanecer compatível com Native AOT durante todo o desenvolvimento.

Requisitos:

- `PublishAot` habilitado.
- Preferir `WebApplication.CreateSlimBuilder`.
- Utilizar Minimal APIs.
- Utilizar serialização JSON gerada em tempo de compilação.
- Evitar reflexão dinâmica.
- Evitar carregamento dinâmico de assemblies.
- Evitar bibliotecas incompatíveis com trimming.
- Tratar todos os avisos de AOT como problemas de implementação.

Configuração inicial:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

A publicação de produção deverá utilizar:

```bash
dotnet publish \
  -c Release \
  -r linux-arm64 \
  -p:PublishAot=true
```

A compilação final deverá acontecer em um ambiente Linux ARM64 compatível. O servidor de produção não deve ser utilizado como ambiente de compilação.

A configuração de globalização invariável reduz dependências e consumo de recursos. A formatação de datas, números e mensagens destinadas ao usuário deverá ser explícita no frontend ou utilizar formatação compatível com essa configuração.

### 3.4. Acesso ao banco

Utilizar Npgsql diretamente.

Exemplo conceitual:

```csharp
await using var command = dataSource.CreateCommand("""
    SELECT id, nome, duracao_segundos
    FROM atividades
    WHERE grupo_id = $1
    ORDER BY started_at DESC
    LIMIT $2
    """);

command.Parameters.AddWithValue(groupId);
command.Parameters.AddWithValue(limit);

await using var reader =
    await command.ExecuteReaderAsync(cancellationToken);
```

Diretrizes:

- Utilizar `NpgsqlDataSource` como recurso compartilhado da aplicação.
- Reutilizar conexões por meio do pool do Npgsql.
- Preferir consultas parametrizadas.
- Evitar SQL dinâmico com valores fornecidos pelo usuário.
- Utilizar transações explícitas quando uma operação envolver múltiplas escritas.
- Projetar apenas as colunas necessárias.
- Utilizar paginação por cursor em feeds.
- Evitar carregar entidades completas quando uma projeção simples for suficiente.

Não utilizar Entity Framework Core ou Dapper inicialmente.

O acesso direto ao banco mantém o caminho de execução explícito e evita dependências adicionais de reflexão e materialização dinâmica.

### 3.5. Organização de responsabilidades

Cada endpoint deverá seguir um fluxo pequeno:

```text
HTTP Request
     |
     v
Autenticação
     |
     v
Validação do contrato
     |
     v
Regra de negócio
     |
     v
Consulta / Transação SQL
     |
     v
HTTP Response
```

As regras de negócio puras devem ser separadas das operações de I/O.
A fatia se organiza por operação HTTP (ver `docs/decisoes/0010-fatia-por-operacao.md`): um arquivo concentra o fluxo da rota; tipos e helpers reutilizados ficam em arquivos nomeados pelo conceito.

Exemplo:

```text
Wrexercise/Features/Autenticacao/
├── Routes.cs              # MapAutenticacaoRoutes(): ligação fina
├── Registrar.cs           # handler, contratos, SQL e regras da operação
├── Entrar.cs
├── Respostas.cs           # DTOs compartilhados na fatia
├── Sessoes.cs             # helpers reutilizados
└── ValidacaoConta.cs      # regras puras compartilhadas

Wrexercise.UnitTests/Features/Autenticacao/
└── ValidacaoContaTests.cs
```

Os testes ficam em `Wrexercise.UnitTests/` e `Wrexercise.IntegrationTests/`, espelhando o caminho da fatia, para que código e pacotes de teste não entrem no binário Native AOT.

Não é obrigatório criar todos esses arquivos quando uma funcionalidade for pequena.
Fatias triviais podem ficar só com `Routes.cs`.

A organização deve refletir responsabilidades reais (coesão por operação), não uma quantidade fixa de classes por camada.

---

## 4. Estrutura do projeto

Estrutura proposta:

```text
wrexercise/
│
├── AGENTS.md                  # regras do repositório
├── Wrexercise.slnx
│
├── Wrexercise/                # aplicação: API e frontend
│   ├── Program.cs
│   ├── Wrexercise.csproj
│   │
│   ├── Infrastructure/
│   │   ├── Database/
│   │   │   ├── DataSourceFactory.cs
│   │   │   └── Migrations/
│   │   ├── Errors/
│   │   ├── Json/
│   │   ├── Authentication/
│   │   └── Files/
│   │
│   ├── Features/
│   │   ├── Health/
│   │   ├── Autenticacao/
│   │   ├── Usuarios/
│   │   ├── Grupos/
│   │   ├── Convites/
│   │   ├── Desafios/
│   │   ├── Atividades/
│   │   ├── Reacoes/
│   │   ├── Progresso/
│   │   └── Feed/
│   │
│   ├── webapp/                # código-fonte do frontend
│   │   ├── public/
│   │   ├── src/
│   │   │   ├── components/
│   │   │   ├── features/
│   │   │   │   ├── autenticacao/
│   │   │   │   ├── grupos/
│   │   │   │   ├── desafios/
│   │   │   │   ├── atividades/
│   │   │   │   └── feed/
│   │   │   ├── pages/
│   │   │   ├── navigation/
│   │   │   ├── services/
│   │   │   ├── styles/
│   │   │   └── main.tsx
│   │   └── package.json
│   │
│   └── wwwroot/               # gerado a partir de webapp/, fora do git
│
├── Wrexercise.UnitTests/
├── Wrexercise.IntegrationTests/
│
├── deploy/
│   ├── compose.yaml
│   ├── compose.dev.yaml
│   ├── Caddyfile
│   └── Dockerfile
│
├── scripts/
│   ├── verify.sh
│   ├── dev-db.sh
│   └── test-db.sh
│
├── docs/
│   ├── arquitetura.md
│   ├── questoes-abertas.md
│   ├── modelo-dados.md
│   └── decisoes/
│
├── .env.example
├── .gitignore
└── README.md
```

A estrutura é descrita em `docs/decisoes/0004-estrutura-do-repositorio.md`.

As pastas de funcionalidades são criadas na fase correspondente (§18), não antecipadamente. A convenção de nomes está em `docs/decisoes/0002-convencao-de-nomes.md`.

### 4.1. Diretrizes de organização

`Infrastructure` contém implementações compartilhadas de recursos externos.

`Features` contém a lógica específica de cada domínio funcional.

Não criar um projeto de domínio, aplicação, infraestrutura e API separados apenas para reproduzir uma arquitetura em camadas.

A separação deve acontecer por responsabilidade lógica dentro do mesmo executável.

As funcionalidades podem compartilhar infraestrutura, mas não devem acessar diretamente os detalhes internos umas das outras.

Quando uma funcionalidade precisar de dados de outra, preferir uma consulta explícita ao banco ou um contrato pequeno e estável.

---

## 5. Frontend

### 5.1. Tecnologias

| Tecnologia | Utilização |
|---|---|
| React | Interface |
| TypeScript | Tipagem |
| Vite | Build |
| Tailwind CSS | Estilização |
| React Router | Navegação |
| TanStack Query | Estado remoto |
| React Hook Form | Formulários |
| Zod | Validação |

O frontend será uma SPA compilada e servida pelo próprio backend.

Não utilizar SSR, Next.js ou servidor Node.js em produção.

Node.js será necessário apenas durante o desenvolvimento e compilação.

### 5.2. Direção visual

A aparência e os fluxos das telas seguem o mockup do projeto, conforme `docs/decisoes/0014-novo-mockup-como-referencia-visual.md`. Em regras, nomes e segurança, este documento prevalece.

A interface deverá utilizar o Apple Human Interface Guidelines como referência estética e de usabilidade, sem reproduzir literalmente componentes proprietários.

Características desejadas:

- Tipografia moderna e legível.
- Hierarquia visual clara.
- Espaçamento generoso.
- Cantos arredondados.
- Superfícies discretas.
- Bordas sutis.
- Ícones simples e consistentes.
- Animações curtas e discretas.
- Feedback visual imediato.
- Navegação confortável em dispositivos móveis.

A interface deve priorizar o uso em smartphones.

Utilizar CSS e componentes próprios sempre que isso evitar dependências desnecessárias.

Não carregar fontes, ícones ou bibliotecas de terceiros remotamente.

### 5.3. Páginas principais

#### Autenticação

- Login.
- Cadastro por convite.
- Recuperação de acesso, caso implementada.

#### Início

- Desafio ativo.
- Progresso semanal pessoal.
- Resumo do grupo.
- Últimas atividades.
- Atalho para registrar atividade.

#### Grupo

- Membros.
- Convites.
- Feed.
- Configurações do grupo.

#### Desafio

- Meta semanal.
- Período.
- Progresso individual.
- Histórico semanal.
- Classificação.

#### Atividade

- Detalhes da submissão.
- Comprovação.
- Reações.
- Comentários.

#### Perfil

- Histórico pessoal.
- Sequência de semanas cumpridas.
- Preferências.

### 5.4. Desempenho e largura de banda

O frontend deverá ser desenvolvido para conexões móveis lentas.

Diretrizes:

- Produzir bundles pequenos.
- Dividir código por página.
- Carregar telas sob demanda.
- Evitar bibliotecas de interface pesadas.
- Evitar imagens decorativas grandes.
- Utilizar imagens WebP ou JPEG comprimidas.
- Carregar evidências somente quando solicitadas. O feed carrega apenas miniaturas geradas no navegador, sob demanda.
- Utilizar paginação.
- Utilizar skeletons discretos durante carregamento.
- Evitar atualizações completas de página.
- Evitar polling frequente.

O feed deverá carregar inicialmente poucos registros, com paginação por cursor.

---

## 6. PWA e autenticação persistente

### 6.1. Instalação

A aplicação deverá ser instalável como PWA em Android e navegadores compatíveis.

Implementar:

- `manifest.webmanifest`.
- Ícones em tamanhos adequados.
- Service Worker.
- Cache do shell da aplicação.
- Tela de indisponibilidade de conexão.
- Estratégia de atualização do aplicativo.

Implementação em `decisoes/0021-pwa-e-service-worker.md`.

### 6.2. Persistência de login

O login deverá permanecer ativo após fechar e reabrir o aplicativo.

Não armazenar senhas, tokens de autenticação ou credenciais em `localStorage`.

Utilizar uma sessão opaca persistida em cookie:

```text
Set-Cookie:
  wx_session=<token>;
  HttpOnly;
  Secure;
  SameSite=Lax;
  Path=/;
  Max-Age=2592000
```

O token deve possuir entropia criptográfica suficiente, ser gerado com um gerador seguro e nunca ser armazenado em texto puro no banco.

Persistir somente seu hash.

A tabela de sessões deverá permitir:

- Expiração.
- Revogação.
- Encerramento de sessão.
- Identificação do usuário associado.

A sessão terá duração inicial de 30 dias.

A autenticação deverá ser implementada com mecanismos compatíveis com Native AOT, evitando dependências de autenticação baseadas em reflexão.

### 6.3. Segurança do Service Worker

O Service Worker poderá armazenar:

- HTML da aplicação.
- CSS.
- JavaScript.
- Ícones.
- Recursos estáticos.

Não deverá armazenar respostas autenticadas da API ou imagens privadas no cache compartilhado do navegador, inclusive as miniaturas de evidências.

As operações de escrita não serão enfileiradas offline na primeira versão.

O usuário poderá abrir a interface offline, mas operações que exigem comunicação com o servidor deverão informar claramente a indisponibilidade.

---

## 7. Modelo de domínio

### 7.1. Usuário

Representa uma conta individual.

Atributos principais:

- Identificador.
- Nome de exibição.
- E-mail.
- Hash da senha.
- Data de criação.

### 7.2. Grupo

Representa um espaço privado de convivência.

Atributos principais:

- Identificador.
- Nome.
- Criador.
- Fuso horário.
- Data de criação.

Cada grupo possui membros e desafios.

### 7.3. Membro do grupo

Representa o vínculo entre usuário e grupo.

Papéis:

- Proprietário.
- Administrador.
- Membro.

O grupo deve possuir no máximo 10 integrantes inicialmente.

### 7.4. Desafio

Define uma meta compartilhada pelo grupo.

Atributos:

- Identificador.
- Grupo.
- Nome.
- Descrição.
- Data inicial.
- Data final.
- Duração mínima por dia, em segundos. As atividades de um dia se somam.
- Quantidade mínima de dias por semana que alcançam a duração mínima.
- Situação, calculada a partir das datas e do cancelamento.

O desafio segue o fuso horário do grupo e cobre apenas semanas inteiras, de segunda a domingo. Ver `docs/decisoes/0009-desafios.md`.

Exemplo:

```text
Nome: Consistência de novembro

Duração mínima: 3600 segundos
Sessões semanais: 4

Meta individual:
4 dias com pelo menos 60 minutos
de atividade por semana.
```

Todos os participantes seguem a mesma meta. Todo membro ativo do grupo participa, sem adesão nem saída, e quem entra depois do início conta a partir da primeira semana inteira. Um grupo tem um desafio por vez, sem sobreposição de datas. Ver `decisoes/0016-um-desafio-por-vez.md`.

Não implementar metas individuais diferentes na primeira versão.

### 7.5. Atividade

Representa uma sessão de exercício registrada por um participante.

Atributos:

- Identificador.
- Usuário.
- Grupo.
- Tipo de atividade.
- Duração em segundos.
- Data/hora de início.
- Descrição.
- Tipo de comprovação.
- Data de envio.

Tipos de atividade:

- Musculação.
- Corrida.
- Caminhada.
- Ciclismo.
- Natação.
- Esporte.
- Yoga.
- Calistenia.
- Outro.

Tipos de comprovação:

- Registro manual.
- Fotografia.
- Captura de tela.

O método de comprovação deve ser compatível com as configurações do grupo.

### 7.6. Reação

Representa a avaliação comunitária de uma atividade.

Tipos:

```text
Kudos
Suspeito
Rejeitar
```

Cada integrante elegível poderá manter apenas uma reação por atividade.

A reação poderá ser alterada até o encerramento da janela de revisão.

### 7.7. Sessão de autenticação

Representa uma sessão persistente de um usuário.

Armazena:

- Hash do token.
- Identificador do usuário.
- Data de criação.
- Data de expiração.
- Data de revogação.

---

## 8. Regras de negócio

### 8.1. Metas semanais

Cada desafio possui:

- Duração mínima por dia.
- Quantidade mínima de dias por semana (sessões).

As atividades de um mesmo dia, no fuso do grupo, se somam. Um dia cuja soma alcança a duração mínima conta como uma sessão; também conta o dia em que a soma dos tipos de uma meta por tipo alcança o mínimo dela. Ver `decisoes/0040-meta-diaria.md` e `decisoes/0043-metas-por-tipo.md`.

Exemplo:

```text
Meta: 4 dias de 60 minutos.

Segunda: 30 + 30 minutos -> 1 sessão.
Terça:   30 minutos      -> Não conta.
Quarta:  90 minutos      -> 1 sessão.
Quinta:  60 + 60 minutos -> 1 sessão.
Sábado:  60 minutos      -> 1 sessão.

Resultado: 4 de 4 sessões.
```

Um dia nunca representa mais de uma sessão, por mais longo que seja.

Atividades adicionais podem ser registradas, mas não compensam semanas anteriores.

### 8.2. Cálculo semanal

As semanas devem seguir o fuso horário configurado no grupo.

Regra fixa para todo o aplicativo (desafios, progresso, perfil e Jogo):

- Segunda-feira como primeiro dia.
- Domingo como último dia.

Os registros devem utilizar `timestamp with time zone`.

A conversão para o período semanal deve considerar o fuso horário do grupo.

Uma atividade poderá contribuir para mais de um desafio ativo do mesmo grupo, desde que cumpra os requisitos de cada desafio.

### 8.3. Verificação comunitária

Cada atividade terá uma lista de revisores elegíveis, definida no momento da submissão.

O autor não poderá reagir à própria atividade.

A lista de revisores deverá ser preservada mesmo que a composição do grupo mude posteriormente.

Cada revisor poderá registrar uma reação.

A contagem de rejeições necessária será:

```text
mínimo_de_rejeições =
  min(total_de_revisores,
      max(2, ceil(total_de_revisores / 2)))
```

### 8.4. Situações de verificação

```text
AguardandoFeedback
Verificada
EmRevisao
Contestada
Rejeitada
```

Regras:

- Todas as reações positivas: Verificada.
- Uma ou mais reações suspeitas: EmRevisao.
- Atingiu o limite de rejeições: Rejeitada.
- Nenhuma reação negativa e prazo encerrado: Verificada.
- Suspeita ou rejeição abaixo do limite: revisão manual pelo administrador do grupo, a qualquer momento.
- As reações continuam abertas em EmRevisao até o fim do prazo e podem ser trocadas, sem remoção. A situação é recalculada a cada reação: atingir o limite leva a Rejeitada, e retirar a única reação negativa devolve a atividade a AguardandoFeedback (ou a Verificada, se todos os revisores já tiverem reagido).
- Verificada e Rejeitada são finais. Depois do prazo, EmRevisao só muda por revisão manual. Ver `decisoes/0015-reacoes-em-revisao.md`.

A revisão manual deve registrar responsável, data e justificativa.

Prazo inicial para reações: 48 horas.

O sistema não deverá considerar uma atividade como verificada simplesmente porque não recebeu nenhuma reação dentro do prazo se existir uma reação suspeita ou rejeição pendente de resolução.

### 8.5. Progresso

Somente atividades verificadas contribuem para o progresso definitivo.

Atividades pendentes podem aparecer separadamente como progresso provisório.

O progresso semanal será calculado pela quantidade de dias em que a soma das atividades verificadas alcança o mínimo. Um dia que só alcança o mínimo com atividades pendentes conta no progresso provisório.

Exemplo:

```text
Meta semanal: 4 sessões.

Verificadas: 2
Pendentes: 1
Rejeitadas: 1

Progresso definitivo: 2/4
Progresso provisório: 3/4
```

A classificação inicial deverá priorizar:

1. Semanas com meta cumprida.
2. Total de dias verificados (sessões).
3. Sequência de semanas cumpridas.

Evitar sistemas artificiais de pontuação no MVP.

Conquistas são apenas reconhecimento: calculadas na leitura, visíveis só no próprio Perfil e sem efeito na classificação. A página de um membro mostra apenas dados daquele grupo. Ver `decisoes/0018-perfil-e-pagina-do-membro.md`.

---

## 9. Persistência

### 9.1. Tabelas principais

```text
usuarios
sessoes_autenticacao

grupos
membros_grupo
convites_grupo

desafios

atividades
evidencias_atividade
revisores_atividade
reacoes_atividade
historico_revisao
```

### 9.2. Restrições importantes

- E-mail único por usuário.
- Um vínculo por usuário e grupo.
- Um convite por token, e um link ativo por grupo.
- Um desafio por período em cada grupo, sem sobreposição de datas (exceto os cancelados).
- Uma reação por usuário e atividade.
- Um revisor por usuário e atividade.
- Duração de atividade positiva.
- Fim do desafio posterior ao início.
- Integridade referencial entre grupo, desafio e atividade.

Utilizar UUID como identificador das entidades principais.

Índices devem ser criados para consultas frequentes, especialmente:

- Atividades por grupo e data.
- Atividades por usuário e data.
- Atividades por status de verificação.
- Reações por atividade.

### 9.3. Migrações

Utilizar scripts SQL versionados.

Exemplo:

```text
Infrastructure/Database/Migrations/
├── 001-create-usuarios.sql
├── 002-create-grupos.sql
├── 003-create-desafios.sql
├── 004-create-atividades.sql
├── 005-create-reacoes.sql
└── 006-create-sessoes.sql
```

O executor é descrito em `docs/decisoes/0001-migracoes-sql.md`.

Não executar alterações destrutivas automaticamente na inicialização da aplicação.

As migrações devem possuir controle de versão e ser aplicadas explicitamente durante o deploy.

---

## 10. API

A API será REST e utilizará JSON.

Todos os endpoints privados deverão verificar autenticação e autorização de acesso ao grupo correspondente.

### 10.1. Autenticação

```http
POST /api/autenticacao/entrar
POST /api/autenticacao/sair
GET  /api/autenticacao/sessao
POST /api/autenticacao/registrar
POST /api/autenticacao/redefinir-senha
PUT  /api/usuarios/eu
POST /api/usuarios/eu/senha
```

### 10.2. Grupos

```http
GET  /api/grupos
POST /api/grupos
GET  /api/grupos/{id}
GET  /api/grupos/{id}/resumo
GET  /api/grupos/{id}/membros
PUT  /api/grupos/{id}/membros/{usuarioId}/papel
DELETE /api/grupos/{id}/membros/{usuarioId}
POST /api/grupos/{id}/sair
POST /api/grupos/{id}/transferir

GET  /api/grupos/{id}/convite
POST /api/grupos/{id}/convite
DELETE /api/grupos/{id}/convite
POST /api/convites/consultar
POST /api/grupos/entrar
```

O token do convite nunca faz parte do caminho, por isso a consulta usa `POST`. Permissões e regras em `docs/decisoes/0008-grupos-e-convites.md` e `docs/decisoes/0017-convite-reutilizavel.md`.

### 10.3. Desafios

```http
GET  /api/grupos/{id}/desafios
POST /api/grupos/{id}/desafios
GET  /api/desafios/{id}
GET  /api/desafios/{id}/progresso
GET  /api/desafios/{id}/classificacao
GET  /api/desafios/{id}/historico
PUT  /api/desafios/{id}
POST /api/desafios/{id}/cancelar
```

Permissões e regras em `docs/decisoes/0009-desafios.md` e `docs/decisoes/0016-um-desafio-por-vez.md`. Classificação, histórico e critérios de ranking em `docs/decisoes/0013-competicao-e-progresso.md`.

### 10.4. Atividades

```http
GET  /api/grupos/{id}/atividades
POST /api/grupos/{id}/atividades
GET  /api/grupos/{id}/atividades/para-avaliar
GET  /api/atividades/recentes
GET  /api/atividades/{id}
POST /api/atividades/{id}/evidencias
GET  /api/atividades/{id}/evidencias/{evidenciaId}
GET  /api/atividades/{id}/evidencias/{evidenciaId}/miniatura
```

A lista do grupo aceita `?filtro=Minhas|Pendentes|Verificadas|EmRevisao` e `?autorId=`, aplicados no servidor para não quebrar a paginação por cursor. `para-avaliar` devolve o que ainda espera a reação de quem consulta, dentro da janela de 48 horas. Enquanto essa fila não estiver vazia, o `POST` recusa com `409 AVALIACOES_PENDENTES` (`docs/decisoes/0041-avaliar-antes-de-registrar.md`); as duas rotas usam o mesmo filtro SQL (`ConsultasAtividade.FiltroParaAvaliar`). A criação usa somente JSON; o upload de evidências usa `multipart/form-data` em rota separada. Permissões e regras em `docs/decisoes/0011-atividades.md`.

### 10.4.1. Usuários

```http
GET  /api/usuarios/eu/perfil
GET  /api/grupos/{id}/membros/{usuarioId}/perfil
```

Regras em `docs/decisoes/0018-perfil-e-pagina-do-membro.md`.

### 10.4.2. Notificações

```http
GET  /api/notificacoes
GET  /api/notificacoes/nao-lidas
POST /api/notificacoes/lidas
POST /api/notificacoes/{id}/lida
```

Regras em `docs/decisoes/0020-notificacoes-no-aplicativo.md`.

### 10.5. Reações

```http
PUT  /api/atividades/{id}/reacao
GET  /api/atividades/{id}/reacoes
POST /api/atividades/{id}/revisao
GET  /api/atividades/{id}/comentarios
POST /api/atividades/{id}/comentarios
```

A operação de reação deverá realizar inserção ou atualização da reação do usuário.

Não permitir que o autor reaja à própria atividade.

Permissões e regras em `docs/decisoes/0012-verificacao-comunitaria.md`.

---

## 11. Evidências e arquivos

As evidências serão armazenadas no disco persistente da VM.

Não armazenar imagens diretamente no PostgreSQL.

Estrutura sugerida:

```text
/dados/
├── postgres/
├── avatares/        # folhas de avatar importadas (nome aleatório .png); limpeza diária dos arquivos sem dono
└── evidencias/
    ├── 2026/
    │   ├── 11/
    │   └── 12/
```

Requisitos:

- Limite inicial de 5 MB por arquivo.
- Validar extensão e conteúdo real.
- Aceitar JPEG, PNG e WebP.
- Gerar nomes aleatórios.
- Não utilizar nomes enviados pelo cliente como caminhos.
- Não expor diretórios de armazenamento diretamente.
- Exigir autorização para consultar evidências.
- Implementar exclusão de arquivos associados a atividades removidas, respeitando as regras de retenção.

As imagens deverão ser redimensionadas e comprimidas no cliente quando possível.

A aplicação deverá utilizar streaming para leitura e gravação dos arquivos, evitando manter arquivos grandes integralmente em memória.

A interface deverá carregar miniaturas e imagens sob demanda.

---

## 12. Segurança

### 12.1. Autenticação

Implementar autenticação por sessão opaca.

- Senhas armazenadas com hash seguro.
- Preferir PBKDF2-HMAC-SHA256 com parâmetros atualizados.
- Nunca armazenar senhas em texto puro.
- Tokens de sessão gerados criptograficamente.
- Hash dos tokens persistido no banco.
- Cookies HttpOnly e Secure.
- Expiração e revogação de sessões.

### 12.2. Autorização

A autorização deverá ser validada no backend.

Não confiar em identificadores de grupo enviados pelo frontend.

Toda consulta ou operação deverá verificar se o usuário pertence ao grupo e possui permissão suficiente.

Não permitir acesso a evidências de grupos diferentes.

### 12.3. Proteção da API

- HTTPS obrigatório em produção.
- Limitação de tentativas de login.
- Limites de tamanho de requisição.
- Validação de entrada.
- Consultas parametrizadas.
- Proteção contra enumeração de usuários.
- CORS desnecessário deve permanecer desabilitado.
- Não expor mensagens internas de exceção.
- Segredos exclusivamente por configuração externa.

Como a autenticação utiliza cookies, implementar proteção contra CSRF nas operações de escrita, além de validação de origem e política SameSite adequada.

---

## 13. Frontend e comunicação

O frontend será servido pela mesma origem da API.

Exemplo:

```text
https://wrexercise.example.com/
https://wrexercise.example.com/api/...
```

Isso simplifica autenticação, cookies e segurança.

Não implementar WebSockets inicialmente.

Utilizar atualizações sob demanda e revalidação moderada de consultas.

A interface deve funcionar bem mesmo quando o usuário retorna após longos períodos sem abrir o aplicativo.

---

## 14. Observabilidade

A aplicação deverá utilizar logs estruturados em stdout.

Não instalar inicialmente:

- Prometheus.
- Grafana.
- Elasticsearch.
- Jaeger.
- OpenTelemetry Collector.

Implementar:

- Logs de inicialização.
- Logs de erros.
- Logs de autenticação relevantes.
- Logs de falhas de persistência.
- Endpoint de saúde.
- Endpoint de prontidão do banco.

Endpoints:

```http
GET /health
GET /ready
```

Nunca registrar senhas, cookies, tokens, conteúdo privado de evidências ou informações sensíveis.

---

## 15. Backup e recuperação

Os dados persistentes deverão sobreviver à recriação dos contêineres.

Volumes:

```text
/srv/wrexercise/postgres
/srv/wrexercise/evidencias
/srv/wrexercise/avatares
```

Implementar:

- Backup diário do PostgreSQL com `pg_dump`.
- Retenção local de pelo menos 7 dias.
- Backup periódico das evidências.
- Cópia externa dos backups.
- Teste periódico de restauração.

Um backup mantido somente na mesma VM não é suficiente contra perda do volume ou da instância.

A restauração deve ser documentada e testada.

Implementação em `decisoes/0022-backups.md`: `deploy/backup.sh`, com destino em `BACKUP_DIR` na pilha local e em `~/wrexercise/dados/backups` no telefone. `run.bat ssh-backup` copia os backups do telefone para o Windows (`decisoes/0032-a14-como-producao.md`).

---

## 16. Docker Compose

A produção utilizará um único arquivo `compose.yaml`.

Serviços:

```yaml
services:
  app:
    image: wrexercise:latest
    restart: unless-stopped
    environment:
      ConnectionStrings__Database: ${DB_CONNECTION}
    depends_on:
      db:
        condition: service_healthy
    volumes:
      - /srv/wrexercise/evidencias:/dados/evidencias
      - /srv/wrexercise/avatares:/dados/avatares
    mem_limit: 384m
    cpus: 0.50

  db:
    image: postgres:18.6-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: wrexercise
      POSTGRES_USER: wrexercise
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - /srv/wrexercise/postgres:/var/lib/postgresql
    command:
      - postgres
      - -c
      - shared_buffers=128MB
      - -c
      - max_connections=20
      - -c
      - work_mem=4MB
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -h 127.0.0.1 -U wrexercise -d wrexercise"]
      interval: 10s
      timeout: 5s
      retries: 5
    mem_limit: 768m
    cpus: 0.50

  proxy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    depends_on:
      - app
    mem_limit: 64m
    cpus: 0.10

volumes:
  caddy_data:
  caddy_config:
```

As versões das imagens deverão ser fixadas em versões ou digests testados antes da implantação.

O exemplo é uma configuração inicial. O arquivo em uso é `deploy/compose.yaml`.

A senha do banco e os demais segredos deverão estar fora do repositório.

O banco não deverá publicar portas externas.

---

## 17. Testes

### 17.1. Backend

Testes unitários para:

- Cálculo de metas semanais.
- Elegibilidade de atividades.
- Cálculo de progresso.
- Regras de reação.
- Limite de rejeições.
- Transições de verificação.
- Expiração de sessões.

Testes de integração para:

- Persistência PostgreSQL.
- Autenticação.
- Autorização entre grupos.
- Submissão de atividades.
- Upload de evidências.
- Reações simultâneas.
- Concorrência em atualizações de reações.

### 17.2. Frontend

Testes para:

- Login persistente.
- Navegação.
- Registro de atividades.
- Envio de evidências.
- Exibição de reações.
- Atualização de progresso.
- Estados offline.

### 17.3. Native AOT

A compilação Native AOT deverá ser executada regularmente durante o desenvolvimento.

Não aceitar dependências que introduzam avisos de compatibilidade sem justificativa e solução documentada.

---

## 18. Estratégia de implementação

Desenvolver em fatias verticais funcionais.

### Fase 1 — Fundação

- Criar solução .NET 10 Native AOT.
- Configurar Npgsql.
- Criar migrações SQL.
- Criar Dockerfile ARM64.
- Criar Compose.
- Implementar health checks.
- Criar frontend React.

### Fase 2 — Autenticação

- Cadastro.
- Login.
- Sessões persistentes.
- Logout.
- Autorização.

### Fase 3 — Grupos

- Criar grupo.
- Convites.
- Entrar em grupo.
- Listar membros.
- Controle de permissões.

### Fase 4 — Desafios

- Criar desafio.
- Definir duração mínima.
- Definir frequência semanal.
- Configurar período.
- Exibir progresso.

### Fase 5 — Atividades

- Registrar atividade.
- Upload de evidências.
- Feed.
- Detalhes da atividade.

### Fase 6 — Verificação

- Registrar reações.
- Calcular consenso.
- Exibir status.
- Revisão administrativa.
- Encerramento automático.

### Fase 7 — Competição

- Classificação.
- Histórico semanal.
- Sequências de metas cumpridas.
- Resumo do grupo.

### Fase 8 — PWA e produção

- Manifest.
- Service Worker.
- Cache estático.
- Instalação mobile.
- HTTPS.
- Backups.
- Deploy em produção (Galaxy A14, 0032).

Situação: PWA em `decisoes/0021-pwa-e-service-worker.md`; backups em `decisoes/0022-backups.md`; HTTPS pelo túnel da Cloudflare: em produção, no Galaxy A14 (`wx.jpgmaraujo.com.br`, `run.bat --ssh`) e, em testes, no `run.bat --tunnel`. A VM Oracle foi abandonada (`decisoes/0032-a14-como-producao.md`).

### Fase 9 — Gamificação (opcional)

Modo Jogo como tela inicial alternativa, com avatar em folha de sprites, Energia, cartas de provocação entre colegas, missões semanais e Rival da semana, sem alterar a competição oficial. Fases G1 a G7 em `gamificacao.md`; direção em `decisoes/0023-gamificacao.md` e `decisoes/0024-cartas-e-avatar-importado.md`.

---

## 19. Fora do escopo inicial

Não implementar no MVP:

- Rastreamento GPS.
- Integração com wearables.
- Integração com Strava.
- Contagem automática de passos.
- Monitoramento de frequência cardíaca.
- Estimativa de calorias.
- Mensagens privadas.
- Grupos públicos.
- Descoberta de usuários.
- Planos de treinamento.
- Inteligência artificial para validar imagens.
- Pontuação baseada em intensidade.
- Notificações push e lembretes agendados. A lista dentro do aplicativo existe: ver `decisoes/0020-notificacoes-no-aplicativo.md`.
- Processamento assíncrono distribuído.

Essas funcionalidades poderão ser consideradas posteriormente, caso exista necessidade real.

---

## 20. Diretrizes para agentes de desenvolvimento

Todos os documentos, mensagens da interface, comentários explicativos e textos apresentados ao usuário devem estar em Português brasileiro.

As regras detalhadas do repositório, incluindo a convenção de nomes do código, estão em `AGENTS.md`.

Ao implementar novas funcionalidades:

1. Respeitar a organização por fatias verticais.
2. Manter contratos HTTP explícitos.
3. Manter SQL próximo da funcionalidade que o utiliza.
4. Evitar abstrações prematuras.
5. Preferir estruturas de dados simples.
6. Manter regras de negócio testáveis.
7. Não introduzir dependências sem justificativa.
8. Preservar compatibilidade Native AOT.
9. Considerar o baixo consumo de memória.
10. Garantir autorização em todas as operações.
11. Evitar operações que dependam de rede externa durante o uso normal.
12. Atualizar a documentação quando uma decisão arquitetural mudar.

### Princípio final

A aplicação deve ser simples o suficiente para que uma única pessoa consiga compreender, desenvolver, implantar e manter todo o sistema.

A arquitetura deve favorecer clareza, previsibilidade e baixo custo operacional, sem sacrificar a integridade dos dados ou a segurança dos participantes.