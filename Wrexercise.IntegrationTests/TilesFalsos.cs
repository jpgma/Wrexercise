using System.Net;
using System.Net.Http.Headers;

namespace Wrexercise.IntegrationTests;

/// <summary>Responde a qualquer tile com um PNG mínimo e conta as buscas (ADR 0080).</summary>
public sealed class TilesFalsos : HttpMessageHandler
{
    public static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    private int _buscas;

    public int Buscas => _buscas;

    public List<string> Caminhos { get; } = [];

    /// <summary>Simula o servidor de mapas fora do ar.</summary>
    public bool ForaDoAr { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _buscas);
        lock (Caminhos)
        {
            Caminhos.Add(request.RequestUri!.AbsolutePath);
        }

        if (ForaDoAr)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        var content = new ByteArrayContent(Png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
