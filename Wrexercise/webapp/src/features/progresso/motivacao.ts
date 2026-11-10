export type Motivacao = {
  /** Curto, cabe no título do cabeçalho. */
  titulo: string;
  detalhe: string;
};

type Situacao = {
  verificadas: number;
  provisorias: number;
  meta: number;
  /** Dias até a semana fechar, contando hoje: 7 na segunda, 1 no domingo. */
  diasRestantes: number;
  contaNestaSemana: boolean;
};

const plural = (n: number, singular: string, varios: string) =>
  `${n} ${n === 1 ? singular : varios}`;

/** Texto de progresso da semana. Um treino conta no máximo uma vez por dia. */
export function getMotivacao({
  verificadas,
  provisorias,
  meta,
  diasRestantes: dias,
  contaNestaSemana,
}: Situacao): Motivacao {
  if (verificadas >= meta) {
    return { titulo: "Meta batida!", detalhe: "Mandou bem nesta semana." };
  }
  if (provisorias >= meta) {
    return { titulo: "Meta no papo!", detalhe: "Só falta a galera conferir." };
  }
  if (!contaNestaSemana) {
    return {
      titulo: "Começa segunda",
      detalhe: "Seus treinos contam a partir da próxima semana.",
    };
  }

  const faltam = meta - provisorias;
  const treinos = plural(faltam, "treino", "treinos");
  const diasTexto = plural(dias, "dia", "dias");

  if (faltam > dias) {
    return {
      titulo: "Fica pra próxima",
      detalhe:
        "A meta desta semana não fecha mais. Treinar ainda conta pro grupo!",
    };
  }
  if (dias === 1) {
    return { titulo: "Último dia!", detalhe: "Falta 1 treino, e é hoje." };
  }
  if (faltam === dias) {
    return {
      titulo: "Sem folga",
      detalhe: `${treinos} em ${diasTexto}: um por dia.`,
    };
  }
  if (dias - faltam === 1) {
    return {
      titulo: "Aperta o passo",
      detalhe: `${treinos} e ${diasTexto} pela frente.`,
    };
  }
  if (provisorias === 0 && dias >= 6) {
    return {
      titulo: "Semana nova",
      detalhe: `Meta: ${plural(meta, "treino", "treinos")}. Bora começar!`,
    };
  }
  if (faltam === 1) {
    return { titulo: "Falta só 1!", detalhe: `Ainda tem ${diasTexto}.` };
  }
  return {
    titulo: `Faltam ${faltam}`,
    detalhe: `${diasTexto} pela frente. Ainda dá tempo!`,
  };
}
