import { Zap } from "lucide-react";
import { SectionTitle } from "@/components/PageHeader";
import { getErrorMessage } from "@/services/api";
import { type DetalhamentoEnergia, useEnergia } from "./useEnergia";

const formatador = new Intl.NumberFormat("pt-BR");

/** Selo com o saldo, como placar. Some enquanto carrega ou se a consulta falhar. */
export function SaldoEnergia() {
  const energia = useEnergia();
  if (!energia.data) return null;

  return <SeloEnergia saldo={energia.data.saldo} />;
}

function SeloEnergia({ saldo }: { saldo: number }) {
  const valor = formatador.format(saldo);
  return (
    <p className="badge badge-game text-xs" title="Energia">
      <span className="sr-only">Energia: {valor}</span>
      <Zap aria-hidden="true" className="size-3.5" />
      <span aria-hidden="true" className="font-score text-sm font-normal">
        {valor}
      </span>
      <span aria-hidden="true">EN</span>
    </p>
  );
}

const origens: { chave: keyof DetalhamentoEnergia; rotulo: string }[] = [
  { chave: "atividades", rotulo: "Atividades verificadas" },
  { chave: "bonus", rotulo: "Bônus de treino" },
  { chave: "metas", rotulo: "Semanas com a meta cumprida" },
  { chave: "sequencias", rotulo: "Sequências de semanas" },
  { chave: "desafios", rotulo: "Desafios concluídos" },
  { chave: "jogadas", rotulo: "Cartas cumpridas" },
];

/** Seção do Perfil: saldo e de onde veio a Energia ganha. */
export function EnergiaPerfil() {
  const energia = useEnergia();

  return (
    <>
      <SectionTitle>Energia</SectionTitle>
      {energia.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(energia.error)}
        </p>
      )}
      {energia.data && (
        <div className="card p-4">
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-muted">
              Use a Energia para comprar cartas no modo Jogo.
            </p>
            <SeloEnergia saldo={energia.data.saldo} />
          </div>
          <dl className="mt-3 divide-y divide-border text-sm">
            {origens.map(({ chave, rotulo }) => (
              <div
                key={chave}
                className="flex items-baseline justify-between gap-3 py-2"
              >
                <dt className="text-muted">{rotulo}</dt>
                <dd className="font-semibold tabular-nums">
                  {formatador.format(energia.data.detalhamento[chave])} EN
                </dd>
              </div>
            ))}
          </dl>
        </div>
      )}
    </>
  );
}
