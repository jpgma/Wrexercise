using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wrexercise.Infrastructure.Push;

/// <summary>
/// Web Push sem dependências: cifra o conteúdo (RFC 8291, aes128gcm) e assina o JWT do VAPID (RFC 8292)
/// só com a criptografia do .NET, compatível com Native AOT. Ver docs/decisoes/0049-notificacoes-push.md.
/// </summary>
internal static class WebPushCriptografia
{
    private const int TamanhoChavePublica = 65;
    private const int TamanhoSegredo = 16;
    private const int TamanhoSalt = 16;
    private const int TamanhoTag = 16;
    private const int TamanhoCabecalho = TamanhoSalt + 4 + 1 + TamanhoChavePublica;
    private const uint TamanhoRegistro = 4096;

    /// <summary>A chave pública do aparelho (65 bytes, ponto não comprimido da P-256) e o segredo de 16 bytes.</summary>
    public static bool IsInscricaoValida(string p256dh, string auth)
    {
        if (!TryDecode(p256dh, out var chave) || chave.Length != TamanhoChavePublica || chave[0] != 0x04
            || !TryDecode(auth, out var segredo) || segredo.Length != TamanhoSegredo)
        {
            return false;
        }

        try
        {
            using var ecdh = ECDiffieHellman.Create(ParametrosPublicos(chave));
            return true;
        }
        // No Windows (CNG), um ponto fora da curva vem como PlatformNotSupportedException.
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public static byte[] Cifrar(ReadOnlySpan<byte> conteudo, ReadOnlySpan<byte> chaveAparelho, ReadOnlySpan<byte> segredo)
    {
        using var servidor = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Cifrar(conteudo, chaveAparelho, segredo, servidor, RandomNumberGenerator.GetBytes(TamanhoSalt));
    }

    /// <summary>Com a chave efêmera e o salt fixos, para conferir com o exemplo da RFC 8291.</summary>
    internal static byte[] Cifrar(
        ReadOnlySpan<byte> conteudo,
        ReadOnlySpan<byte> chaveAparelho,
        ReadOnlySpan<byte> segredo,
        ECDiffieHellman servidor,
        ReadOnlySpan<byte> salt)
    {
        var chaveServidor = PontoPublico(servidor.ExportParameters(includePrivateParameters: false));
        using var aparelho = ECDiffieHellman.Create(ParametrosPublicos(chaveAparelho));
        var segredoEcdh = servidor.DeriveRawSecretAgreement(aparelho.PublicKey);

        var infoChave = new byte[14 + TamanhoChavePublica * 2];
        "WebPush: info\0"u8.CopyTo(infoChave);
        chaveAparelho.CopyTo(infoChave.AsSpan(14));
        chaveServidor.CopyTo(infoChave.AsSpan(14 + TamanhoChavePublica));

        Span<byte> ikm = stackalloc byte[32];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, segredoEcdh, ikm, segredo, infoChave);
        Span<byte> cek = stackalloc byte[16];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, cek, salt, "Content-Encoding: aes128gcm\0"u8);
        Span<byte> nonce = stackalloc byte[12];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, nonce, salt, "Content-Encoding: nonce\0"u8);

        // Um registro só: o conteúdo seguido do delimitador 0x02 do último registro.
        var claro = new byte[conteudo.Length + 1];
        conteudo.CopyTo(claro);
        claro[^1] = 0x02;

        var mensagem = new byte[TamanhoCabecalho + claro.Length + TamanhoTag];
        salt.CopyTo(mensagem);
        BinaryPrimitives.WriteUInt32BigEndian(mensagem.AsSpan(TamanhoSalt), TamanhoRegistro);
        mensagem[TamanhoSalt + 4] = TamanhoChavePublica;
        chaveServidor.CopyTo(mensagem.AsSpan(TamanhoSalt + 5));

        using var aes = new AesGcm(cek, TamanhoTag);
        aes.Encrypt(
            nonce,
            claro,
            mensagem.AsSpan(TamanhoCabecalho, claro.Length),
            mensagem.AsSpan(TamanhoCabecalho + claro.Length));
        return mensagem;
    }

    /// <summary>JWT ES256 do VAPID: `aud` é a origem do serviço de push, `sub` é o contato do operador.</summary>
    public static string CriarJwt(ECDsa chave, string audiencia, string assunto, DateTimeOffset expiraEm)
    {
        using var claims = new MemoryStream();
        using (var writer = new Utf8JsonWriter(claims))
        {
            writer.WriteStartObject();
            writer.WriteString("aud", audiencia);
            writer.WriteNumber("exp", expiraEm.ToUnixTimeSeconds());
            writer.WriteString("sub", assunto);
            writer.WriteEndObject();
        }

        var semAssinatura = $"{Base64Url.EncodeToString("""{"typ":"JWT","alg":"ES256"}"""u8)}.{Base64Url.EncodeToString(claims.ToArray())}";
        // O formato padrão do .NET (r || s, 64 bytes) é o que o JWS exige.
        var assinatura = chave.SignData(Encoding.ASCII.GetBytes(semAssinatura), HashAlgorithmName.SHA256);
        return $"{semAssinatura}.{Base64Url.EncodeToString(assinatura)}";
    }

    /// <summary>Par novo de chaves VAPID, em base64url: a pública (65 bytes) e a privada (32 bytes).</summary>
    public static (string Publica, string Privada) GerarChaves()
    {
        using var chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parametros = chave.ExportParameters(includePrivateParameters: true);
        return (Base64Url.EncodeToString(PontoPublico(parametros)), Base64Url.EncodeToString(parametros.D));
    }

    public static ECDsa ImportarChaveVapid(string publica, string privada)
    {
        if (!TryDecode(publica, out var ponto) || ponto.Length != TamanhoChavePublica || ponto[0] != 0x04
            || !TryDecode(privada, out var d) || d.Length != 32)
        {
            throw new InvalidOperationException("Chaves VAPID inválidas: gere de novo com o comando gerar-vapid.");
        }

        var parametros = ParametrosPublicos(ponto);
        parametros.D = d;
        return ECDsa.Create(parametros);
    }

    public static bool TryDecode(string valor, out byte[] bytes)
    {
        try
        {
            bytes = Base64Url.DecodeFromChars(valor);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    internal static ECParameters ParametrosPublicos(ReadOnlySpan<byte> ponto) => new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = ponto[1..33].ToArray(), Y = ponto[33..65].ToArray() },
    };

    private static byte[] PontoPublico(ECParameters parametros) =>
        [0x04, .. parametros.Q.X!, .. parametros.Q.Y!];
}
