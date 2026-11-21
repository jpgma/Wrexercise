import { prepararEvidencia, type EvidenciaPreparada } from "./comprimirImagem";
import {
  analisarVideo,
  duracaoMaximaS,
  duracaoMinimaS,
  prepararVideo,
  VideoNaoProcessadoError,
} from "./prepararVideo";

/**
 * Prepara uma foto ou um vídeo inteiro, sem recorte: a foto é comprimida e o vídeo, de 2 segundos a 1 minuto,
 * reempacotado. Serve para o que sobe na hora (treino ao vivo, foto depois do envio, comentário).
 */
export async function prepararMidia(
  arquivo: File,
  onProgresso: (fracao: number) => void = () => undefined,
): Promise<EvidenciaPreparada> {
  if (!arquivo.type.startsWith("video/")) return prepararEvidencia(arquivo);
  const analise = await analisarVideo(arquivo);
  if (analise.duracao < duracaoMinimaS) {
    throw new VideoNaoProcessadoError(
      "O vídeo precisa ter pelo menos 2 segundos.",
    );
  }
  if (analise.duracao > duracaoMaximaS + 0.5) {
    throw new VideoNaoProcessadoError("Envie vídeos de até 1 minuto.");
  }
  return prepararVideo(
    arquivo,
    analise,
    { inicio: 0, fim: analise.duracao },
    { onProgresso },
  );
}
