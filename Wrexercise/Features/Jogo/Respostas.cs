namespace Wrexercise.Features.Jogo;

internal sealed record MembroCenaResponse(Guid UsuarioId, string NomeExibicao, string Pose);

internal sealed record CenaResponse(Guid GrupoId, IReadOnlyList<MembroCenaResponse> Membros);
