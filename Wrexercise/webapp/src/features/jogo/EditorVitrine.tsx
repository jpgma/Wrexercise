import { useState } from "react";
import { getErrorMessage } from "@/services/api";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { useMeuAvatar, useSalvarVitrine, type MeuAvatar } from "./useAvatares";

const maxVitrine = 3;

function Editor({ avatar }: { avatar: MeuAvatar }) {
  const salvar = useSalvarVitrine();
  const [escolhidos, setEscolhidos] = useState<string[]>(
    avatar.vitrine.map((e) => e.codigo),
  );
  const [salvo, setSalvo] = useState(false);

  const porCodigo = new Map(avatar.liberados.map((e) => [e.codigo, e]));
  const alterar = (proximos: string[]) => {
    setSalvo(false);
    setEscolhidos(proximos);
  };
  const alternar = (codigo: string) =>
    alterar(
      escolhidos.includes(codigo)
        ? escolhidos.filter((c) => c !== codigo)
        : escolhidos.length < maxVitrine
          ? [...escolhidos, codigo]
          : escolhidos,
    );
  const mover = (indice: number, passo: -1 | 1) => {
    const destino = indice + passo;
    if (destino < 0 || destino >= escolhidos.length) return;
    const proximos = [...escolhidos];
    [proximos[indice], proximos[destino]] = [
      proximos[destino]!,
      proximos[indice]!,
    ];
    alterar(proximos);
  };

  return (
    <div className="card space-y-4 p-5">
      <div>
        <h2 className="text-sm font-semibold">Vitrine de Enfeites</h2>
        <p className="text-xs text-muted">
          Até {maxVitrine} Enfeites aparecem para quem divide um grupo com você
          no modo Jogo. O primeiro fica ao lado do seu avatar na praça. Deixe
          vazia para não mostrar nada.
        </p>
      </div>

      {avatar.liberados.length === 0 ? (
        <p className="text-sm text-muted">
          Você ainda não liberou nenhum Enfeite. Eles vêm das conquistas do seu
          Perfil.
        </p>
      ) : (
        <>
          <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
            {avatar.liberados.map((enfeite) => {
              const marcado = escolhidos.includes(enfeite.codigo);
              return (
                <li key={enfeite.codigo}>
                  <button
                    type="button"
                    aria-pressed={marcado}
                    disabled={!marcado && escolhidos.length >= maxVitrine}
                    onClick={() => alternar(enfeite.codigo)}
                    className={`flex w-full flex-col items-center gap-1 rounded-lg p-2 tap ${marcado ? "ring-3 ring-game" : "opacity-80"}`}
                  >
                    <EnfeiteIcone conquista={enfeite.conquista} tamanho={64} />
                    <span className="text-2xs">{enfeite.nome}</span>
                  </button>
                </li>
              );
            })}
          </ul>

          {escolhidos.length > 1 && (
            <ol className="space-y-1 text-sm">
              {escolhidos.map((codigo, indice) => (
                <li key={codigo} className="flex items-center gap-2">
                  <span className="w-5 text-muted">{indice + 1}.</span>
                  <span className="flex-1">{porCodigo.get(codigo)?.nome}</span>
                  <button
                    type="button"
                    className="btn-ghost"
                    disabled={indice === 0}
                    aria-label={`Subir ${porCodigo.get(codigo)?.nome}`}
                    onClick={() => mover(indice, -1)}
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="btn-ghost"
                    disabled={indice === escolhidos.length - 1}
                    aria-label={`Descer ${porCodigo.get(codigo)?.nome}`}
                    onClick={() => mover(indice, 1)}
                  >
                    ↓
                  </button>
                </li>
              ))}
            </ol>
          )}
        </>
      )}

      {salvar.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(salvar.error)}
        </p>
      )}
      {salvo && (
        <p role="status" className="text-sm font-semibold text-progress">
          Vitrine salva.
        </p>
      )}
      <button
        type="button"
        className="btn-primary w-full"
        disabled={salvar.isPending}
        onClick={() =>
          salvar.mutate(escolhidos, { onSuccess: () => setSalvo(true) })
        }
      >
        {salvar.isPending ? "Salvando…" : "Salvar vitrine"}
      </button>
    </div>
  );
}

export function EditorVitrine() {
  const avatar = useMeuAvatar();
  if (!avatar.data) return null;
  // `key` recomeça o editor se a vitrine mudar no servidor.
  return (
    <Editor
      key={avatar.data.vitrine.map((e) => e.codigo).join(",")}
      avatar={avatar.data}
    />
  );
}
