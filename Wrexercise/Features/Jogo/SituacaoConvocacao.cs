using Wrexercise.Features.Atividades.Contracts;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// Uma atividade que pode cumprir a Convocação: de quem foi convidado, no grupo, do tipo escolhido e com início
/// entre a criação e o prazo. O SQL já filtra isso; a situação e as datas decidem o resto.
/// </summary>
/// <param name="VerificadaEm">O <c>verified_at</c>, quando a atividade foi verificada.</param>
/// <param name="ResolvidaEm">Quando uma revisão a rejeitou, se houver.</param>
internal sealed record AtividadeConvocacao(
    Guid UsuarioId,
    string Situacao,
    DateTimeOffset? VerificadaEm,
    DateTimeOffset? ResolvidaEm = null);

/// <summary>Uma pessoa convidada, com a decisão gravada.</summary>
/// <param name="Decisao"><c>Aceita</c>, <c>Recusada</c>, <c>Desistiu</c> ou <c>null</c> (sem resposta).</param>
/// <param name="RemovidaEm">Quando a pessoa saiu ou foi removida do grupo, se saiu.</param>
internal sealed record ParticipanteConvocacao(
    Guid UsuarioId,
    string NomeExibicao,
    string? Decisao,
    DateTimeOffset? DecididaEm,
    DateTimeOffset? RemovidaEm = null);

/// <summary>Os fatos gravados de uma Convocação. Tudo o mais é calculado em <see cref="SituacaoConvocacao"/>.</summary>
/// <param name="CartaId">A carta Global gasta para criar a Convocação.</param>
/// <param name="Preco">O preço pago pela carta, que volta para o anfitrião em partes.</param>
internal sealed record ConvocacaoFatos(
    Guid Id,
    Guid GrupoId,
    Guid AnfitriaoId,
    Guid CartaId,
    string Codigo,
    int Preco,
    string TipoAtividade,
    DateTimeOffset CriadaEm,
    DateTimeOffset ExpiraEm,
    IReadOnlyList<ParticipanteConvocacao> Participantes,
    IReadOnlyList<AtividadeConvocacao> Atividades);

/// <summary>A situação de uma pessoa na Convocação.</summary>
/// <param name="Em">Quando o resultado final aconteceu; <c>null</c> enquanto não é final.</param>
internal sealed record ResultadoParticipante(ParticipanteConvocacao Participante, string Situacao, DateTimeOffset? Em)
{
    public Guid UsuarioId => Participante.UsuarioId;

    public bool IsFinal => Em is not null;
}

