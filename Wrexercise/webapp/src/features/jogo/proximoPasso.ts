import {
  formatarPrazoCurto,
  getLimiteEnvio,
  getObjetivoCurto,
  primeiroNome,
} from "./textosCartas";
import type { Jogada, Loja } from "./useCartas";

/**
 * A melhor coisa a fazer agora no Jogo, numa frase, para a linha da Mão fora do Foco: responder uma
 * carta, treinar para cumprir uma, jogar, comprar ou treinar para ganhar Energia. `null` quando não
 * há o que sugerir.
 */
export function getProximoPasso(jogadas: Jogada[], loja: Loja): string | null {
  const recebidas = jogadas
    .filter((j) => j.recebida)
    .sort((a, b) => Date.parse(a.expiraEm) - Date.parse(b.expiraEm));

  const esperando = recebidas.find((j) => j.situacao === "Enviada");
  if (esperando)
    return `Responda a carta de ${primeiroNome(esperando.remetenteNome)} até ${formatarPrazoCurto(esperando.expiraEm)}.`;

  const valendo = recebidas.find((j) => j.situacao === "Aceita");
  if (valendo)
    return `Treine pra cumprir a carta de ${primeiroNome(valendo.remetenteNome)}: ${getObjetivoCurto(valendo)}.`;

  const podeJogar =
    !getLimiteEnvio(jogadas) && loja.mao.some((c) => c.tipo === "Provocacao");
  if (podeJogar) return "Toque num colega pra jogar uma carta.";

  // A carta Global se joga tocando nela, não num colega (decisão 0091).
  if (loja.mao.some((c) => c.tipo === "Global"))
    return "Toque no TODO MUNDO pra convocar a galera.";

  if (loja.mao.length >= loja.limiteMao) return null;
  const vazia = loja.mao.length === 0 ? "Sua Mão está vazia. " : "";
  const maisBarata = Math.min(...loja.catalogo.map((c) => c.preco));
  return loja.saldo >= maisBarata
    ? `${vazia}Você tem EN pra uma carta. Passe na Loja.`
    : `${vazia}Treine pra ganhar EN e comprar cartas.`;
}
