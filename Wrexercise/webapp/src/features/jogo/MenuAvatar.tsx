import { useState } from "react";
import { Link } from "react-router";
import { Dialog } from "@/components/Dialog";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { CartaJogo } from "./CartaJogo";
import { primeiroNome } from "./textosCartas";
import { useJogarCarta, useLoja } from "./useCartas";
import type { MembroCena } from "./useCena";

/** Escolher uma carta da Mão e jogar para o colega. */
function JogarCarta({
  grupoId,
  membro,
  onJogada,
}: {
  grupoId: string;
  membro: MembroCena;
  onJogada: () => void;
}) {
  const loja = useLoja();
  const jogar = useJogarCarta(grupoId);
  const { showToast } = useToast();
  const [cartaId, setCartaId] = useState<string | null>(null);
  const nome = primeiroNome(membro.nomeExibicao);

  if (!membro.recebeCartas) {
    return (
      <p className="text-sm text-muted">
        {nome} está no modo App e não recebe cartas.
      </p>
    );
  }

  if (!loja.data) return null;
  // Só Provocações são jogadas para um colega; as Respostas ficam para aceitar uma carta.
  const disponiveis = loja.data.mao.filter(
    (c) => c.tipo === "Provocacao" && !c.emJogo,
  );
  if (disponiveis.length === 0) {
    return (
      <p className="text-sm text-muted">
        Você não tem Provocações livres na Mão.{" "}
        <Link to="/jogo/loja" className="font-semibold text-link">
          Ir para a Loja
        </Link>
      </p>
    );
  }

  const escolhida = disponiveis.find((c) => c.id === cartaId);

  return (
    <div>
      <p className="eyebrow mb-2">Provocar {nome}</p>
      <div
        role="radiogroup"
        aria-label="Carta para jogar"
        className="-mx-5 flex gap-3 overflow-x-auto px-5 pt-1 pb-3"
      >
        {disponiveis.map((carta) => {
          const marcada = carta.id === cartaId;
          return (
            <button
              key={carta.id}
              type="button"
              role="radio"
              aria-checked={marcada}
              aria-label={`${carta.nome}, ${carta.preco} EN`}
              className={`shrink-0 rounded-lg p-1 tap ${marcada ? "ring-3 ring-game" : ""}`}
              onClick={() => setCartaId(carta.id)}
            >
              <CartaJogo carta={carta} />
            </button>
          );
        })}
      </div>
      <p className="min-h-10 text-sm text-muted">
        {escolhida
          ? escolhida.efeito
          : "Escolha uma carta. Se a carta for recusada ou expirar, ela volta para a sua Mão."}
      </p>
      {jogar.isError && (
        <p role="alert" className="form-error mt-3">
          {getErrorMessage(jogar.error)}
        </p>
      )}
      <button
        type="button"
        className="btn-primary mt-4 w-full"
        disabled={!escolhida || jogar.isPending}
        onClick={() =>
          escolhida &&
          jogar.mutate(
            { cartaId: escolhida.id, destinatarioId: membro.usuarioId },
            {
              onSuccess: () => {
                showToast(`Carta enviada para ${nome}!`);
                onJogada();
              },
            },
          )
        }
      >
        {escolhida ? `Jogar ${escolhida.nome}` : "Jogar carta"}
      </button>
    </div>
  );
}

/** Tocar num avatar: acesso à página do membro e, para um colega, jogar uma carta. */
export function MenuAvatar({
  grupoId,
  membro,
  usuarioId,
  onClose,
}: {
  grupoId: string;
  membro: MembroCena | null;
  usuarioId: string;
  onClose: () => void;
}) {
  return (
    <Dialog
      open={membro !== null}
      title={membro ? primeiroNome(membro.nomeExibicao) : ""}
      onClose={onClose}
    >
      {membro && (
        <div className="space-y-5">
          {membro.usuarioId !== usuarioId && (
            <JogarCarta grupoId={grupoId} membro={membro} onJogada={onClose} />
          )}
          <Link
            to={`/grupos/${grupoId}/membros/${membro.usuarioId}`}
            className="btn-secondary w-full"
          >
            {membro.usuarioId === usuarioId
              ? "Ver a minha página"
              : `Ver a página de ${primeiroNome(membro.nomeExibicao)}`}
          </Link>
        </div>
      )}
    </Dialog>
  );
}