/// <summary>Uma Convocação lida do banco, com as situações já calculadas.</summary>
internal sealed record ConvocacaoCarregada(
    ConvocacaoFatos Fatos,
    string Situacao,
    IReadOnlyList<ResultadoParticipante> Resultados)
{
    public Guid Id => Fatos.Id;

    public ResultadoParticipante? Get(Guid usuarioId) => Resultados.FirstOrDefault(r => r.UsuarioId == usuarioId);

    /// <summary>Quantos convidados concluíram, com o anfitrião.</summary>
    public int Concluidas => Resultados.Count(r => r.Situacao == SituacaoConvocacao.Concluida);

    /// <summary>A Energia que a pessoa ganhou cumprindo o objetivo.</summary>
    public int GetRecompensa(Guid usuarioId) =>
        Get(usuarioId)?.Situacao == SituacaoConvocacao.Concluida ? SituacaoConvocacao.RecompensaParticipante : 0;

    /// <summary>
    /// O que voltou para o anfitrião: o preço dividido entre os outros convidados, uma parte para cada resultado
    /// final. Quem cumpre, recusa ou sai do grupo devolve a parte inteira; quem deixa expirar, falha ou desiste,
    /// só metade (como a devolução da Provocação, decisão 0087). A sobra da divisão vem com o último.
    /// </summary>
    public int Devolucao
    {
        get
        {
            var outros = Resultados.Where(r => r.UsuarioId != Fatos.AnfitriaoId && r.IsFinal).ToList();
            var metades = outros.Count(r => SituacaoConvocacao.IsDevolucaoPelaMetade(r.Situacao));
            return SituacaoConvocacao.GetDevolucao(
                Fatos.Preco, Resultados.Count - 1, outros.Count - metades, metades);
        }
    }

    /// <summary>A Energia desta Convocação para a pessoa: a recompensa e, para o anfitrião, a devolução.</summary>
    public int GetEnergia(Guid usuarioId) =>
        GetRecompensa(usuarioId) + (usuarioId == Fatos.AnfitriaoId ? Devolucao : 0);

    /// <summary>
    /// Quando o convidado ficou com FADIGA, como numa Provocação recebida: recusa, expiração, ou falha (inclusive
    /// sair depois de aceitar). O anfitrião não tem gatilho: a Convocação é dele.
    /// </summary>
    public GatilhoFadiga? GetGatilho(Guid usuarioId)
    {
        if (usuarioId == Fatos.AnfitriaoId || Get(usuarioId) is not { Em: { } em } resultado)
        {
            return null;
        }

        MotivoFadiga? motivo = resultado.Situacao switch
        {
            SituacaoConvocacao.Recusada => MotivoFadiga.Recusa,
            SituacaoConvocacao.Expirada => MotivoFadiga.Expiracao,
            SituacaoConvocacao.Falhou or SituacaoConvocacao.Desistiu => MotivoFadiga.Falha,
            _ => null,
        };
        return motivo is { } m ? new GatilhoFadiga(em, m, ConvocacaoId: Fatos.Id) : null;
    }

    /// <summary>Quando a pessoa cumpriu o objetivo, para a redenção da FADIGA.</summary>
    public DateTimeOffset? GetCumprida(Guid usuarioId) =>
        Get(usuarioId) is { Situacao: SituacaoConvocacao.Concluida, Em: { } em } ? em : null;
}

/// <summary>
/// As regras da Convocação (decisão 0091), calculadas na leitura. Quem aceitou antes do prazo cumpre com um treino
/// verificado do tipo escolhido que começou entre a criação e o prazo. A verificação pode chegar depois do prazo.
/// </summary>
internal static class SituacaoConvocacao
{
    public const int RecompensaParticipante = 30;
    public static readonly TimeSpan Duracao = TimeSpan.FromDays(7);

    // A Convocação.
    public const string Ativa = "Ativa";
    public const string AguardandoVerificacao = "AguardandoVerificacao";
    public const string Encerrada = "Encerrada";

    // A participação de cada pessoa.
    public const string Pendente = "Pendente";
    public const string Participando = "Participando";
    public const string Concluida = "Concluida";
    public const string Recusada = "Recusada";
    public const string Desistiu = "Desistiu";
    public const string Falhou = "Falhou";
    public const string Expirada = "Expirada";
    public const string ForaDoGrupo = "ForaDoGrupo";

    // As decisões gravadas.
    public const string DecisaoAceita = "Aceita";
    public const string DecisaoRecusada = "Recusada";
    public const string DecisaoDesistiu = "Desistiu";

    /// <summary>
    /// Os tipos que o anfitrião pode escolher: todos, menos os genéricos demais para um objetivo comum
    /// (<c>Outro</c> e <c>Esporte</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> Tipos =
    [
        TipoAtividade.Musculacao,
        TipoAtividade.Corrida,
        TipoAtividade.Caminhada,
        TipoAtividade.Ciclismo,
        TipoAtividade.Natacao,
        TipoAtividade.Futebol,
        TipoAtividade.Volei,
        TipoAtividade.Luta,
        TipoAtividade.Yoga,
        TipoAtividade.Calistenia,
    ];

    /// <summary>Situações de uma atividade que ainda podem virar <c>Verificada</c>.</summary>
    private static readonly HashSet<string> SituacoesPendentes =
        ["AguardandoFeedback", "EmRevisao", "Contestada", "EmAndamento"];

    public static bool IsPendente(AtividadeConvocacao atividade) => SituacoesPendentes.Contains(atividade.Situacao);

    /// <summary>Uma atividade que conta ou ainda pode contar: nem rejeitada nem anulada.</summary>
    public static bool IsViva(AtividadeConvocacao atividade) =>
        atividade.Situacao == "Verificada" || IsPendente(atividade);

