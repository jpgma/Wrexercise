namespace Wrexercise.Features.Atividades.Contracts;

/// <summary>
/// Tipos de <c>Aviso</c> que o grupo vê numa atividade (ADR 0052). Reacoes os usa para não verificar
/// sozinha, ao fim da janela, uma atividade com aviso.
/// </summary>
internal static class TipoAviso
{
    public const string FotoForaDoPeriodo = "FotoForaDoPeriodo";
    public const string FotoSemData = "FotoSemData";
    public const string FotoParecida = "FotoParecida";
    public const string RegistroTardio = "RegistroTardio";

    // FotoForaDoPeriodo, FotoParecida e RegistroTardio impedem a verificação automática ao fim da janela.
    // FotoSemData só informa: fotos repassadas por aplicativos de mensagem quase nunca têm data.
}
