namespace Wrexercise.Infrastructure.Push;

/// <summary>
/// Subcomando "gerar-vapid": imprime um par novo de chaves no formato do .env e encerra.
/// O deploy/remote-termux.sh o usa na primeira implantação. Trocar as chaves desfaz todas as inscrições.
/// </summary>
internal static class GerarVapidCommand
{
    public static int Run()
    {
        var (publica, privada) = WebPushCriptografia.GerarChaves();
        Console.WriteLine($"VAPID_PUBLIC_KEY={publica}");
        Console.WriteLine($"VAPID_PRIVATE_KEY={privada}");
        return 0;
    }
}
