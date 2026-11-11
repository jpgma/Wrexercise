import { Link } from "react-router";
import { useState } from "react";
import { getErrorMessage } from "@/services/api";
import { Bloqueado, maxVitrine } from "./EditorVitrine";
import { EnfeiteIcone } from "./EnfeiteIcone";
import {
  type EnfeiteBloqueado,
  type MeuAvatar,
  useMeuAvatar,
  useSalvarVitrine,
} from "./useAvatares";

/** Os que estão mais perto de liberar vêm primeiro. */
function porProximidade(a: EnfeiteBloqueado, b: EnfeiteBloqueado) {
  return b.atual / Math.max(1, b.alvo) - a.atual / Math.max(1, a.alvo);
}

function Editor({ avatar }: { avatar: MeuAvatar }) {
  const salvar = useSalvarVitrine();
  const [escolhidos, setEscolhidos] = useState<string[]>(
    avatar.vitrine.map((e) => e.codigo),
  );
  const [salvo, setSalvo] = useState(false);
  const porCodigo = new Map(avatar.liberados.map((e) => [e.codigo, e]));
  const bloqueados = [...avatar.bloqueados].sort(porProximidade);
  const cheia = escolhidos.length >= maxVitrine;

  // Cada toque salva. Se o envio falhar, a vitrine volta ao que era antes do toque.
  const alterar = (proximos: string[]) => {
    const anteriores = escolhidos;
    setEscolhidos(proximos);
    setSalvo(false);
    salvar.mutate(proximos, {
      onSuccess: () => setSalvo(true),
      onError: () => setEscolhidos(anteriores),
    });
  };
  const tirar = (codigo: string) =>
    alterar(escolhidos.filter((c) => c !== codigo));
  const alternar = (codigo: string) => {
    if (escolhidos.includes(codigo)) tirar(codigo);
    else if (!cheia) alterar([...escolhidos, codigo]);
  };

  const status = salvar.isPending ? "Salvando…" : salvo ? "Salva" : "";

  return (
    <>
      <div className="flex min-h-11 items-center gap-3">
        <h2
          id="vitrine-foco-titulo"
          className="min-w-0 flex-1 text-sm font-semibold"
        >
          Sua Vitrine{" "}
          <span className="font-score text-base font-normal text-muted">
            {escolhidos.length}/{maxVitrine}
          </span>
        </h2>
        <p aria-live="polite" className="text-xs text-muted">
          {status}
        </p>
        <Link
          to="/perfil/avatar"
          className="inline-flex min-h-11 shrink-0 items-center text-xs font-semibold text-link"
        >
          Trocar avatar
        </Link>
      </div>

      {/* As vagas na ordem da praça: a primeira fica ao lado do avatar. */}
      <ol aria-label="Ordem da vitrine" className="mt-1 grid grid-cols-3 gap-2">
        {Array.from({ length: maxVitrine }, (_, i) => {
          const codigo = escolhidos[i];
          const enfeite = codigo ? porCodigo.get(codigo) : undefined;
          return (
            <li key={i}>
              {enfeite ? (
                <button
                  type="button"
                  aria-label={`${i + 1}º: ${enfeite.nome}. Tirar da vitrine`}
                  onClick={() => tirar(enfeite.codigo)}
                  className="relative grid h-16 w-full place-items-center rounded-lg border-2 border-game tap"
                >
                  <span className="absolute top-1 left-1.5 font-score text-xs text-muted">
                    {i + 1}
                  </span>
                  <EnfeiteIcone conquista={enfeite.conquista} tamanho={48} />
                </button>
              ) : (
                <div
                  aria-label={`${i + 1}º: vazio`}
                  role="img"
                  className="relative grid h-16 w-full place-items-center rounded-lg border-2 border-dashed border-border-strong"
                >
                  <span className="font-score text-sm text-muted">{i + 1}</span>
                </div>
              )}
            </li>
          );
        })}
      </ol>

      {avatar.liberados.length === 0 ? (
        <p className="mt-3 text-sm text-muted">
          Você ainda não liberou nenhum Enfeite. Eles vêm das conquistas do seu
          Perfil.
        </p>
      ) : (
        <>
          <p className="mt-3 text-xs text-muted">
            {cheia
              ? "Vitrine cheia: toque num escolhido para tirar."
              : "Toque para pôr na próxima vaga. A 1ª fica ao lado do seu avatar."}
          </p>
          <ul className="mt-2 grid grid-cols-4 gap-2">
            {avatar.liberados.map((enfeite) => {
              const posicao = escolhidos.indexOf(enfeite.codigo);
              const marcado = posicao >= 0;
              return (
                <li key={enfeite.codigo}>
                  <button
                    type="button"
                    aria-pressed={marcado}
                    disabled={!marcado && cheia}
                    onClick={() => alternar(enfeite.codigo)}
                    className={`relative flex min-h-11 w-full flex-col items-center gap-1 rounded-lg p-1.5 tap disabled:opacity-50 ${marcado ? "ring-3 ring-game" : ""}`}
                  >
                    {marcado && (
                      <span
                        aria-hidden="true"
                        className="absolute top-0.5 right-1 font-score text-xs text-game"
                      >
                        {posicao + 1}
                      </span>
                    )}
                    <EnfeiteIcone conquista={enfeite.conquista} tamanho={48} />
                    <span className="text-2xs leading-tight">
                      {enfeite.nome}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        </>
      )}

      {salvar.isError && (
        <p role="alert" className="form-error mt-2">
          {getErrorMessage(salvar.error)}
        </p>
      )}

      {bloqueados.length > 0 && (
        <div className="mt-4">
          <h3 className="text-sm font-semibold">Ainda bloqueados</h3>
          <ul className="mt-2 grid grid-cols-3 gap-2">
            {bloqueados.map((enfeite) => (
              <Bloqueado key={enfeite.codigo} enfeite={enfeite} />
            ))}
          </ul>
        </div>
      )}
    </>
  );
}

/**
 * No próprio Foco, a Vitrine toma o lugar da Mão (docs/gamificacao.md §3.5): as vagas em ordem,
 * os Enfeites liberados e os que faltam, dos mais perto de liberar aos mais longe. Cada toque
 * salva, e o primeiro Enfeite troca na praça logo em seguida.
 */
export function VitrineFoco() {
  const avatar = useMeuAvatar();
  return (
    <section
      aria-labelledby="vitrine-foco-titulo"
      className="card mt-2 animate-slide-up p-3"
    >
      {avatar.data ? (
        <Editor avatar={avatar.data} />
      ) : (
        <h2 id="vitrine-foco-titulo" className="min-h-11 text-sm font-semibold">
          Sua Vitrine
        </h2>
      )}
    </section>
  );
}
