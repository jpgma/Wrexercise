namespace Wrexercise.IntegrationTests.Deploy;

/// <summary>
/// O relatório de deploy/metricas.sql (metricas.bat) roda no banco com todas as migrações. Sem este teste,
/// uma migração que renomeia uma coluna só quebraria o relatório no celular, semanas depois.
/// </summary>
public class MetricasSqlTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Metricas_RodaNoBancoMigrado_SemEscrever()
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(FindRoot(), "deploy", "metricas.sql"), Token);

        // O psql troca :dias pelo período; aqui, um valor fixo. Comandos do psql (\...) não rodariam pelo Npgsql.
        Assert.DoesNotContain(sql.Split('\n'), line => line.TrimStart().StartsWith('\\'));
        Assert.Contains(":dias", sql);

        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var command = database.DataSource.CreateCommand(
            "SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY;\n" + sql.Replace(":dias", "30"));

        await command.ExecuteNonQueryAsync(Token);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wrexercise.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
