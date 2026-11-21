import { Check, Link2, X } from "lucide-react";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import { BrandMark, Logo } from "@/components/BrandMark";
import { montarRetorno, useAutorizarApp } from "@/features/integracoes";
import { getErrorMessage } from "@/services/api";

const podera = [
  "Preparar um rascunho de cada treino, com o resumo em imagem",
  "Ver o nome dos seus grupos, para você escolher onde treinar ao vivo",
  "Começar o treino ao vivo no grupo escolhido e cancelar se você desistir",
  "Mandar o resumo e o tempo do treino, até depois de você terminar aqui",
];

const naoPodera = [
  "Terminar ou postar por você: quem envia é sempre você, aqui",
  "Ver as atividades, as torcidas ou os comentários da galera",
];

/**
 * A pessoa permite que o Movr prepare rascunhos (ADR 0065) e comece o treino ao vivo (ADR 0081). O app abre
 * esta tela com o desafio do PKCE,
 * um `state` e o endereço de retorno; o código volta para o app por esse endereço.
 */
export function IntegracaoAutorizarPage() {
  const [busca] = useSearchParams();
  const codeChallenge = busca.get("code_challenge") ?? "";
  const state = busca.get("state") ?? "";
  const redirectUri = busca.get("redirect_uri") ?? "";
  const autorizar = useAutorizarApp();
  const [retorno, setRetorno] = useState<string | null>(null);

  const permitir = () =>
    autorizar.mutate(
      { app: "Movr", codeChallenge, redirectUri },
      {
        onSuccess: ({ codigo, redirectUri: confirmado }) => {
          const destino = montarRetorno(confirmado, codigo, state);
          setRetorno(destino);
          // Ainda dentro do toque em "Permitir": o navegador deixa abrir o app.
          window.location.href = destino;
        },
      },
    );

  if (!codeChallenge || !redirectUri || !state) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center px-6">
        <Logo />
        <h1 className="mt-8 text-3xl uppercase">Link incompleto</h1>
        <p className="mt-2 text-muted">
          Abra a conexão de novo pelo app Movr, em Conta → Wrexercise.
        </p>
        <Link to="/" className="btn-secondary mt-6">
          Ir para o início
        </Link>
      </main>
    );
  }

  if (retorno) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md animate-fade-in flex-col items-center justify-center px-6 text-center">
        <div className="grid size-20 animate-pop place-items-center rounded-xl border-2 border-outline bg-progress text-on-progress shadow-sticker">
          <Check aria-hidden="true" className="size-10" />
        </div>
        <h1 className="mt-6 text-3xl uppercase">Movr conectado</h1>
        <p className="mt-2 text-muted">
          Volte ao Movr para terminar. Se ele não abriu sozinho, toque abaixo.
        </p>
        <a href={retorno} className="btn-primary mt-6 w-full">
          Voltar ao Movr
        </a>
      </main>
    );
  }

  return (
    <main className="mx-auto flex min-h-dvh max-w-md animate-slide-up flex-col justify-center px-6 py-10">
      <div className="flex items-center justify-center gap-3">
        <BrandMark />
        <Link2 aria-hidden="true" className="size-6 text-muted" />
        <span className="grid size-16 place-items-center rounded-xl border-2 border-outline bg-surface font-display text-2xl uppercase shadow-sticker-sm">
          M
        </span>
      </div>
      <h1 className="mt-8 text-center text-3xl uppercase">Conectar o Movr</h1>
      <p className="mt-2 text-center text-muted">
        Ao começar um treino no Movr, a galera já te vê treinando ao vivo aqui.
      </p>

      <section className="card mt-6 p-4">
        <h2 className="text-sm font-semibold">O Movr poderá</h2>
        <ul className="mt-2 space-y-2 text-sm">
          {podera.map((item) => (
            <li key={item} className="flex gap-2">
              <Check
                aria-hidden="true"
                className="mt-0.5 size-4 shrink-0 text-on-success-soft"
              />
              {item}
            </li>
          ))}
        </ul>
        <h2 className="mt-4 text-sm font-semibold">O Movr não poderá</h2>
        <ul className="mt-2 space-y-2 text-sm">
          {naoPodera.map((item) => (
            <li key={item} className="flex gap-2">
              <X
                aria-hidden="true"
                className="mt-0.5 size-4 shrink-0 text-danger"
              />
              {item}
            </li>
          ))}
        </ul>
      </section>

      <p className="mt-4 text-center text-xs text-muted">
        Para desconectar, use o Movr ou Configurações → Apps conectados.
      </p>

      {autorizar.isError && (
        <p role="alert" className="form-error mt-4">
          {getErrorMessage(autorizar.error)}
        </p>
      )}

      <button
        type="button"
        className="btn-primary mt-6 w-full"
        disabled={autorizar.isPending}
        onClick={permitir}
      >
        {autorizar.isPending ? "Conectando…" : "Permitir"}
      </button>
      <Link to="/" className="btn-ghost mt-2 w-full">
        Agora não
      </Link>
    </main>
  );
}
