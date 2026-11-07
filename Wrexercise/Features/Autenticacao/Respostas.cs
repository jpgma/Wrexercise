namespace Wrexercise.Features.Autenticacao;

internal sealed record UsuarioResponse(Guid Id, string NomeExibicao, string Email, string ModoInicio);

internal sealed record SessaoResponse(UsuarioResponse Usuario);
