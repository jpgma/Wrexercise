using System.Buffers.Binary;
using System.Text;

namespace Wrexercise.IntegrationTests.Infrastructure;

/// <summary>MP4 mínimo, só com as caixas que o LeitorMp4 lê. Não toca: serve para testar a estrutura.</summary>
internal static class Mp4Teste
{
    public static byte[] Criar(
        int duracaoMs = 10_000,
        int largura = 1280,
        int altura = 720,
        string codecVideo = "avc1",
        string? codecAudio = "mp4a",
        bool moovNoFim = false,
        string marca = "isom",
        uint segundosDesde1904 = 0,
        bool semVideo = false,
        int variante = 0)
    {
        var ftyp = Caixa("ftyp", [.. Ascii(marca), 0, 0, 0, 0, .. Ascii(marca)]);
        List<byte[]> filhas = [Mvhd(duracaoMs, segundosDesde1904)];
        if (!semVideo)
        {
            filhas.Add(Trak("vide", codecVideo, largura, altura));
        }

        if (codecAudio is not null)
        {
            filhas.Add(Trak("soun", codecAudio, 0, 0));
        }

        var moov = Caixa("moov", [.. filhas.SelectMany(f => f)]);
        var mdat = Caixa("mdat", [.. Enumerable.Repeat((byte)variante, 64)]);
        return moovNoFim ? [.. ftyp, .. mdat, .. moov] : [.. ftyp, .. moov, .. mdat];
    }

    public static byte[] Caixa(string tipo, byte[] conteudo)
    {
        var caixa = new byte[8 + conteudo.Length];
        BinaryPrimitives.WriteUInt32BigEndian(caixa, (uint)caixa.Length);
        Ascii(tipo).CopyTo(caixa, 4);
        conteudo.CopyTo(caixa, 8);
        return caixa;
    }

    private static byte[] Mvhd(int duracaoMs, uint criacao)
    {
        var c = new byte[100];
        BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(4), criacao);
        BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(12), 1000);
        BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(16), (uint)duracaoMs);
        return Caixa("mvhd", c);
    }

    private static byte[] Trak(string handler, string codec, int largura, int altura)
    {
        var tkhd = new byte[84];
        BinaryPrimitives.WriteUInt32BigEndian(tkhd.AsSpan(76), (uint)largura << 16);
        BinaryPrimitives.WriteUInt32BigEndian(tkhd.AsSpan(80), (uint)altura << 16);

        var hdlr = new byte[25];
        Ascii(handler).CopyTo(hdlr, 8);

        var stsd = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(stsd.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(stsd.AsSpan(8), 16);
        Ascii(codec).CopyTo(stsd, 12);

        var minf = Caixa("minf", Caixa("stbl", Caixa("stsd", stsd)));
        var mdia = Caixa("mdia", [.. Caixa("hdlr", hdlr), .. minf]);
        return Caixa("trak", [.. Caixa("tkhd", tkhd), .. mdia]);
    }

    private static byte[] Ascii(string texto) => Encoding.Latin1.GetBytes(texto);
}
