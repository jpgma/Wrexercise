using System.Buffers.Binary;
using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Notificacoes;
using Wrexercise.Features.Reacoes;
using Wrexercise.Infrastructure.Json;
using Wrexercise.Infrastructure.Push;

namespace Wrexercise.IntegrationTests.Features.Notificacoes;

public sealed class PushTests : IAsyncLifetime
{
    private readonly (string Publica, string Privada) _vapid = WebPushCriptografia.GerarChaves();
    private readonly ServicoPushFalso _servico = new();
    private ApiTestHost _host = null!;
    private WebPushClient _push = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await ApiTestHost.CreateAsync(builder => builder
            .UseSetting("Vapid:PublicKey", _vapid.Publica)
            .UseSetting("Vapid:PrivateKey", _vapid.Privada));
        _push = new WebPushClient(
            Options.Create(new VapidOptions { PublicKey = _vapid.Publica, PrivateKey = _vapid.Privada }),
            _servico);
    }

    public async ValueTask DisposeAsync()
    {
        _push.Dispose();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task SemChaves_PushFicaDesligado()
    {
        await using var semChaves = await ApiTestHost.CreateAsync();
        var ana = await semChaves.CreateUsuarioAsync("Ana");

        var configuracao = await ana.Client.GetFromJsonAsync(
            "/api/notificacoes/push", AppJsonContext.Default.PushConfiguracaoResponse, Token);
        Assert.Null(configuracao!.ChavePublica);

        var (request, _) = NovaInscricao();
        await ApiTestHost.AssertProblemAsync(
            await SalvarAsync(ana, request), HttpStatusCode.Conflict, "PUSH_DESLIGADO");
    }

    [Fact]
    public async Task Configuracao_TrazAChavePublica()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var configuracao = await ana.Client.GetFromJsonAsync(
            "/api/notificacoes/push", AppJsonContext.Default.PushConfiguracaoResponse, Token);

        Assert.Equal(_vapid.Publica, configuracao!.ChavePublica);
    }

    [Fact]
    public async Task Inscricao_RecusaServicoDesconhecidoEChaveInvalida()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var (request, _) = NovaInscricao();

        await ApiTestHost.AssertProblemAsync(
            await SalvarAsync(ana, request with { Endpoint = "https://exemplo.com/push" }),
            HttpStatusCode.BadRequest,
            "PUSH_INVALIDO");
        await ApiTestHost.AssertProblemAsync(
            await SalvarAsync(ana, request with { P256dh = "abc" }),
            HttpStatusCode.BadRequest,
            "PUSH_INVALIDO");
        Assert.Equal(0, await _host.CountAsync("SELECT count(*) FROM push_subscriptions"));
    }

    [Fact]
    public async Task NovaAtividade_VaiAoTelefoneDoRevisor_UmaVez()
    {
        var ana = await _host.CreateUsuarioAsync("Ana Souza");
        var grupoId = await _host.CreateGrupoAsync(ana, "Turma");
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var (request, chaveBia) = NovaInscricao();
        (await SalvarAsync(bia, request)).EnsureSuccessStatusCode();
        var atividadeId = await CriarAtividadeAsync(ana, grupoId);

        await EnviarAsync();

        var recebida = Assert.Single(_servico.Recebidas);
        Assert.Equal(request.Endpoint, recebida.Endpoint);
        Assert.StartsWith("vapid t=", recebida.Authorization);
        Assert.EndsWith($", k={_vapid.Publica}", recebida.Authorization);
        Assert.Equal("aes128gcm", recebida.ContentEncoding);
        Assert.Equal("86400", recebida.Ttl);

        var mensagem = Decifrar(recebida.Corpo, chaveBia, request);
        Assert.Equal("Turma", mensagem.Titulo);
        Assert.Equal("Ana registrou um treino. Dá uma olhada!", mensagem.Corpo);
        Assert.Equal($"/grupos/{grupoId}/revisar/{atividadeId}", mensagem.Url);
        Assert.Equal(1, mensagem.NaoLidas);
        Assert.Equal(mensagem.Id.ToString("N"), recebida.Topic);

        await EnviarAsync();
        Assert.Single(_servico.Recebidas);
    }

    [Fact]
    public async Task ReacaoSomada_VaiDeNovo_NoMesmoTopico()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        var (request, chaveAna) = NovaInscricao();
        (await SalvarAsync(ana, request)).EnsureSuccessStatusCode();
        var atividadeId = await CriarAtividadeAsync(ana, grupoId);

        await ReagirAsync(bia, atividadeId);
        await EnviarAsync();
        await ReagirAsync(caio, atividadeId);
        await EnviarAsync();

        Assert.Equal(2, _servico.Recebidas.Count);
        Assert.Equal(_servico.Recebidas[0].Topic, _servico.Recebidas[1].Topic);
        Assert.Equal("Bia reagiu ao seu treino", Decifrar(_servico.Recebidas[0].Corpo, chaveAna, request).Corpo);
        Assert.Equal(
            "Caio e mais 1 pessoa reagiram ao seu treino",
            Decifrar(_servico.Recebidas[1].Corpo, chaveAna, request).Corpo);
    }

    [Fact]
    public async Task LidaAntigaOuSemInscricao_NaoVai()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await _host.AddMembroAsync(grupoId, nome: "Caio");
        var dani = await _host.AddMembroAsync(grupoId, nome: "Dani");
        (await SalvarAsync(bia, NovaInscricao().Request)).EnsureSuccessStatusCode();
        (await SalvarAsync(dani, NovaInscricao().Request)).EnsureSuccessStatusCode();
        await CriarAtividadeAsync(ana, grupoId);

        (await bia.Client.PostAsync("/api/notificacoes/lidas", null, Token)).EnsureSuccessStatusCode();
        await _host.ExecuteAsync(
            $"UPDATE notificacoes SET updated_at = now() - interval '2 hours' WHERE usuario_id = '{dani.Id}'");
        await EnviarAsync();

        Assert.Empty(_servico.Recebidas);
        Assert.Equal(0, await _host.CountAsync("SELECT count(*) FROM notificacoes WHERE push_enviado_em IS NULL"));
    }

    [Fact]
    public async Task ServicoEsqueceuOAparelho_ApagaAInscricao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        (await SalvarAsync(bia, NovaInscricao().Request)).EnsureSuccessStatusCode();
        await CriarAtividadeAsync(ana, grupoId);
        _servico.Resposta = HttpStatusCode.Gone;

        await EnviarAsync();

        Assert.Single(_servico.Recebidas);
        Assert.Equal(0, await _host.CountAsync("SELECT count(*) FROM push_subscriptions"));
    }

    [Fact]
    public async Task MesmoAparelho_FicaComQuemEntrouPorUltimo_EPodeSerRemovido()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var bia = await _host.CreateUsuarioAsync("Bia");
        var (request, _) = NovaInscricao();

        (await SalvarAsync(ana, request)).EnsureSuccessStatusCode();
        (await SalvarAsync(bia, request)).EnsureSuccessStatusCode();
        Assert.Equal(bia.Id, await _host.ScalarAsync<Guid>("SELECT usuario_id FROM push_subscriptions"));

        var endpoint = Uri.EscapeDataString(request.Endpoint!);
        (await ana.Client.DeleteAsync($"/api/notificacoes/push?endpoint={endpoint}", Token)).EnsureSuccessStatusCode();
        Assert.Equal(1, await _host.CountAsync("SELECT count(*) FROM push_subscriptions"));
        (await bia.Client.DeleteAsync($"/api/notificacoes/push?endpoint={endpoint}", Token)).EnsureSuccessStatusCode();
        Assert.Equal(0, await _host.CountAsync("SELECT count(*) FROM push_subscriptions"));
    }

    [Fact]
    public async Task Inscricoes_GuardaAs10MaisRecentes()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        for (var i = 0; i < 11; i++)
        {
            (await SalvarAsync(ana, NovaInscricao().Request)).EnsureSuccessStatusCode();
        }

        Assert.Equal(10, await _host.CountAsync("SELECT count(*) FROM push_subscriptions"));
    }

    private Task EnviarAsync() => EnvioPush.EnviarPendentesAsync(
        _host.Services.GetRequiredService<Npgsql.NpgsqlDataSource>(), _push, Token);

    private static (PushSubscriptionRequest Request, ECDiffieHellman Chave) NovaInscricao()
    {
        var chave = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var publica = chave.ExportParameters(includePrivateParameters: false).Q;
        byte[] ponto = [0x04, .. publica.X!, .. publica.Y!];
        return (
            new PushSubscriptionRequest(
                $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}",
                Base64Url.EncodeToString(ponto),
                Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16))),
            chave);
    }

    private static Task<HttpResponseMessage> SalvarAsync(TestUsuario usuario, PushSubscriptionRequest request) =>
        usuario.Client.PutAsJsonAsync(
            "/api/notificacoes/push", request, AppJsonContext.Default.PushSubscriptionRequest, Token);

    /// <summary>O lado do aparelho da RFC 8291, para ler o que o servidor enviou.</summary>
    private static PushMensagem Decifrar(byte[] corpo, ECDiffieHellman aparelho, PushSubscriptionRequest request)
    {
        var salt = corpo.AsSpan(0, 16);
        Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(corpo.AsSpan(16)));
        var chaveServidor = corpo.AsSpan(21, corpo[20]);
        var cifrado = corpo.AsSpan(21 + corpo[20]);

        using var servidor = ECDiffieHellman.Create(WebPushCriptografia.ParametrosPublicos(chaveServidor));
        var segredoEcdh = aparelho.DeriveRawSecretAgreement(servidor.PublicKey);
        var chaveAparelho = Base64Url.DecodeFromChars(request.P256dh!);
        byte[] info = [.. "WebPush: info\0"u8, .. chaveAparelho, .. chaveServidor];
        var ikm = new byte[32];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, segredoEcdh, ikm, Base64Url.DecodeFromChars(request.Auth!), info);
        var cek = new byte[16];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, cek, salt, "Content-Encoding: aes128gcm\0"u8);
        var nonce = new byte[12];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, nonce, salt, "Content-Encoding: nonce\0"u8);

        var claro = new byte[cifrado.Length - 16];
        using var aes = new AesGcm(cek, 16);
        aes.Decrypt(nonce, cifrado[..^16], cifrado[^16..], claro);
        Assert.Equal(0x02, claro[^1]);
        return JsonSerializer.Deserialize(claro.AsSpan(0, claro.Length - 1), AppJsonContext.Default.PushMensagem)!;
    }

    private static async Task<Guid> CriarAtividadeAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida,
                3600,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                null,
                TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        return atividade!.Id;
    }

    private static async Task ReagirAsync(TestUsuario usuario, Guid atividadeId)
    {
        var response = await usuario.Client.PutAsJsonAsync(
            $"/api/atividades/{atividadeId}/reacao",
            new ReacaoRequest(TipoReacao.Kudos),
            AppJsonContext.Default.ReacaoRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private sealed record PushRecebido(
        string Endpoint,
        string? Authorization,
        string? Topic,
        string? Ttl,
        string? ContentEncoding,
        byte[] Corpo);

    private sealed class ServicoPushFalso : HttpMessageHandler
    {
        public List<PushRecebido> Recebidas { get; } = [];

        public HttpStatusCode Resposta { get; set; } = HttpStatusCode.Created;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Recebidas.Add(new PushRecebido(
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Headers.GetValues("Topic").Single(),
                request.Headers.GetValues("TTL").Single(),
                request.Content!.Headers.ContentEncoding.Single(),
                await request.Content.ReadAsByteArrayAsync(cancellationToken)));
            return new HttpResponseMessage(Resposta);
        }
    }
}
