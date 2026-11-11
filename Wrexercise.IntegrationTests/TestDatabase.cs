using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.IntegrationTests;

/// <summary>Banco descartável criado no PostgreSQL dos testes (scripts/test-db.sh).</summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private const string DefaultServer =
        "Host=127.0.0.1;Port=5434;Database=wrexercise_test;Username=wrexercise;Password=wrexercise_test";

    private const string TemplatePrefix = "wrexercise_template_";

    private static readonly Lazy<Task<string>> MigratedTemplate = new(CreateMigratedTemplateAsync);

    private static string ServerConnectionString =>
        Environment.GetEnvironmentVariable("WREXERCISE_TEST_DB") ?? DefaultServer;

    private readonly string _name;

    private TestDatabase(string name, string connectionString)
    {
        _name = name;
        ConnectionString = connectionString;
        DataSource = new NpgsqlSlimDataSourceBuilder(connectionString).Build();
    }

    public string ConnectionString { get; }

    public NpgsqlDataSource DataSource { get; }

    /// <summary>Banco vazio, sem migrações.</summary>
    public static Task<TestDatabase> CreateAsync() => CreateAsync(template: null);

    /// <summary>
    /// Banco com todas as migrações, copiado de um modelo. Copiar é bem mais rápido que migrar
    /// a cada teste; o modelo só é recriado quando as migrações mudam.
    /// </summary>
    public static async Task<TestDatabase> CreateMigratedAsync() => await CreateAsync(await MigratedTemplate.Value);

    private static async Task<TestDatabase> CreateAsync(string? template)
    {
        var name = $"wrexercise_test_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync(template is null ? $"CREATE DATABASE {name}" : $"CREATE DATABASE {name} TEMPLATE {template}");
        return new TestDatabase(name, ConnectionStringFor(name));
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await ExecuteOnServerAsync($"DROP DATABASE {_name} WITH (FORCE)");
    }

    /// <summary>
    /// O nome do modelo leva o hash das migrações. Ele é migrado com outro nome e só depois
    /// renomeado, para que uma execução interrompida não deixe um modelo pela metade.
    /// </summary>
    private static async Task<string> CreateMigratedTemplateAsync()
    {
        var migrations = MigrationCatalog.LoadEmbedded();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', migrations.Select(migration => migration.Checksum))));
        var template = TemplatePrefix + Convert.ToHexStringLower(hash)[..16];

        await using var server = new NpgsqlSlimDataSourceBuilder(ServerConnectionString).Build();
        await using (var exists = server.CreateCommand("SELECT EXISTS (SELECT FROM pg_database WHERE datname = $1)"))
        {
            exists.Parameters.AddWithValue(template);
            if ((bool)(await exists.ExecuteScalarAsync())!)
            {
                return template;
            }
        }

        var building = $"wrexercise_building_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync($"CREATE DATABASE {building}");
        await using (var dataSource = new NpgsqlSlimDataSourceBuilder(ConnectionStringFor(building)).Build())
        {
            await MigrationRunner.RunAsync(dataSource, migrations, NullLogger.Instance, CancellationToken.None);
        }

        await using (var stale = server.CreateCommand("SELECT datname FROM pg_database WHERE datname LIKE $1"))
        {
            stale.Parameters.AddWithValue(TemplatePrefix + "%");
            await using var reader = await stale.ExecuteReaderAsync();
            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            foreach (var name in names)
            {
                await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
            }
        }

        try
        {
            await ExecuteOnServerAsync($"ALTER DATABASE {building} RENAME TO {template}");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // Outra execução criou o mesmo modelo ao mesmo tempo.
            await ExecuteOnServerAsync($"DROP DATABASE {building} WITH (FORCE)");
        }

        return template;
    }

    private static string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = database }.ToString();

    private static async Task ExecuteOnServerAsync(string sql)
    {
        await using var dataSource = new NpgsqlSlimDataSourceBuilder(ServerConnectionString).Build();
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}
