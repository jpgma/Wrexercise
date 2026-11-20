using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Wrexercise.Infrastructure.Files;

/// <summary>Um tile do mapa no esquema <c>z/x/y</c> do OpenStreetMap (Web Mercator).</summary>
internal readonly record struct TileMapa(int Zoom, int X, int Y)
{
    /// <summary>Nível de rua: um tile de 256 pixels cobre uns 600 metros no equador.</summary>
    public const int ZoomPadrao = 16;

    public static TileMapa De(double latitude, double longitude, int zoom = ZoomPadrao)
    {
        var n = 1 << zoom;
        var x = (int)Math.Floor((longitude + 180) / 360 * n);
        var latRad = latitude * Math.PI / 180;
        var y = (int)Math.Floor((1 - Math.Asinh(Math.Tan(latRad)) / Math.PI) / 2 * n);
        return new TileMapa(zoom, Math.Clamp(x, 0, n - 1), Math.Clamp(y, 0, n - 1));
    }

    /// <summary>O tile vizinho; <c>null</c> além do polo. A longitude dá a volta no globo.</summary>
    public TileMapa? Deslocar(int dx, int dy)
    {
        var n = 1 << Zoom;
        var y = Y + dy;
        if (y < 0 || y >= n)
        {
            return null;
        }

        return new TileMapa(Zoom, ((X + dx) % n + n) % n, y);
    }
}

/// <summary>
/// Busca os tiles do mini-mapa no servidor de mapas e os guarda em disco (ADR 0080). O navegador só fala com o
/// Wrexercise: o servidor de mapas vê o IP do servidor e o tile, nunca quem olhou.
/// </summary>
internal sealed class CacheMapas : IDisposable
{
    private const int MaxBytesTile = 512 * 1024;
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly HttpClient _http;
    private readonly string _diretorio;
    private readonly string _servidor;
    private readonly ILogger<CacheMapas> _logger;
    // Poucos acessos: uma busca por vez evita baixar o mesmo tile duas vezes.
    private readonly SemaphoreSlim _busca = new(1, 1);

    public CacheMapas(IOptions<MapasOptions> options, HttpMessageHandler handler, ILogger<CacheMapas> logger)
    {
        var mapas = options.Value;
        // Caminho absoluto: `Results.File` lê um caminho relativo como um arquivo dentro de wwwroot.
        _diretorio = Path.GetFullPath(mapas.Diretorio);
        _servidor = mapas.Servidor.TrimEnd('/');
        _logger = logger;
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(mapas.UserAgent);
    }

    /// <summary>O caminho do PNG do tile, ou <c>null</c> se o servidor de mapas não respondeu.</summary>
    public async Task<string?> ObterAsync(TileMapa tile, CancellationToken cancellationToken)
    {
        var caminho = Path.Combine(
            _diretorio,
            tile.Zoom.ToString(),
            tile.X.ToString(),
            $"{tile.Y}.png");
        if (File.Exists(caminho))
        {
            return caminho;
        }

        await _busca.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(caminho))
            {
                return caminho;
            }

            var bytes = await BaixarAsync(tile, cancellationToken);
            if (bytes is null)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
            var temporario = caminho + ".tmp";
            await File.WriteAllBytesAsync(temporario, bytes, cancellationToken);
            File.Move(temporario, caminho, overwrite: true);
            return caminho;
        }
        finally
        {
            _busca.Release();
        }
    }

    private async Task<byte[]?> BaixarAsync(TileMapa tile, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{_servidor}/{tile.Zoom}/{tile.X}/{tile.Y}.png");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentLength is > MaxBytesTile)
            {
                _logger.LogWarning("O servidor de mapas respondeu {Status}.", (int)response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > MaxBytesTile || !bytes.AsSpan().StartsWith(AssinaturaPng))
            {
                _logger.LogWarning("O servidor de mapas não devolveu um PNG.");
                return null;
            }

            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Só o tipo do erro: a URL tem o tile, e o tile diz onde alguém treinou.
            _logger.LogWarning("Falha ao buscar um tile do mapa: {Erro}.", ex.GetType().Name);
            return null;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _busca.Dispose();
    }
}
