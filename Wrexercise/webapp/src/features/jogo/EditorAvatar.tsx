import { Avatar } from "@/components/Avatar";
import { folhaPadrao } from "@/components/folhaAvatar";
import { useSessao } from "@/features/autenticacao";
import { getErrorMessage } from "@/services/api";
import { direcoes } from "./animacoes";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMovimentoReduzido } from "./useMovimentoReduzido";
import { useRestaurarFolha } from "./useAvatares";

/**
 * A importação está pausada enquanto o avatar em 8 direções é testado (ADR 0039, Andamento): todos
 * usam a folha padrão. A folha importada antes continua guardada, e pode ser apagada aqui.
 */
export function EditorAvatar() {
  const { data: usuario } = useSessao();
  const restaurar = useRestaurarFolha();
  const reduzido = useMovimentoReduzido();

  if (!usuario) return null;

  return (
    <div className="space-y-4">
      <div className="card flex items-center gap-4 p-5">
        <Avatar nome={usuario.nomeExibicao} id={usuario.id} size={72} />
        <div className="min-w-0">
          <p className="font-display text-xl">{usuario.nomeExibicao}</p>
          <p className="text-sm text-muted">
            Seu avatar aparece para quem divide um grupo com você.
          </p>
        </div>
      </div>

      <div className="card space-y-3 p-5">
        <p className="text-sm font-semibold">Importar sua folha de sprites</p>
        <p role="status" className="text-sm text-muted">
          A importação está pausada enquanto testamos o avatar novo, em 8
          direções. Por enquanto, todos usam o mesmo avatar.
        </p>
        <ul
          className="flex flex-wrap justify-center"
          aria-label="Avatar padrão"
        >
          {direcoes.map((direcao) => (
            <li key={direcao}>
              <SpriteAvatar
                folha={folhaPadrao()}
                animacao="parado"
                direcao={direcao}
                escala={1}
                estatico={reduzido}
              />
            </li>
          ))}
        </ul>
        {restaurar.isError && (
          <p role="alert" className="form-error">
            {getErrorMessage(restaurar.error)}
          </p>
        )}
      </div>

      <button
        type="button"
        className="btn-secondary w-full justify-center"
        disabled={restaurar.isPending}
        onClick={() => restaurar.mutate()}
      >
        {restaurar.isPending ? "Apagando…" : "Apagar a folha importada"}
      </button>
    </div>
  );
}
