import { formatarDataVersao, type Versao } from "./versoes";

/** As novidades de uma ou mais versões, cada uma com o número, a data e os tópicos. */
export function ListaVersoes({ versoes }: { versoes: readonly Versao[] }) {
  return (
    <div className="space-y-5">
      {versoes.map(({ versao, data, novidades }) => (
        <section key={versao} aria-label={`Versão ${versao}`}>
          <p className="text-xs font-semibold text-muted">
            Versão {versao} · {formatarDataVersao(data)}
          </p>
          <ul className="mt-2 list-disc space-y-1.5 pl-5 text-sm">
            {novidades.map((novidade) => (
              <li key={novidade}>{novidade}</li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
