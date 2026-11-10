using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class LimpezaAvataresTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    /// <summary>
    /// Usa o banco e o armazenamento do servidor (o banco do servidor tem arrays habilitados). Subir o
    /// servidor também roda a limpeza de início; por isso os testes conferem o disco, não a contagem.
    /// </summary>
    private Task<int> LimparAsync() => LimpezaAvatares.LimparAsync(
        _host.Services.GetRequiredService<NpgsqlDataSource>(),
        _host.Services.GetRequiredService<ArmazenamentoAvatares>(),
        Token);

    private string CriarArquivo(TimeSpan idade)
    {
        var nome = $"{Guid.NewGuid():N}.png";
        var caminho = Path.Combine(_host.AvataresDiretorio, nome);
        File.WriteAllBytes(caminho, [1, 2, 3]);
        File.SetLastWriteTimeUtc(caminho, DateTime.UtcNow - idade);
        return nome;
    }

    [Fact]
    public async Task Limpar_ApagaSoOsArquivosAntigosSemDono()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var emUso = CriarArquivo(TimeSpan.FromDays(2));
        var orfao = CriarArquivo(TimeSpan.FromDays(2));
        var recente = CriarArquivo(TimeSpan.FromMinutes(1));
        await _host.ExecuteAsync($"INSERT INTO avatares (usuario_id, arquivo) VALUES ('{ana.Id}', '{emUso}')");

        await LimparAsync();

        var restantes = Directory.GetFiles(_host.AvataresDiretorio).Select(Path.GetFileName).ToHashSet();
        Assert.Contains(emUso, restantes);
        Assert.Contains(recente, restantes);
        Assert.DoesNotContain(orfao, restantes);
    }

    [Fact]
    public async Task Limpar_IgnoraArquivosComOutroNome()
    {
        var caminho = Path.Combine(_host.AvataresDiretorio, "leia-me.png");
        File.WriteAllBytes(caminho, [1]);
        File.SetLastWriteTimeUtc(caminho, DateTime.UtcNow.AddDays(-2));

        await LimparAsync();

        Assert.True(File.Exists(caminho));
    }
}
