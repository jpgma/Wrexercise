# Backend — .NET 10 Native AOT

Complementa o `AGENTS.md` da raiz.

## Estrutura de uma fatia

Organização por operação HTTP (ver `docs/decisoes/0010-fatia-por-operacao.md`):

```text
Features/Autenticacao/
├── Routes.cs                 # MapAutenticacaoRoutes + políticas transversais
├── Registrar.cs              # fluxo completo da operação
├── Entrar.cs
├── Sair.cs
├── ObterSessao.cs
├── Respostas.cs              # DTOs usados por 2+ operações
├── Sessoes.cs                # helpers SQL/domínio reutilizados
├── Senhas.cs
├── ValidacaoConta.cs
└── Contracts/                # só o que OUTRA fatia importa
```

Crie apenas os arquivos necessários. Fatias triviais (ex.: Health) podem ficar só com `Routes.cs`.
`Infrastructure/` contém somente recursos compartilhados.

`webapp/` é um projeto Node independente, excluído dos itens padrão em `Wrexercise.csproj`. Não remova essa exclusão: sem ela, os arquivos de `node_modules/` entram na publicação.

Fluxo de um endpoint: autenticação → validação do contrato → regra de negócio → SQL → resposta.
`Routes.cs` fica fino (grupos, rate limit, `RequireSessao`, ligação rota → handler).
Regras puras reutilizadas ficam em arquivo compartilhado nomeado pelo conceito (`ValidacaoConta.cs`, `ProgressoDesafio.cs`), testáveis sem banco.
Não use `Commands.cs`, `Queries.cs`, `Contracts.cs` ou `Rules.cs` na raiz da fatia.

**Contratos entre fatias:** quando outra fatia realmente precisar de um tipo, coloque-o em `Features/<Fatia>/Contracts/` no namespace `Wrexercise.Features.<Fatia>.Contracts`. É o único namespace de outra fatia que pode ser referenciado.

### Template de operação

Substituir placeholders; omitir seções vazias (ex.: sem body em GET).

```csharp
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;
using Npgsql;

namespace Wrexercise.Features.Autenticacao;

// --- Contratos desta operação (omitir se a rota não tiver body ou DTO exclusivo) ---
// Campos anuláveis quando o JSON pode omiti-los; validar antes de usar.
internal sealed record OperacaoRequest(/* ... */);

// --- Handler HTTP (registrado em Routes.cs) ---
internal static class Operacao
{
    public static async Task<IResult> HandleAsync(
        OperacaoRequest request,           // omitir parâmetro se não houver body
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        // 1. Autenticação / autorização já aplicada em Routes (RequireSessao, rate limit, etc.)

        // 2. Validação do contrato
        if (!IsValidRequest(request))
        {
            return InvalidData();
        }

        // 3. Regra de negócio (puras, sem I/O — métodos nesta classe ou arquivo compartilhado)
        var normalizado = Normalize(/* ... */);

        // 4. SQL (leitura ou transação)
        var resultado = await ExecutarAsync(dataSource, normalizado, cancellationToken);
        if (resultado.Erro is { } erro)
        {
            return erro;
        }

        // 5. Resposta HTTP
        return Results.Json(
            resultado.Valor,
            AppJsonContext.Default.TipoResponse,
            statusCode: StatusCodes.Status201Created);
    }

    private static IResult InvalidData() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");

    // --- Regras puras só desta operação (mover para Validacao*.cs se outra operação reutilizar) ---

    private static bool IsValidRequest(OperacaoRequest request) => /* ... */;

    private static string Normalize(/* ... */) => /* ... */;

    // --- SQL privado à operação (mover para Sessoes.cs etc. se outra operação reutilizar) ---

    private static async Task<(IResult? Erro, object? Valor)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        /* ... */,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // Transação quando houver várias escritas relacionadas.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // ...
        await transaction.CommitAsync(cancellationToken);
        return (null, /* valor */);
    }
}
```

`Routes.cs` fino:

```csharp
group.MapPost("/operacao", Operacao.HandleAsync).RequireRateLimiting(TentativasPolicy);
```

Arquivo compartilhado (`Respostas.cs`, `Sessoes.cs`): mesmo namespace da fatia; tipos `internal`; sem handler HTTP — só records e métodos estáticos reutilizados por duas ou mais operações.

## Native AOT

- Aviso de AOT ou trimming é erro de implementação. Não o suprima sem justificativa documentada.
- Sem reflexão dinâmica, `Assembly.Load` ou bibliotecas incompatíveis com trimming.
- Todo tipo serializado em JSON é registrado em `Infrastructure/Json/AppJsonContext.cs`.
- Serialize sempre contratos explícitos, nunca estruturas internas.

## SQL

- Npgsql direto, com `NpgsqlDataSource` injetado. Parâmetros posicionais (`$1`, `$2`).
- SQL fica junto da fatia que o usa (no arquivo da operação ou em helper compartilhado).
- Projete só as colunas necessárias. Feeds usam paginação por cursor.
- Várias escritas relacionadas usam transação explícita.
- O `NpgsqlSlimDataSourceBuilder` só tem os recursos habilitados em `DataSourceFactory`; habilite o que faltar lá (por exemplo, arrays).

## Migrações

- Arquivos em `Infrastructure/Database/Migrations/`, no formato `NNN-kebab-case.sql`, com numeração sequencial.
- Só avançam: nunca edite uma migração já aplicada, crie outra. O executor recusa arquivos alterados.
- Nunca rodam na inicialização. Em desenvolvimento, aplique com `dotnet run --project Wrexercise -- migrate`. A implantação já as aplica: em produção (A14), `run.bat --ssh` roda `app/Wrexercise migrate` no celular depois do backup; na pilha local em Docker, `run.bat` roda `docker compose ... run --rm app migrate`. Não há passo manual (ver `docs/decisoes/0001-migracoes-sql.md`).

## Erros

Use `ApiProblem` (`application/problem+json` com `code`). Códigos em `MAIUSCULAS_COM_SUBLINHADO`, títulos em pt-BR, sem detalhes internos.

## Testes

- `Wrexercise.UnitTests`: regras puras e testes de arquitetura.
- `Wrexercise.IntegrationTests`: HTTP e SQL contra o PostgreSQL dos testes (`scripts/test-db.sh`: porta 5434, em memória e sem `fsync`), com um banco descartável por teste. Use `TestDatabase.CreateMigratedAsync()`, que copia um modelo já migrado; `TestDatabase.CreateAsync()` só para testes das próprias migrações. Os servidores de teste usam `UseTestDefaults()`: logs só de avisos e sem os serviços periódicos.
