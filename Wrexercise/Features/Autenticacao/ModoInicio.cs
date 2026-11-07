namespace Wrexercise.Features.Autenticacao;

/// <summary>Modo da tela inicial da conta (ver <c>docs/gamificacao.md</c> §2).</summary>
internal static class ModoInicio
{
    public const string App = "App";
    public const string Jogo = "Jogo";

    public static bool IsValid(string? modo) => modo is App or Jogo;
}
