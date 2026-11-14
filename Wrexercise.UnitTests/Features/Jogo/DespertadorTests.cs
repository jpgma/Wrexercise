using Wrexercise.Features.Jogo;

namespace Wrexercise.UnitTests.Features.Jogo;

public class DespertadorTests
{
    private static readonly DateTimeOffset Cutucou = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Grupo = Guid.NewGuid();
    private static readonly Guid Bia = Guid.NewGuid();

    private static CutucadaEnviada Cutucada(double horas = 0, Guid? destinatario = null, Guid? grupo = null) =>
        new(grupo ?? Grupo, destinatario ?? Bia, Cutucou.AddHours(horas));

    private static AtividadeDespertar Atividade(
        double inicioHoras,
        double registroHoras,
        Guid? usuario = null,
        Guid? grupo = null) =>
        new(Guid.NewGuid(), grupo ?? Grupo, usuario ?? Bia, Cutucou.AddHours(inicioHoras), Cutucou.AddHours(registroHoras));

    [Fact]
    public void SemAtividade_NaoDesperta() => Assert.Equal(0, Despertador.Contar([Cutucada()], []));

    [Fact]
    public void AtividadeDentroDe24Horas_Desperta() =>
        Assert.Equal(1, Despertador.Contar([Cutucada()], [Atividade(inicioHoras: 5, registroHoras: 6)]));

    [Fact]
    public void ComecaNoLimiteDe24Horas_Desperta() =>
        Assert.Equal(1, Despertador.Contar([Cutucada()], [Atividade(inicioHoras: 24, registroHoras: 25)]));

    [Fact]
    public void ComecaDepoisDe24Horas_NaoDesperta() =>
        Assert.Equal(0, Despertador.Contar([Cutucada()], [Atividade(inicioHoras: 25, registroHoras: 26)]));

    [Fact]
    public void RegistradaAntesDaCutucada_NaoDesperta() =>
        Assert.Equal(0, Despertador.Contar([Cutucada()], [Atividade(inicioHoras: -3, registroHoras: -1)]));

    [Fact]
    public void TreinouAntesERegistrouDepois_Desperta() =>
        Assert.Equal(1, Despertador.Contar([Cutucada()], [Atividade(inicioHoras: -2, registroHoras: 1)]));

    [Fact]
    public void OutroGrupoOuOutroMembro_NaoDesperta() =>
        Assert.Equal(0, Despertador.Contar(
            [Cutucada()],
            [Atividade(1, 2, grupo: Guid.NewGuid()), Atividade(1, 2, usuario: Guid.NewGuid())]));

    [Fact]
    public void VariasCutucadasNaMesmaAtividade_PagamUmaVez() =>
        Assert.Equal(1, Despertador.Contar(
            [Cutucada(0), Cutucada(-20)],
            [Atividade(inicioHoras: 2, registroHoras: 3)]));

    [Fact]
    public void SoAPrimeiraAtividadeDesperta() =>
        Assert.Equal(1, Despertador.Contar(
            [Cutucada()],
            [Atividade(inicioHoras: 10, registroHoras: 11), Atividade(inicioHoras: 2, registroHoras: 3)]));

    [Fact]
    public void DoisColegasAcordados_DoisDespertares()
    {
        var caio = Guid.NewGuid();
        Assert.Equal(2, Despertador.Contar(
            [Cutucada(), Cutucada(destinatario: caio)],
            [Atividade(1, 2), Atividade(1, 2, usuario: caio)]));
    }
}
