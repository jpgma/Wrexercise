using System.Buffers.Binary;
using System.IO.Compression;

namespace Wrexercise.UnitTests.Infrastructure.Files;

/// <summary>
/// Monta PNGs de teste para a folha de avatar. Também é compilado no projeto de testes de integração
/// (ver Wrexercise.IntegrationTests.csproj).
/// </summary>
internal static class PngFolha
{
    private static readonly byte[] Assinatura = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <param name="tipoCor">6 = RGBA, 3 = paleta indexada, 2 = RGB.</param>
    /// <param name="blocoExtra">Um bloco a inserir depois do IHDR, como o <c>acTL</c> de um APNG.</param>
    /// <param name="bytesExtras">Dados (num bloco <c>tEXt</c>) para estourar o limite de tamanho.</param>
    public static byte[] Criar(
        int largura = 432,
        int altura = 2352,
        byte tipoCor = 6,
        byte profundidade = 8,
        string? blocoExtra = null,
        int bytesExtras = 0,
        bool comIend = true,
        int lixoNoFim = 0)
    {
        using var saida = new MemoryStream();
        saida.Write(Assinatura);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)largura);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)altura);
        ihdr[8] = profundidade;
        ihdr[9] = tipoCor;
        EscreverBloco(saida, "IHDR", ihdr);

        if (tipoCor == 3)
        {
            // Paleta de duas cores; o índice 0 é transparente.
            EscreverBloco(saida, "PLTE", new byte[6]);
            EscreverBloco(saida, "tRNS", [0]);
        }

        if (blocoExtra is not null)
        {
            EscreverBloco(saida, blocoExtra, new byte[8]);
        }

        if (bytesExtras > 0)
        {
            EscreverBloco(saida, "tEXt", new byte[bytesExtras]);
        }

        EscreverBloco(saida, "IDAT", Comprimir(largura, altura, BytesPorLinha(largura, tipoCor, profundidade)));
        if (comIend)
        {
            EscreverBloco(saida, "IEND", []);
        }

        saida.Write(new byte[lixoNoFim]);
        return saida.ToArray();
    }

    private static int BytesPorLinha(int largura, byte tipoCor, byte profundidade)
    {
        var canais = tipoCor switch { 6 => 4, 2 => 3, _ => 1 };
        return ((largura * canais * profundidade) + 7) / 8;
    }

    private static byte[] Comprimir(int largura, int altura, int bytesPorLinha)
    {
        using var saida = new MemoryStream();
        using (var zlib = new ZLibStream(saida, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            // Cada linha: o byte do filtro (0) e os pixels, todos transparentes.
            var linha = new byte[1 + bytesPorLinha];
            for (var y = 0; y < altura; y++)
            {
                zlib.Write(linha);
            }
        }

        return saida.ToArray();
    }

    private static void EscreverBloco(Stream saida, string tipo, byte[] dados)
    {
        var tipoBytes = System.Text.Encoding.ASCII.GetBytes(tipo);
        var tamanho = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(tamanho, (uint)dados.Length);
        saida.Write(tamanho);
        saida.Write(tipoBytes);
        saida.Write(dados);

        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(tipoBytes, dados));
        saida.Write(crc);
    }

    private static uint Crc32(byte[] tipo, byte[] dados)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in tipo.Concat(dados))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
