namespace Wrexercise.Features.Atividades;

/// <summary>Quando uma foto ou vídeo do autor chega à atividade (ADR 0083).</summary>
internal enum MomentoEvidencia
{
    /// <summary>Ainda vale para a verificação: a primeira evidência sem revisores verifica a atividade.</summary>
    NoPrazo,

    /// <summary>Entra com o selo "Adicionada depois": aparece na atividade, mas não verifica nem muda a comprovação.</summary>
    Depois,

    /// <summary>Não entra mais.</summary>
    Recusada,
}

/// <summary>Regra pura de quando o autor ainda anexa evidências e com que efeito (ADR 0078, 0083).</summary>
internal static class PrazoEvidencia
{
    public static MomentoEvidencia Classificar(string situacao, string tipoComprovacao, bool temRevisores)
    {
        if (situacao == SituacaoVerificacao.Anulada)
        {
            return MomentoEvidencia.Recusada;
        }

        // O treino ao vivo recebe fotos e vídeos enquanto acontece (ADR 0078).
        if (situacao == SituacaoVerificacao.EmAndamento)
        {
            return MomentoEvidencia.NoPrazo;
        }

        // Sem revisores, a primeira evidência já verifica a atividade: as seguintes ainda entram no prazo.
        var noPrazo = tipoComprovacao != TipoComprovacao.RegistroManual
            && (situacao == SituacaoVerificacao.AguardandoFeedback
                || (situacao == SituacaoVerificacao.Verificada && !temRevisores));
        return noPrazo ? MomentoEvidencia.NoPrazo : MomentoEvidencia.Depois;
    }

    /// <summary>O autor vê o botão de mandar fotos e vídeos na página da atividade.</summary>
    public static bool AutorPodeAnexar(bool autor, string situacao) =>
        autor && situacao != SituacaoVerificacao.Anulada;
}
