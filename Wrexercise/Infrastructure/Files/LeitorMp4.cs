using System.Buffers.Binary;
using System.Text;

namespace Wrexercise.Infrastructure.Files;

/// <summary>O que o servidor confere num vídeo (ADR 0058). <c>CodecAudio</c> é nulo quando não há áudio.</summary>
internal sealed record InfoVideo(
    int DuracaoMs,
    int Largura,
    int Altura,
    string CodecVideo,
    string? CodecAudio,
    DateTimeOffset? CriadoEm);

/// <summary>
/// Leitor mínimo das caixas de um MP4 ou MOV, sem biblioteca e sem decodificar a mídia: só a duração
/// (<c>mvhd</c>), as dimensões (<c>tkhd</c>), o tipo de cada faixa (<c>hdlr</c>) e o codec (<c>stsd</c>).
/// A caixa <c>moov</c> pode vir antes ou depois dos dados (<c>mdat</c>): as câmeras de celular costumam
/// gravá-la no fim, e o navegador a encontra com pedidos <c>Range</c>.
/// Qualquer estrutura estranha resulta em <see cref="EvidenciaInvalidaException"/>, nunca em leitura fora do arquivo.
/// </summary>
internal static class LeitorMp4
{
    private const int MaxBytesMoov = 8 * 1024 * 1024;
    private const int MaxBytesFtyp = 1024;
    private const int MaxCaixasNoTopo = 64;
    private const int MaxCaixasPorNivel = 4096;

    private static readonly DateTimeOffset Epoca = new(1904, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CriacaoMinima = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<string> MarcasAceitas =
    [
        "isom", "iso2", "iso3", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "qt  ", "M4V ",
        "3gp4", "3gp5", "3gp6", "mmp4", "MSNV",
    ];

    public static InfoVideo Ler(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw Invalido("O vídeo precisa ser lido com acesso aleatório.");
        }

        var tamanho = stream.Length;
        long posicao = 0;
        InfoVideo? info = null;
        Span<byte> cabecalho = stackalloc byte[16];

        for (var i = 0; posicao < tamanho; i++)
        {
            if (i >= MaxCaixasNoTopo)
            {
                throw Invalido("Caixas demais no topo do arquivo.");
            }

            stream.Position = posicao;
            LerExato(stream, cabecalho[..8]);
            var tamanhoCaixa = (long)BinaryPrimitives.ReadUInt32BigEndian(cabecalho);
            var tipo = Tipo(cabecalho.Slice(4, 4));
            var tamanhoCabecalho = 8;
            if (tamanhoCaixa == 1)
            {
                LerExato(stream, cabecalho.Slice(8, 8));
                tamanhoCaixa = (long)BinaryPrimitives.ReadUInt64BigEndian(cabecalho[8..]);
                tamanhoCabecalho = 16;
            }
            else if (tamanhoCaixa == 0)
            {
                tamanhoCaixa = tamanho - posicao;
            }

            if (tamanhoCaixa < tamanhoCabecalho || tamanhoCaixa > tamanho - posicao)
            {
                throw Invalido("Caixa com tamanho inválido.");
            }

            var tamanhoConteudo = tamanhoCaixa - tamanhoCabecalho;
            if (i == 0)
            {
                if (tipo != "ftyp" || tamanhoConteudo is < 8 or > MaxBytesFtyp)
                {
                    throw Invalido("O arquivo não começa pela caixa ftyp.");
                }

                var ftyp = new byte[tamanhoConteudo];
                LerExato(stream, ftyp);
                ConferirMarcas(ftyp);
            }
            else if (tipo == "moov")
            {
                if (tamanhoConteudo > MaxBytesMoov)
                {
                    throw Invalido("Caixa moov grande demais.");
                }

                if (info is not null)
                {
                    throw Invalido("Mais de uma caixa moov.");
                }

                var moov = new byte[tamanhoConteudo];
                LerExato(stream, moov);
                info = LerMoov(moov);
            }

            posicao += tamanhoCaixa;
        }

        // Percorre o arquivo inteiro: um arquivo cortado no meio dos dados também é recusado.
        return info ?? throw Invalido("Vídeo sem a caixa moov.");
    }

