namespace Wrexercise.UnitTests.Architecture;

/// <summary>
/// O Percurso (ADR 0084) nunca pontua: a distância não entra no progresso, na Energia nem na classificação. Só a
/// Cena (a distância no Foco) e as conquistas de distância, que são só reconhecimento, leem a tabela.
/// </summary>
public class PercursoNaoPontuaTests
{
    private static readonly string[] Permitidos =
    [
        Path.Combine("Jogo", "ObterCena.cs"),
        Path.Combine("Usuarios", "Contracts", "ConquistasDoUsuario.cs"),
    ];

    [Fact]
    public void SoAAtividadeEAsLeiturasPermitidasUsamOPercurso()
    {
        var violacoes = Directory
            .EnumerateFiles(RepositoryPaths.FeaturesDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(arquivo => Path.GetRelativePath(RepositoryPaths.FeaturesDirectory, arquivo))
            .Where(relativo => !relativo.StartsWith("Atividades" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(relativo => !Permitidos.Contains(relativo))
            .Where(relativo => File.ReadAllText(Path.Combine(RepositoryPaths.FeaturesDirectory, relativo))
                .Contains("percursos", StringComparison.Ordinal))
            .ToList();

        Assert.True(violacoes.Count == 0, string.Join(Environment.NewLine, violacoes));
    }
}
