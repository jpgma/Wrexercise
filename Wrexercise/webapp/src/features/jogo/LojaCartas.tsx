import { Zap } from "lucide-react";
import { SectionTitle } from "@/components/PageHeader";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { CartaJogo } from "./CartaJogo";
import { type Carta, useComprarCarta, useLoja } from "./useCartas";

function ItemLoja({
  carta,
  saldo,
  maoCheia,
}: {
  carta: Carta;
  saldo: number;
  maoCheia: boolean;
}) {
  const comprar = useComprarCarta();
  const { showToast } = useToast();
  const faltam = carta.preco - saldo;

  return (
    <li className="card flex gap-4 p-4">
      <CartaJogo carta={carta} />
      <div className="flex min-w-0 flex-1 flex-col">
        <span className="badge self-start bg-surface-muted text-foreground shadow-none">
          Provocação
        </span>
        <p className="mt-2 text-sm">{carta.efeito}</p>
        <div className="mt-auto pt-3">
          {comprar.isError && (
            <p role="alert" className="form-error mb-2">
              {getErrorMessage(comprar.error)}
            </p>
          )}
          <button
            type="button"
            className="btn-secondary h-11 w-full px-3"
            disabled={faltam > 0 || maoCheia || comprar.isPending}
            onClick={() =>
              comprar.mutate(carta.codigo, {
                onSuccess: () => showToast(`${carta.nome} foi para a sua Mão.`),
              })
            }
          >
            <Zap aria-hidden="true" className="size-4" />
            Comprar por {carta.preco} EN
          </button>
          {faltam > 0 && (
            <p className="mt-1 text-xs text-muted">Faltam {faltam} EN.</p>
          )}
        </div>
      </div>
    </li>
  );
}

/** A Loja: as Provocações à venda e a Mão de quem compra. */
export function LojaCartas() {
  const loja = useLoja();

  if (loja.isPending) {
    return (
      <p role="status" className="py-8 text-center text-sm text-muted">
        Carregando…
      </p>
    );
  }

  if (loja.isError) {
    return (
      <p role="alert" className="form-error">
        {getErrorMessage(loja.error)}
      </p>
    );
  }

  const { saldo, limiteMao, catalogo, mao } = loja.data;
  const maoCheia = mao.length >= limiteMao;

  return (
    <>
      <div className="card flex items-center justify-between gap-3 p-4">
        <div>
          <p className="eyebrow">Seu saldo</p>
          <p className="font-score text-3xl text-game">
            {saldo} <span className="text-base">EN</span>
          </p>
        </div>
        <div className="text-right">
          <p className="eyebrow">Sua Mão</p>
          <p className="font-score text-3xl">
            {mao.length}
            <span className="text-base text-muted">/{limiteMao}</span>
          </p>
        </div>
      </div>
      <p className="mt-3 text-sm text-muted">
        {maoCheia
          ? "A sua Mão está cheia. Jogue uma carta para abrir espaço."
          : "Compre uma Provocação e jogue para um colega na praça. Se ele cumprir, os dois ganham Energia."}
      </p>

      <SectionTitle>Provocações</SectionTitle>
      <ul className="space-y-3">
        {catalogo.map((carta) => (
          <ItemLoja
            key={carta.codigo}
            carta={carta}
            saldo={saldo}
            maoCheia={maoCheia}
          />
        ))}
      </ul>
    </>
  );
}
