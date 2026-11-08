import { Flag } from "lucide-react";
import { primeiroNome } from "./textosCartas";
import { type Rival, useRivais } from "./useRivais";

/** O selo diz o resultado por extenso: a cor só reforça (docs/gamificacao.md §1.3). */
function SeloRival({ rival }: { rival: Rival }) {
  if (rival.situacao === "Venceu" && rival.escolhido) {
    return <span className="badge badge-reward">Venceu · +15 EN</span>;
  }
  if (rival.situacao === "EmAndamento") {
    return <span className="badge badge-game">Rival</span>;
  }
  if (rival.situacao === "SemVencedor") {
    return <span className="badge">Sem vencedor</span>;
  }
  return <span className="badge">Encerrado</span>;
}

/** Uma frase do ponto de vista de quem consulta. Quem foi escolhido nunca "perde". */
function getTexto(rival: Rival): string {
  const outro = primeiroNome(
    rival.escolhido ? rival.rivalNome : rival.escolhedorNome,
  );
  if (rival.escolhido) {
    switch (rival.situacao) {
      case "EmAndamento":
        return `Você × ${outro}: quem cumprir a meta da semana primeiro vence.`;
      case "Venceu":
        return `Você cumpriu a meta antes de ${outro}.`;
      case "Perdeu":
        return `${outro} cumpriu a meta primeiro. Semana que vem tem mais.`;
      case "SemVencedor":
        return `Ninguém venceu o duelo com ${outro} nesta semana.`;
    }
  }
  switch (rival.situacao) {
    case "EmAndamento":
      return `${outro} escolheu você como Rival da semana.`;
    case "Venceu":
      return `${outro} cumpriu a meta antes de você no duelo da semana.`;
    case "Perdeu":
      return `Você cumpriu a meta antes de ${outro}, que te escolheu como Rival.`;
    case "SemVencedor":
      return `O duelo com ${outro} terminou sem vencedor.`;
  }
}

/** Rival da semana, embaixo da praça. Só quem está no par vê o par. */
export function RivalDaSemana({ grupoId }: { grupoId: string }) {
  const rivais = useRivais(grupoId);
  if (!rivais.data) return null;

  const { rivais: lista, podeEscolher, vitorias, semanaInicio } = rivais.data;
  if (lista.length === 0 && !podeEscolher) return null;

  // O par da semana primeiro; os resultados antigos ainda não vistos, depois.
  const ordenados = [...lista].sort(
    (a, b) =>
      Number(b.semanaInicio === semanaInicio) -
      Number(a.semanaInicio === semanaInicio),
  );

  return (
    <section aria-labelledby="rival-titulo" className="card mt-4 p-4">
      <div className="mb-2 flex items-center justify-between gap-3">
        <h2
          id="rival-titulo"
          className="flex items-center gap-2 text-sm font-semibold"
        >
          <Flag aria-hidden="true" className="size-4 text-game" />
          Rival da semana
        </h2>
        {vitorias > 0 && (
          <span className="text-xs text-muted">
            {vitorias} {vitorias === 1 ? "vitória" : "vitórias"}
          </span>
        )}
      </div>
      {ordenados.length > 0 && (
        <ul className="space-y-2">
          {ordenados.map((rival) => (
            <li key={rival.id} className="flex items-center gap-3 text-sm">
              <p className="min-w-0 flex-1">{getTexto(rival)}</p>
              <SeloRival rival={rival} />
            </li>
          ))}
        </ul>
      )}
      {podeEscolher && (
        <p className="text-sm text-muted">
          Toque num colega na praça para escolher o seu Rival. Vence quem
          cumprir a meta da semana primeiro, e quem vence ganha 15 EN.
        </p>
      )}
    </section>
  );
}