    public static ConvocacaoCarregada Avaliar(ConvocacaoFatos fatos, DateTimeOffset agora)
    {
        var resultados = fatos.Participantes.Select(p => Avaliar(fatos, p, agora)).ToList();
        var situacao = agora < fatos.ExpiraEm
            ? Ativa
            : resultados.Any(r => r.Situacao == AguardandoVerificacao) ? AguardandoVerificacao : Encerrada;
        return new ConvocacaoCarregada(fatos, situacao, resultados);
    }

    public static ResultadoParticipante Avaliar(ConvocacaoFatos fatos, ParticipanteConvocacao participante, DateTimeOffset agora)
    {
        var atividades = fatos.Atividades.Where(a => a.UsuarioId == participante.UsuarioId).ToList();
        var pendente = atividades.Any(IsPendente);
        var (situacao, em) = AvaliarSemSaida(fatos, participante, atividades, pendente, agora);

        // Quem saiu do grupo antes de ter um resultado final fica de fora, sem FADIGA e sem recompensa.
        if (participante.RemovidaEm is { } removida && (em is null || em > removida))
        {
            return new ResultadoParticipante(participante, ForaDoGrupo, removida);
        }

        return new ResultadoParticipante(participante, situacao, em);
    }

    private static (string Situacao, DateTimeOffset? Em) AvaliarSemSaida(
        ConvocacaoFatos fatos,
        ParticipanteConvocacao participante,
        List<AtividadeConvocacao> atividades,
        bool pendente,
        DateTimeOffset agora)
    {
        if (participante is { Decisao: DecisaoAceita, DecididaEm: { } aceita } && aceita < fatos.ExpiraEm)
        {
            // Um treino de antes do aceite também conta (como na decisão 0076). Só um conclui: o primeiro verificado.
            var verificada = atividades
                .Where(a => a.Situacao == "Verificada" && a.VerificadaEm is not null)
                .Select(a => a.VerificadaEm!.Value)
                .DefaultIfEmpty()
                .Min();
            if (verificada != default)
            {
                return (Concluida, verificada > aceita ? verificada : aceita);
            }
        }

        switch (participante.Decisao)
        {
            case DecisaoRecusada:
                return (Recusada, participante.DecididaEm);
            case DecisaoDesistiu:
                return (Desistiu, participante.DecididaEm);
        }

        if (agora < fatos.ExpiraEm)
        {
            return participante.Decisao is null
                ? (Pendente, null)
                : (pendente ? AguardandoVerificacao : Participando, null);
        }

        if (participante.Decisao is null)
        {
            return (Expirada, fatos.ExpiraEm);
        }

        if (pendente)
        {
            return (AguardandoVerificacao, null);
        }

        // A falha acontece no prazo, ou quando o último treino que ainda podia contar foi rejeitado.
        var resolvida = atividades.Select(a => a.ResolvidaEm).OfType<DateTimeOffset>().DefaultIfEmpty(fatos.ExpiraEm).Max();
        return (Falhou, resolvida > fatos.ExpiraEm ? resolvida : fatos.ExpiraEm);
    }

    /// <summary>Resultados que devolvem só metade da parte ao anfitrião: expirar, falhar ou desistir.</summary>
    public static bool IsDevolucaoPelaMetade(string situacao) => situacao is Expirada or Falhou or Desistiu;

    /// <summary>
    /// A devolução ao anfitrião: a parte, <c>floor(preço / outros)</c>, por resultado final inteiro e metade dela
    /// (arredondada para baixo) por resultado pela metade. Com todos os outros resolvidos, a sobra da divisão
    /// também volta: o preço inteiro, menos o que ficou nas metades.
    /// </summary>
    public static int GetDevolucao(int preco, int outros, int inteiras, int metades)
    {
        if (outros <= 0 || inteiras + metades <= 0)
        {
            return 0;
        }

        var parte = preco / outros;
        return inteiras + metades >= outros
            ? preco - metades * (parte - parte / 2)
            : inteiras * parte + metades * (parte / 2);
    }
}
