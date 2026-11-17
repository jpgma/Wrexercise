import { type EnfeiteBloqueado, useMeuAvatar } from "./useAvatares";

/** A conquista de cada tipo de atividade (`ConquistasUsuario.cs`). */
const conquistaDoTipo: Record<string, string> = {
  Musculacao: "RatoDeAcademia",
  Corrida: "Corredor",
  Caminhada: "Andarilho",
  Ciclismo: "Ciclista",
  Natacao: "Peixe",
  Esporte: "Boleiro",
  Yoga: "Zen",
  Calistenia: "PesoDoCorpo",
};

export type TreinoPostado = {
  tipo: string;
  inicio: Date;
  /** O treino já saiu verificado (por exemplo, sem revisores). */
  verificado: boolean;
};

/**
 * O Enfeite bloqueado de que este treino mais aproxima a pessoa, para a tela de "registrado". Só as
 * conquistas que contam por atividade: o tipo, o fim de semana, antes das 7h e a partir das 20h. As
 * conquistas só contam treino verificado, então o texto diz "quando o grupo verificar". É uma
 * orientação, com o fuso do aparelho; o Perfil mostra a contagem real.
 */
export function getAvanco(
  treino: TreinoPostado,
  bloqueados: EnfeiteBloqueado[],
  comEnfeite: boolean,
): string | null {
  const hora = treino.inicio.getHours();
  const dia = treino.inicio.getDay();
  const conquistas = [
    conquistaDoTipo[treino.tipo],
    dia === 0 || dia === 6 ? "FimDeSemana" : undefined,
    hora < 7 ? "Madrugador" : undefined,
    hora >= 20 ? "Noturno" : undefined,
  ];
  const [maisPerto] = bloqueados
    .filter((e) => conquistas.includes(e.conquista))
    .sort((a, b) => b.atual / b.alvo - a.atual / a.alvo);
  if (!maisPerto) return null;

  const nome = comEnfeite ? maisPerto.nome : maisPerto.tituloConquista;
  const depois = maisPerto.atual + 1;
  const quando = treino.verificado
    ? ""
    : " quando o grupo verificar esse treino";
  return depois >= maisPerto.alvo
    ? `${nome}: ${treino.verificado ? "liberou com esse treino!" : "libera quando o grupo verificar esse treino!"}`
    : `${nome}: ${depois}/${maisPerto.alvo}${quando}.`;
}

/**
 * Para o formulário de treino: devolve a função que descreve o avanço, com os Enfeites bloqueados
 * lidos antes do envio.
 */
export function useAvancoEnfeite(comEnfeite: boolean) {
  const bloqueados = useMeuAvatar().data?.bloqueados;
  return (treino: TreinoPostado) =>
    bloqueados ? getAvanco(treino, bloqueados, comEnfeite) : null;
}
