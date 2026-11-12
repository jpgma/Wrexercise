namespace Wrexercise.Infrastructure.Push;

/// <summary>Chaves VAPID do Web Push. Sem as duas chaves, o push fica desligado e só a lista do app funciona.</summary>
internal sealed class VapidOptions
{
    public const string Section = "Vapid";

    public string? PublicKey { get; set; }

    public string? PrivateKey { get; set; }

    /// <summary>Contato do operador para os serviços de push (`mailto:` ou `https:`).</summary>
    public string Subject { get; set; } = "https://wx.jpgmaraujo.com.br";
}
