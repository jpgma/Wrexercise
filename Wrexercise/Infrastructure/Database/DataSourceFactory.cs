using Npgsql;

namespace Wrexercise.Infrastructure.Database;

internal static class DataSourceFactory
{
    public static NpgsqlDataSource Create(IServiceProvider services)
    {
        var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("A configuração ConnectionStrings:Database não foi definida.");
        }

        // O builder slim evita recursos do Npgsql que não usamos e reduz o binário nativo.
        var builder = new NpgsqlSlimDataSourceBuilder(connectionString);
        builder.EnableArrays();
        return builder.Build();
    }
}
