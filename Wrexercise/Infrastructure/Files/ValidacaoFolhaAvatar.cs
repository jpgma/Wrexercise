using System.Buffers.Binary;

namespace Wrexercise.Infrastructure.Files;

/// <summary>
/// Confere o formato público da folha de sprites em 8 direções (docs/avatar-formato.md §6): PNG de 432×2352,
/// RGBA de 8 bits ou com paleta indexada, sem animação APNG e com até 1000 KB. O conteúdo não é conferido:
/// cada pessoa desenha o que quiser. O navegador já conferiu o tamanho; isto é a segunda barreira.
/// </summary>
internal static class ValidacaoFolhaAvatar
{
    // Abaixo de 1 MiB, para caber com o envelope multipart no limite do Kestrel (Program.cs).
    public const int MaxBytes = 1000 * 1024;
    public const int Largura = 432;
    public const int Altura = 2352;

    private const int TamanhoIhdr = 13;
    private const byte TipoCorIndexada = 3;
    private const byte TipoCorRgba = 6;

    private static ReadOnlySpan<byte> Assinatura => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsValida(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxBytes || !bytes.StartsWith(Assinatura))
        {
            return false;
        }

        var posicao = Assinatura.Length;
        var primeiro = true;
        var temDados = false;

        // Cada bloco: 4 bytes de tamanho, 4 de tipo, os dados e 4 de CRC.
        while (posicao + 12 <= bytes.Length)
        {
            var tamanho = BinaryPrimitives.ReadUInt32BigEndian(bytes[posicao..]);
            var tipo = bytes.Slice(posicao + 4, 4);
            if (tamanho > (uint)(bytes.Length - posicao - 12))
            {
                return false;
            }

            var dados = bytes.Slice(posicao + 8, (int)tamanho);
            if (primeiro)
            {
                if (!tipo.SequenceEqual("IHDR"u8) || !IsCabecalhoValido(dados))
                {
                    return false;
                }

                primeiro = false;
            }
            else if (tipo.SequenceEqual("acTL"u8))
            {
                return false;
            }
            else if (tipo.SequenceEqual("IDAT"u8))
            {
                temDados = true;
            }
            else if (tipo.SequenceEqual("IEND"u8))
            {
                // O arquivo termina no IEND, sem nada escondido depois.
                return temDados && posicao + 12 == bytes.Length;
            }

            posicao += 12 + (int)tamanho;
        }

        return false;
    }

    private static bool IsCabecalhoValido(ReadOnlySpan<byte> ihdr) =>
        ihdr.Length == TamanhoIhdr
        && BinaryPrimitives.ReadUInt32BigEndian(ihdr) == Largura
        && BinaryPrimitives.ReadUInt32BigEndian(ihdr[4..]) == Altura
        && IsCorAceita(tipoCor: ihdr[9], profundidade: ihdr[8]);

    // Blocos de metadados (tEXt e afins) podem vir junto: são inofensivos, porque a folha é servida como
    // image/png com nosniff e só para quem divide um grupo com a pessoa.
    private static bool IsCorAceita(byte tipoCor, byte profundidade) => tipoCor switch
    {
        TipoCorRgba => profundidade == 8,
        TipoCorIndexada => profundidade is 1 or 2 or 4 or 8,
        _ => false,
    };
}
