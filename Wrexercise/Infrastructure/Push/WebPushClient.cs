using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Wrexercise.Infrastructure.Push;

internal enum ResultadoPush
{
    Entregue,
    /// <summary>O serviço de push esqueceu o aparelho (404 ou 410): a inscrição deve ser apagada.</summary>
    Expirada,
    Falhou,
}

internal sealed record DestinoPush(string Endpoint, string P256dh, string Auth);

/// <summary>Envia mensagens Web Push. Ver docs/decisoes/0049-notificacoes-push.md.</summary>
internal sealed class WebPushClient : IDisposable
{
    private static readonly TimeSpan ValidadeJwt = TimeSpan.FromHours(12);

    private readonly HttpClient _http;
    private readonly ECDsa? _chave;
    private readonly string _assunto;

    public WebPushClient(IOptions<VapidOptions> options, HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var vapid = options.Value;
        _assunto = vapid.Subject;
        if (!string.IsNullOrWhiteSpace(vapid.PublicKey) && !string.IsNullOrWhiteSpace(vapid.PrivateKey))
        {
            _chave = WebPushCriptografia.ImportarChaveVapid(vapid.PublicKey, vapid.PrivateKey);
            ChavePublica = vapid.PublicKey;
        }
    }

    /// <summary>A chave que o navegador usa para se inscrever, ou `null` com o push desligado.</summary>
    public string? ChavePublica { get; }

    public async Task<ResultadoPush> EnviarAsync(
        DestinoPush destino,
        byte[] conteudo,
        string topico,
        CancellationToken cancellationToken)
    {
        if (_chave is null
            || !Uri.TryCreate(destino.Endpoint, UriKind.Absolute, out var endpoint)
            || !WebPushCriptografia.TryDecode(destino.P256dh, out var chaveAparelho)
            || !WebPushCriptografia.TryDecode(destino.Auth, out var segredo))
        {
            return ResultadoPush.Falhou;
        }

        var corpo = WebPushCriptografia.Cifrar(conteudo, chaveAparelho, segredo);
        var jwt = WebPushCriptografia.CriarJwt(
            _chave,
            endpoint.GetLeftPart(UriPartial.Authority),
            _assunto,
            DateTimeOffset.UtcNow + ValidadeJwt);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("vapid", $"t={jwt}, k={ChavePublica}");
        // Um dia na fila do serviço basta: depois disso, a lista do app já mostrou o aviso.
        request.Headers.Add("TTL", "86400");
        request.Headers.Add("Urgency", "high");
        // O serviço troca uma mensagem ainda não entregue por outra do mesmo tópico.
        request.Headers.Add("Topic", topico);
        request.Content = new ByteArrayContent(corpo);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.Gone => ResultadoPush.Expirada,
                _ when response.IsSuccessStatusCode => ResultadoPush.Entregue,
                _ => ResultadoPush.Falhou,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ResultadoPush.Falhou;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _chave?.Dispose();
    }
}
