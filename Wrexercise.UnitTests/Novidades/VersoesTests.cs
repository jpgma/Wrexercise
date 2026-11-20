using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wrexercise.UnitTests.Architecture;

namespace Wrexercise.UnitTests.Novidades;

/// <summary>
/// As regras de escrita das Novidades (decisão 0077, "Versões e novidades" no AGENTS.md). O frontend não
/// tem executor de testes, então a verificação do <c>versoes.json</c> mora aqui.
/// </summary>
public partial class VersoesTests
{
    private const int MaximoCaracteres = 80;
    private const int MaximoNovidades = 6;

    private static readonly string Arquivo = Path.Combine(
        RepositoryPaths.Root, "Wrexercise", "webapp", "src", "features", "novidades", "versoes.json");

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex Semver();

    private sealed record Entrada(string Versao, string Data, IReadOnlyList<string> Novidades);

    private static List<Entrada> Ler()
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(Arquivo));
        return documento.RootElement.EnumerateArray()
            .Select(item => new Entrada(
                item.GetProperty("versao").GetString() ?? string.Empty,
                item.GetProperty("data").GetString() ?? string.Empty,
                item.GetProperty("novidades").EnumerateArray().Select(n => n.GetString() ?? string.Empty).ToList()))
            .ToList();
    }

    private static Version ParaVersion(string versao) => Version.Parse(versao);

    [Fact]
    public void Versoes_TemPeloMenosUma()
    {
        Assert.NotEmpty(Ler());
    }

    [Fact]
    public void Versoes_SeguemXYZ_EmOrdemDecrescente_SemRepeticao()
    {
        var entradas = Ler();

        foreach (var entrada in entradas)
        {
            Assert.True(Semver().IsMatch(entrada.Versao), $"Versão fora do formato X.Y.Z: \"{entrada.Versao}\".");
        }

        for (var i = 1; i < entradas.Count; i++)
        {
            Assert.True(
                ParaVersion(entradas[i - 1].Versao) > ParaVersion(entradas[i].Versao),
                $"As versões vão da mais nova para a mais antiga, sem repetir: {entradas[i - 1].Versao} antes de {entradas[i].Versao}.");
        }
    }

    [Fact]
    public void Datas_SaoIso_EAcompanhamAsVersoes()
    {
        var entradas = Ler();
        var datas = entradas
            .Select(entrada =>
            {
                Assert.True(
                    DateOnly.TryParseExact(entrada.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data),
                    $"Data fora do formato AAAA-MM-DD na versão {entrada.Versao}: \"{entrada.Data}\".");
                return data;
            })
            .ToList();

        for (var i = 1; i < datas.Count; i++)
        {
            Assert.True(
                datas[i - 1] >= datas[i],
                $"A versão {entradas[i - 1].Versao} tem data anterior à da {entradas[i].Versao}.");
        }
    }

    [Fact]
    public void Novidades_SaoTopicosCurtos()
    {
        foreach (var entrada in Ler())
        {
            Assert.True(
                entrada.Novidades.Count is >= 1 and <= MaximoNovidades,
                $"A versão {entrada.Versao} precisa de 1 a {MaximoNovidades} novidades.");

            foreach (var novidade in entrada.Novidades)
            {
                Assert.False(string.IsNullOrWhiteSpace(novidade), $"Novidade vazia na versão {entrada.Versao}.");
                Assert.True(novidade == novidade.Trim(), $"Espaço sobrando em \"{novidade}\".");
                Assert.True(
                    novidade.Length <= MaximoCaracteres,
                    $"Novidade com mais de {MaximoCaracteres} caracteres na versão {entrada.Versao}: \"{novidade}\".");
                Assert.DoesNotContain('\n', novidade);
                Assert.False(novidade.EndsWith('.'), $"Sem ponto final: \"{novidade}\".");
            }
        }
    }
}
