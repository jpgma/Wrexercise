import { Zap } from "lucide-react";
import { SectionTitle } from "@/components/PageHeader";
import { getErrorMessage } from "@/services/api";
import { type DetalhamentoEnergia, useEnergia } from "./useEnergia";

const formatador = new Intl.NumberFormat("pt-BR");

/** Selo com o saldo, como placar, e a FADIGA, se houver. Some enquanto carrega ou se a consulta falhar. */
export function SaldoEnergia() {
  const energia = useEnergia();
  if (!energia.data) return null;

  return (
    <span className="flex items-center gap-1.5">
      <SeloFadiga restante={energia.data.fadigaRestante} />
      <SeloEnergia saldo={energia.data.saldo} />
    </span>
  );
}

/**
 * A FADIGA ativa: bateria fraca e quantos treinos ainda rendem metade (decisão 0059). Sem FADIGA, não aparece.
 * O aviso de mudança vem do toast; o selo não é uma região viva.
 */
export function SeloFadiga({ restante }: { restante: number }) {
  if (restante <= 0) return null;
  const treinos = restante === 1 ? "1 treino" : `${restante} treinos`;
  return (
    <span
      className="chip chip-danger gap-1 text-xs uppercase"
      title={`FADIGA: seus próximos ${treinos} rendem 50% menos EN.`}
    >
      <img
        src="/fadiga.png"
        alt=""
        width={16}
        height={16}
        className="shrink-0 [image-rendering:pixelated]"
      />
      <span className="sr-only">
        FADIGA: seus próximos {treinos} rendem 50% menos EN.
      </span>
      <span aria-hidden="true">
        Fadiga <span className="font-score font-normal">{restante}</span>
      </span>
    </span>
  );
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
  { chave: "rivais", rotulo: "Rivais vencidos" },
  { chave: "fadiga", rotulo: "FADIGA (treinos com −50%)" },
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
          {energia.data.fadigaRestante > 0 && (
            <p className="mt-3 flex items-center gap-2 text-sm">
              <SeloFadiga restante={energia.data.fadigaRestante} />
              <span aria-hidden="true" className="text-muted">
                Seus próximos{" "}
                {energia.data.fadigaRestante === 1
                  ? "treino rende"
                  : `${energia.data.fadigaRestante} treinos rendem`}{" "}
                50% menos EN.
              </span>
            </p>
          )}
          <dl className="mt-3 divide-y divide-border text-sm">
            {origens
              .filter(
                ({ chave }) =>
                  chave !== "fadiga" || energia.data.detalhamento.fadiga !== 0,
              )
              .map(({ chave, rotulo }) => (
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
