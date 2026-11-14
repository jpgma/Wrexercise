using System.Text;

namespace Wrexercise.Features.Jogo;

/// <summary>O texto livre da Cutucada (decisão 0064): uma linha curta, sem caracteres de controle.</summary>
internal static class ValidacaoCutucada
{
    public const int TextoMaximo = 80;

    /// <summary>
    /// Junta espaços e quebras de linha num espaço só, tira caracteres de controle e as pontas.
    /// Devolve <c>null</c> quando sobra nada ou o texto passa de <see cref="TextoMaximo"/> caracteres.
    /// </summary>
    public static string? NormalizeTexto(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        var resultado = new StringBuilder(texto.Length);
        var espaco = false;
        foreach (var caractere in texto)
        {
            if (char.IsWhiteSpace(caractere))
            {
                espaco = resultado.Length > 0;
                continue;
            }

            if (char.IsControl(caractere))
            {
                continue;
            }

            if (espaco)
            {
                resultado.Append(' ');
                espaco = false;
            }

            resultado.Append(caractere);
        }

        var normalizado = resultado.ToString();
        // Conta como o PostgreSQL (char_length): pontos de código, não unidades UTF-16. Emojis contam como um.
        var tamanho = normalizado.EnumerateRunes().Count();
        return tamanho is > 0 and <= TextoMaximo ? normalizado : null;
    }
}
