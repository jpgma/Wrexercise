using System.Buffers.Binary;

namespace Wrexercise.Infrastructure.Files;

/// <summary>
/// Confere o formato público da folha de sprites (docs/avatar-formato.md): PNG RGBA de 8 bits, 256×384,
/// sem animação APNG e com até 64 KB. O navegador já re-codifica a imagem; isto é a segunda barreira.
/// </summary>
internal static class ValidacaoFolhaAvatar
{
    public const int MaxBytes = 64 * 1024;
    public const int Largura = 256;
    public const int Altura = 384;

    private const int TamanhoIhdr = 13;
    private const byte ProfundidadeBits = 8;
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
        && ihdr[8] == ProfundidadeBits
        && ihdr[9] == TipoCorRgba;
}