    private static void ConferirMarcas(ReadOnlySpan<byte> ftyp)
    {
        // Marca principal, versão (4 bytes) e as marcas compatíveis, de 4 em 4.
        if (MarcasAceitas.Contains(Tipo(ftyp[..4])))
        {
            return;
        }

        for (var i = 8; i + 4 <= ftyp.Length; i += 4)
        {
            if (MarcasAceitas.Contains(Tipo(ftyp.Slice(i, 4))))
            {
                return;
            }
        }

        throw new EvidenciaInvalidaException("Marca de MP4 não suportada.", CodigosVideo.FormatoNaoSuportado);
    }

    private static InfoVideo LerMoov(ReadOnlySpan<byte> moov)
    {
        long? escala = null;
        long duracao = 0;
        DateTimeOffset? criadoEm = null;
        Faixa? video = null;
        string? codecAudio = null;

        var posicao = 0;
        var contador = 0;
        while (ProximaCaixa(moov, ref posicao, ref contador, out var tipo, out var conteudo))
        {
            if (tipo == "mvhd")
            {
                (escala, duracao, criadoEm) = LerMvhd(conteudo);
            }
            else if (tipo == "trak" && LerTrak(conteudo) is { } faixa)
            {
                if (faixa.Tipo == "vide")
                {
                    video ??= faixa;
                }
                else if (faixa.Tipo == "soun")
                {
                    codecAudio ??= faixa.Codec;
                }
            }
        }

        if (escala is not > 0 || video is null)
        {
            throw Invalido("Vídeo sem duração ou sem faixa de vídeo.");
        }

        var duracaoMs = duracao * 1000 / escala.Value;
        return new InfoVideo(
            (int)Math.Min(duracaoMs, int.MaxValue),
            video.Largura,
            video.Altura,
            video.Codec,
            codecAudio,
            criadoEm);
    }

    private static (long Escala, long Duracao, DateTimeOffset? CriadoEm) LerMvhd(ReadOnlySpan<byte> c)
    {
        var versao1 = c.Length > 0 && c[0] == 1;
        if (c.Length < (versao1 ? 32 : 20))
        {
            throw Invalido("Caixa mvhd curta demais.");
        }

        var criacao = versao1 ? BinaryPrimitives.ReadUInt64BigEndian(c[4..]) : BinaryPrimitives.ReadUInt32BigEndian(c[4..]);
        var escala = BinaryPrimitives.ReadUInt32BigEndian(c[(versao1 ? 20 : 12)..]);
        var duracao = versao1
            ? BinaryPrimitives.ReadUInt64BigEndian(c[24..])
            : BinaryPrimitives.ReadUInt32BigEndian(c[16..]);
        if (duracao > long.MaxValue / 1000)
        {
            throw Invalido("Duração impossível.");
        }

        return (escala, (long)duracao, ParaData(criacao));
    }

    /// <summary>Segundos desde 1904 em UTC. Câmeras sem relógio gravam zero: vira "sem data".</summary>
    private static DateTimeOffset? ParaData(ulong segundos)
    {
        if (segundos == 0 || segundos > 200UL * 365 * 24 * 3600)
        {
            return null;
        }

        var data = Epoca.AddSeconds(segundos);
        return data < CriacaoMinima ? null : data;
    }

    private sealed record Faixa(string Tipo, string Codec, int Largura, int Altura);

