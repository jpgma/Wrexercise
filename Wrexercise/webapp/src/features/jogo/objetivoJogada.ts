import {
  formatarDuracao,
  tipoAtividadeLabels,
  type TipoAtividade,
} from "@/features/atividades";
import { primeiroNome } from "./textosCartas";
import type { CodigoCarta, Jogada } from "./useCartas";

/** O treino que a pessoa está preenchendo no formulário. */
export type TreinoPrevisto = {
  tipo: string | undefined;
  inicio: Date;
  duracaoSegundos: number;
};

/** `falha`: postar esse treino faz a carta falhar, com FADIGA (decisão 0075). */
export type AvisoCarta = { cumpre: boolean; falha: boolean; texto: string };

/** O treino termina até as 08:00 do dia em que começou (decisão 0075). */
function terminaAte8(treino: TreinoPrevisto): boolean {
  const limite = new Date(treino.inicio);
  limite.setHours(8, 0, 0, 0);
  return (
    treino.inicio.getTime() + treino.duracaoSegundos * 1000 <= limite.getTime()
  );
}

/**
 * Se o treino do formulário cumpre uma carta aceita, e por que não. É só uma orientação antes de
 * postar: repete em poucas linhas a regra de `SituacaoJogada.cs` e usa o fuso do aparelho, e não o
 * do grupo. Quem decide é o servidor, depois que o treino é verificado.
 *
 * Só o próximo treino depois do aceite vale (decisão 0075): um treino no prazo que não cumpre faz a
 * carta falhar. Um treino que começou depois que a carta chegou e antes do aceite conta se cumpre e
 * nunca faz falhar (decisão 0076). Um treino fora do prazo não conta e não faz falhar.
 */
export function getAvisoCarta(
  jogada: Jogada,
  treino: TreinoPrevisto,
): AvisoCarta | null {
  const codigos: CodigoCarta[] = [jogada.objetivo];
  if (jogada.cartaFusao) codigos.push(jogada.cartaFusao.codigo);
  // Sem tipo escolhido ainda não dá para falar da VARIEDADE.
  if (codigos.includes("Variedade") && !treino.tipo) return null;

  const nome = primeiroNome(jogada.remetenteNome);
  const inicio = treino.inicio.getTime();
  const antesDoAceite =
    jogada.respondidaEm !== null && inicio < Date.parse(jogada.respondidaEm);
  const naoCumpre = (motivo: string): AvisoCarta =>
    antesDoAceite
      ? {
          cumpre: false,
          falha: false,
          texto: `Não conta pra carta de ${nome}: é de antes do aceite e ${motivo}.`,
        }
      : {
          cumpre: false,
          falha: true,
          texto: `Não cumpre a carta de ${nome}: ${motivo}. Postando, a carta falha e você fica com FADIGA.`,
        };

  if (
    Number.isNaN(inicio) ||
    inicio < Date.parse(jogada.enviadaEm) ||
    inicio > Date.parse(jogada.expiraEm)
  )
    return {
      cumpre: false,
      falha: false,
      texto: `Não conta pra carta de ${nome}: está fora do prazo.`,
    };
  if (
    codigos.includes("Variedade") &&
    jogada.tipoEvitar &&
    treino.tipo === jogada.tipoEvitar
  )
    return naoCumpre(
      `é ${tipoAtividadeLabels[jogada.tipoEvitar as TipoAtividade] ?? jogada.tipoEvitar}`,
    );
  if (
    codigos.includes("FimDeSemana") &&
    treino.inicio.getDay() !== 0 &&
    treino.inicio.getDay() !== 6
  )
    return naoCumpre("não é no fim de semana");
  if (codigos.includes("AntesDas8") && !terminaAte8(treino))
    return naoCumpre("não termina até as 8h");

  const alvo = jogada.duracaoAlvoSegundos;
  if (alvo && treino.duracaoSegundos < alvo) {
    // Na Fusão vale só este treino; fora dela, o servidor soma os treinos do dia dele.
    return jogada.cartaFusao
      ? naoCumpre(`precisa de ${formatarDuracao(alvo)} ou mais`)
      : {
          cumpre: true,
          falha: false,
          texto: `Conta pra carta de ${nome}: até o fim desse dia, os treinos precisam somar ${formatarDuracao(alvo)}.`,
        };
  }
  return {
    cumpre: true,
    falha: false,
    texto: antesDoAceite
      ? `Esse treino é de antes do aceite e cumpre a carta de ${nome}.`
      : `Esse treino cumpre a carta de ${nome}.`,
  };
}
