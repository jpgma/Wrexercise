namespace Wrexercise.Features.Jogo;

/// <summary>Uma Cutucada que a pessoa enviou.</summary>
internal sealed record CutucadaEnviada(Guid GrupoId, Guid DestinatarioId, DateTimeOffset EnviadaEm);

/// <summary>Uma atividade <c>Verificada</c> de alguém que a pessoa cutucou.</summary>
internal sealed record AtividadeDespertar(
    Guid Id,
    Guid GrupoId,
    Guid UsuarioId,
    DateTimeOffset IniciadaEm,
    DateTimeOffset RegistradaEm);

/// <summary>
/// O bônus Despertador (decisão 0063): quem cutucou ganha Energia quando o colega volta a treinar.
/// Uma cutucada desperta com a primeira atividade verificada do colega no mesmo grupo, em ordem de registro,
/// que foi registrada depois dela e começou até 24 horas depois dela. Cada atividade paga uma vez a quem cutucou.
/// </summary>
internal static class Despertador
{
    public static readonly TimeSpan Janela = TimeSpan.FromHours(24);

    /// <summary>Quantos despertares a pessoa causou: atividades distintas que acordaram alguém que ela cutucou.</summary>
    public static int Contar(IEnumerable<CutucadaEnviada> cutucadas, IReadOnlyList<AtividadeDespertar> atividades)
    {
        var porMembro = atividades
            .GroupBy(a => (a.GrupoId, a.UsuarioId))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.RegistradaEm).ThenBy(a => a.Id).ToList());

        var despertadoras = new HashSet<Guid>();
        foreach (var cutucada in cutucadas)
        {
            if (!porMembro.TryGetValue((cutucada.GrupoId, cutucada.DestinatarioId), out var doMembro))
            {
                continue;
            }

            var primeira = doMembro.FirstOrDefault(a => IsDespertar(cutucada, a));
            if (primeira is not null)
            {
                despertadoras.Add(primeira.Id);
            }
        }

        return despertadoras.Count;
    }

    public static bool IsDespertar(CutucadaEnviada cutucada, AtividadeDespertar atividade) =>
        atividade.GrupoId == cutucada.GrupoId
        && atividade.UsuarioId == cutucada.DestinatarioId
        && atividade.RegistradaEm > cutucada.EnviadaEm
        && atividade.IniciadaEm <= cutucada.EnviadaEm + Janela;
}