    private static Faixa? LerTrak(ReadOnlySpan<byte> trak)
    {
        int largura = 0, altura = 0;
        string? tipo = null, codec = null;

        var posicao = 0;
        var contador = 0;
        while (ProximaCaixa(trak, ref posicao, ref contador, out var nome, out var conteudo))
        {
            if (nome == "tkhd")
            {
                (largura, altura) = LerTkhd(conteudo);
            }
            else if (nome == "mdia")
            {
                (tipo, codec) = LerMdia(conteudo);
            }
        }

        return tipo is null || codec is null ? null : new Faixa(tipo, codec, largura, altura);
    }

    private static (int Largura, int Altura) LerTkhd(ReadOnlySpan<byte> c)
    {
        var versao1 = c.Length > 0 && c[0] == 1;
        var inicio = versao1 ? 88 : 76;
        if (c.Length < inicio + 8)
        {
            throw Invalido("Caixa tkhd curta demais.");
        }

        // Ponto fixo 16.16.
        return (
            (int)(BinaryPrimitives.ReadUInt32BigEndian(c[inicio..]) >> 16),
            (int)(BinaryPrimitives.ReadUInt32BigEndian(c[(inicio + 4)..]) >> 16));
    }

    private static (string? Tipo, string? Codec) LerMdia(ReadOnlySpan<byte> mdia)
    {
        string? tipo = null, codec = null;
        var posicao = 0;
        var contador = 0;
        while (ProximaCaixa(mdia, ref posicao, ref contador, out var nome, out var conteudo))
        {
            if (nome == "hdlr" && conteudo.Length >= 12)
            {
                tipo = Tipo(conteudo.Slice(8, 4));
            }
            else if (nome == "minf")
            {
                codec = Filha(conteudo, "stbl") is { } stbl && Filha(stbl, "stsd") is { } stsd && stsd.Length >= 16
                    ? Tipo(stsd.AsSpan(12, 4))
                    : null;
            }
        }

        return (tipo, codec);
    }

    private static byte[]? Filha(ReadOnlySpan<byte> pai, string procurada)
    {
        var posicao = 0;
        var contador = 0;
        while (ProximaCaixa(pai, ref posicao, ref contador, out var nome, out var conteudo))
        {
            if (nome == procurada)
            {
                return conteudo.ToArray();
            }
        }

        return null;
    }

    private static bool ProximaCaixa(
        ReadOnlySpan<byte> dados,
        ref int posicao,
        ref int contador,
        out string tipo,
        out ReadOnlySpan<byte> conteudo)
    {
        tipo = string.Empty;
        conteudo = default;
        if (posicao + 8 > dados.Length)
        {
            return false;
        }

        if (++contador > MaxCaixasPorNivel)
        {
            throw Invalido("Caixas demais.");
        }

        var tamanho = (long)BinaryPrimitives.ReadUInt32BigEndian(dados[posicao..]);
        tipo = Tipo(dados.Slice(posicao + 4, 4));
        var cabecalho = 8;
        if (tamanho == 1)
        {
            if (posicao + 16 > dados.Length)
            {
                throw Invalido("Caixa truncada.");
            }

            tamanho = (long)Math.Min(BinaryPrimitives.ReadUInt64BigEndian(dados[(posicao + 8)..]), long.MaxValue);
            cabecalho = 16;
        }
        else if (tamanho == 0)
        {
            tamanho = dados.Length - posicao;
        }

        if (tamanho < cabecalho || tamanho > dados.Length - posicao)
        {
            throw Invalido("Caixa com tamanho inválido.");
        }

        conteudo = dados.Slice(posicao + cabecalho, (int)tamanho - cabecalho);
        posicao += (int)tamanho;
        return true;
    }

    private static string Tipo(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

    private static void LerExato(Stream stream, Span<byte> destino)
    {
        try
        {
            stream.ReadExactly(destino);
        }
        catch (EndOfStreamException)
        {
            throw Invalido("Arquivo truncado.");
        }
    }

    private static EvidenciaInvalidaException Invalido(string mensagem) => new(mensagem, CodigosVideo.Invalido);
}
