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

export type AvisoCarta = { cumpre: boolean; texto: string };

/**
 * Se o treino do formulário cumpre uma carta aceita, e por que não. É só uma orientação antes de
 * postar: repete em poucas linhas a regra de `SituacaoJogada.cs` e usa o fuso do aparelho, e não o
 * do grupo. Quem decide é o servidor, depois que o treino é verificado.
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
  const naoCumpre = (motivo: string): AvisoCarta => ({
    cumpre: false,
    texto: `Não cumpre a carta de ${nome}: ${motivo}.`,
  });

  const inicio = treino.inicio.getTime();
  if (
    Number.isNaN(inicio) ||
    inicio < Date.parse(jogada.enviadaEm) ||
    inicio > Date.parse(jogada.expiraEm)
  )
    return naoCumpre("está fora do prazo");
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
  if (codigos.includes("AntesDas8") && treino.inicio.getHours() >= 8)
    return naoCumpre("não começa antes das 8h");

  const alvo = jogada.duracaoAlvoSegundos;
  if (alvo && treino.duracaoSegundos < alvo) {
    // Na Fusão vale só este treino; fora dela, o servidor soma os treinos do dia.
    return jogada.cartaFusao
      ? naoCumpre(`precisa de ${formatarDuracao(alvo)} ou mais`)
      : {
          cumpre: true,
          texto: `Conta pra carta de ${nome}: o dia precisa somar ${formatarDuracao(alvo)}.`,
        };
  }
  return { cumpre: true, texto: `Esse treino cumpre a carta de ${nome}.` };
}
