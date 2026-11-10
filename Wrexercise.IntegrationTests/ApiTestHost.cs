using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Wrexercise.Features.Grupos;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests;

internal sealed record TestUsuario(Guid Id, HttpClient Client);

/// <summary>
/// API e banco descartável para testes que precisam de várias pessoas. Usuários e sessões
/// são criados direto no banco, sem passar pelo PBKDF2 nem pelo limite de tentativas do cadastro.
/// </summary>
internal sealed class ApiTestHost : IAsyncDisposable
{
    private readonly TestDatabase _database;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<HttpClient> _clients = [];

    internal Npgsql.NpgsqlDataSource DataSource => _database.DataSource;

    /// <summary>Os serviços do servidor de teste (sobe o servidor, se ainda não subiu).</summary>
    internal IServiceProvider Services => _factory.Services;

    private ApiTestHost(TestDatabase database, string evidenciasDiretorio, string avataresDiretorio)
    {
        _database = database;
        AvataresDiretorio = avataresDiretorio;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", database.ConnectionString);
            builder.UseSetting("Evidencias:Diretorio", evidenciasDiretorio);
            builder.UseSetting("Avatares:Diretorio", avataresDiretorio);
        });
    }

    /// <summary>Onde o servidor de teste guarda as folhas de avatar importadas.</summary>
    public string AvataresDiretorio { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<ApiTestHost> CreateAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await MigrationRunner.RunAsync(database.DataSource, MigrationCatalog.LoadEmbedded(), NullLogger.Instance, Token);
        var evidenciasDiretorio = Path.Combine(Path.GetTempPath(), "wrexercise-test-evidencias", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidenciasDiretorio);
        var avataresDiretorio = Path.Combine(Path.GetTempPath(), "wrexercise-test-avatares", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(avataresDiretorio);
        return new ApiTestHost(database, evidenciasDiretorio, avataresDiretorio);
    }

    public HttpClient CreateAnonimo()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        _clients.Add(client);
        return client;
    }

    public async Task<TestUsuario> CreateUsuarioAsync(string nome = "Pessoa")
    {
        Guid id;
        await using (var insert = _database.DataSource.CreateCommand(
            "INSERT INTO usuarios (nome_exibicao, email, password_hash) VALUES ($1, $2, 'x') RETURNING id"))
        {
            insert.Parameters.AddWithValue(nome);
            insert.Parameters.AddWithValue($"{Guid.NewGuid():N}@exemplo.com");
            id = (Guid)(await insert.ExecuteScalarAsync(Token))!;
        }

        var sessao = OpaqueToken.Generate();
        await using (var insert = _database.DataSource.CreateCommand(
            "INSERT INTO sessoes_autenticacao (usuario_id, token_hash, expires_at) VALUES ($1, $2, now() + interval '1 day')"))
        {
            insert.Parameters.AddWithValue(id);
            insert.Parameters.AddWithValue(OpaqueToken.Hash(sessao));
            await insert.ExecuteNonQueryAsync(Token);
        }

        var client = CreateAnonimo();
        client.DefaultRequestHeaders.Add("Cookie", $"wx_session={sessao}");
        return new TestUsuario(id, client);
    }

    /// <summary>Cria o grupo pela API, com <paramref name="proprietario"/> como dono.</summary>
    public async Task<Guid> CreateGrupoAsync(TestUsuario proprietario, string nome = "Grupo")
    {
        var response = await proprietario.Client.PostAsJsonAsync(
            "/api/grupos",
            new CriarGrupoRequest(nome, "America/Sao_Paulo"),
            AppJsonContext.Default.CriarGrupoRequest,
            Token);
        response.EnsureSuccessStatusCode();

        var grupo = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.GrupoResponse, Token);
        return grupo!.Id;
    }

    public async Task<TestUsuario> AddMembroAsync(Guid grupoId, string papel = PapelMembro.Membro, string nome = "Pessoa")
    {
        var usuario = await CreateUsuarioAsync(nome);

        await using var insert = _database.DataSource.CreateCommand(
            "INSERT INTO membros_grupo (grupo_id, usuario_id, papel) VALUES ($1, $2, $3)");
        insert.Parameters.AddWithValue(grupoId);
        insert.Parameters.AddWithValue(usuario.Id);
        insert.Parameters.AddWithValue(papel);
        await insert.ExecuteNonQueryAsync(Token);

        return usuario;
    }

    /// <summary>Dá moral a tudo que espera <paramref name="usuario"/> no grupo, como a API exige antes de registrar.</summary>
    public static async Task AvaliarFilaAsync(TestUsuario usuario, Guid grupoId)
    {
        var fila = await usuario.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades/para-avaliar",
            AppJsonContext.Default.FilaAvaliacaoResponse,
            Token);
        foreach (var atividade in fila!.Atividades)
        {
            var response = await usuario.Client.PutAsJsonAsync(
                $"/api/atividades/{atividade.Id}/reacao",
                new Wrexercise.Features.Reacoes.ReacaoRequest("Kudos"),
                AppJsonContext.Default.ReacaoRequest,
                Token);
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(Token);
    }

    public async Task<long> CountAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync(Token))!;
    }

    public async Task<T> ScalarAsync<T>(string sql, params object[] parameters)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        for (var i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue(parameters[i]);
        }

        return (T)(await command.ExecuteScalarAsync(Token))!;
    }

    public static async Task AssertProblemAsync(
        HttpResponseMessage response,
        System.Net.HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ProblemResponse, Token);
        Assert.Equal(code, problem?.Code);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
