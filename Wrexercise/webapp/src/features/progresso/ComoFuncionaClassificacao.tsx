import { ChevronDown, CircleHelp } from "lucide-react";

const criterios = [
  "Mais semanas com a meta cumprida",
  "Mais dias verificados no desafio",
  "Maior sequência atual de semanas cumpridas 🔥",
  "Mais tempo de treino verificado",
  "Mais atividades verificadas",
];

const detalhes = [
  "Dias além da meta também contam no desempate.",
  "Só conta treino verificado. Um treino de domingo verificado na terça conta para a semana dele.",
  "A semana atual conta assim que você bate a meta.",
  "A sequência só perde a semana atual se ela fechar sem a meta.",
  "Quem entra no meio começa na primeira semana completa. As semanas antes contam como 0, e a linha mostra “X de Y semanas”.",
];

/**
 * As regras da classificação, por extenso (ADR 0090). Fechado por padrão: quem quer entender abre,
 * e a lista continua curta para quem só quer ver a posição.
 */
export function ComoFuncionaClassificacao() {
  return (
    <details className="group card mt-3">
      <summary className="flex min-h-11 cursor-pointer list-none items-center gap-2 px-4 py-3 text-sm font-semibold [&::-webkit-details-marker]:hidden">
        <CircleHelp aria-hidden="true" className="size-4 shrink-0 text-link" />
        <span className="flex-1">Como funciona a classificação</span>
        <ChevronDown
          aria-hidden="true"
          className="size-4 shrink-0 text-muted transition-transform group-open:rotate-180"
        />
      </summary>
      <div className="space-y-3 px-4 pb-4 text-sm">
        <div>
          <p>Fica na frente quem tem, nesta ordem:</p>
          <ol className="mt-1 list-decimal space-y-1 pl-5">
            {criterios.map((criterio) => (
              <li key={criterio}>{criterio}</li>
            ))}
          </ol>
          <p className="mt-2 text-muted">
            Cada regra só vale se a anterior empatar. Empatou em tudo? Os dois
            dividem a mesma posição.
          </p>
        </div>
        <p className="text-muted">
          Exemplo: Ana e Bia cumpriram 3 semanas. Ana tem 14 dias verificados e
          Bia, 12. Ana fica na frente.
        </p>
        <ul className="list-disc space-y-1 pl-5 text-muted">
          {detalhes.map((detalhe) => (
            <li key={detalhe}>{detalhe}</li>
          ))}
        </ul>
      </div>
    </details>
  );
}
