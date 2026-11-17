namespace Wrexercise.Features.Usuarios;

/// <param name="Atual">O progresso da pessoa, limitado a <paramref name="Alvo"/>.</param>
/// <param name="Falta">O que falta para liberar, por extenso ("Faltam 2 treinos no fim de semana"); nulo se liberada.</param>
internal sealed record ConquistaResponse(
    string Codigo,
    string Titulo,
    string Descricao,
    int Atual,
    int Alvo,
    bool Liberada,
    string? Falta = null);

internal sealed record SemanaConsistenciaResponse(DateOnly Inicio, int SessoesVerificadas, int SessoesProvisorias);

/// <summary>O próprio perfil cruza todos os grupos, por isso só o dono o vê.</summary>
internal sealed record MeuPerfilResponse(
    DateTimeOffset MembroDesde,
    int TotalTreinos,
    long TotalSegundos,
    int MelhorSequencia,
    IReadOnlyList<SemanaConsistenciaResponse> Semanas,
    IReadOnlyList<ConquistaResponse> Conquistas);

internal sealed record SemanaMembroResponse(
    int Numero,
    DateOnly Inicio,
    int SessoesVerificadas,
    int SessoesProvisorias,
    bool Participou);

/// <param name="SemanasDisputadas">Semanas já iniciadas que contam para o membro.</param>
internal sealed record DesafioMembroResponse(
    Guid Id,
    string Nome,
    string FusoHorario,
    int SessoesPorSemana,
    int SessoesVerificadas,
    int SessoesProvisorias,
    bool EmAndamento,
    bool ContaNestaSemana,
    int SemanasCumpridas,
    int SemanasDisputadas,
    int SequenciaSemanas,
    IReadOnlyList<SemanaMembroResponse> Semanas);

/// <summary>Só dados deste grupo: nada de e-mail, conquistas ou treinos de outros grupos.</summary>
internal sealed record PerfilMembroResponse(
    Guid UsuarioId,
    string NomeExibicao,
    string Papel,
    DateTimeOffset JoinedAt,
    int TotalTreinos,
    long TotalSegundos,
    DesafioMembroResponse? Desafio);
