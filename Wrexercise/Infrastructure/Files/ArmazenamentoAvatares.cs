using Microsoft.Extensions.Options;

namespace Wrexercise.Infrastructure.Files;

internal sealed class AvataresOptions
{
    public const string Section = "Avatares";

    public string Diretorio { get; set; } = "/dados/avatares";
}

/// <summary>
/// Guarda as folhas de avatar importadas, no mesmo padrão das evidências: nome aleatório gerado aqui,
/// nunca vindo do cliente, e nada exposto diretamente (docs/arquitetura.md §11).
/// </summary>
internal sealed class ArmazenamentoAvatares(IOptions<AvataresOptions> options)
{
    private readonly string _raiz = Path.GetFullPath(options.Value.Diretorio);

    /// <summary>Lê o fluxo (no máximo o limite da folha), confere o formato e grava com um nome novo.</summary>
    /// <returns>O nome do arquivo gravado, ou <c>null</c> se a folha não segue o formato.</returns>
    public async Task<string?> SalvarAsync(Stream origem, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var lidos = await origem.ReadAsync(buffer, cancellationToken);
            if (lidos == 0)
            {
                break;
            }

            if (memoria.Length + lidos > ValidacaoFolhaAvatar.MaxBytes)
            {
                return null;
            }

            memoria.Write(buffer, 0, lidos);
        }

        var bytes = memoria.ToArray();
        if (!ValidacaoFolhaAvatar.IsValida(bytes))
        {
            return null;
        }

        Directory.CreateDirectory(_raiz);
        var nome = $"{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(_raiz, nome), bytes, cancellationToken);
        return nome;
    }

    /// <returns>O caminho do arquivo, ou <c>null</c> se o nome não é um dos que este armazenamento gera.</returns>
    public string? ResolverCaminho(string nome) => IsNomeValido(nome) ? Path.Combine(_raiz, nome) : null;

    public void ExcluirSeExistir(string nome)
    {
        if (ResolverCaminho(nome) is { } caminho && File.Exists(caminho))
        {
            File.Delete(caminho);
        }
    }

    /// <summary>32 caracteres hexadecimais e <c>.png</c>: nada de separadores nem de <c>..</c>.</summary>
    public static bool IsNomeValido(string nome) =>
        nome.Length == 36
        && nome.EndsWith(".png", StringComparison.Ordinal)
        && nome.AsSpan(0, 32).IndexOfAnyExcept("0123456789abcdef") < 0;
}
