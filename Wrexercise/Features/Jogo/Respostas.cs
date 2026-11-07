namespace Wrexercise.Features.Jogo;

internal sealed record MembroCenaResponse(Guid UsuarioId, string NomeExibicao, string Pose);

internal sealed record CenaResponse(Guid GrupoId, IReadOnlyList<MembroCenaResponse> Membros);

/// <summary>A Energia ganha por origem. A soma é a Energia ganha total.</summary>
internal sealed record EnergiaDetalhamentoResponse(int Atividades, int Bonus, int Metas, int Sequencias, int Desafios);

internal sealed record EnergiaResponse(int Saldo, EnergiaDetalhamentoResponse Detalhamento);
