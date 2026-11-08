import { useState } from "react";
import { Link } from "react-router";
import { Dialog } from "@/components/Dialog";
import { useVitrine } from "@/components/folhaAvatar";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { CartaJogo } from "./CartaJogo";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { primeiroNome } from "./textosCartas";
import { useJogarCarta, useLoja } from "./useCartas";
import type { MembroCena } from "./useCena";
import { useEscolherRival, useRivais } from "./useRivais";

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

/** Escolher o colega como Rival da semana. Só aparece para quem pode ser escolhido agora. */
function EscolherRival({
  grupoId,
  membro,
  onEscolhido,
}: {
  grupoId: string;
  membro: MembroCena;
  onEscolhido: (rivalId: string) => void;
}) {
  const rivais = useRivais(grupoId);
  const escolher = useEscolherRival(grupoId);
  const { showToast } = useToast();
  const nome = primeiroNome(membro.nomeExibicao);

  if (!rivais.data?.candidatos.includes(membro.usuarioId)) return null;

  return (
    <div>
      <p className="eyebrow mb-2">Rival da semana</p>
      <p className="text-sm text-muted">
        Vence quem cumprir a meta da semana primeiro, e quem vence ganha 15 EN.{" "}
        {nome} não perde nada. A escolha vale até domingo e não pode ser
        trocada.
      </p>
      {escolher.isError && (
        <p role="alert" className="form-error mt-3">
          {getErrorMessage(escolher.error)}
        </p>
      )}
      <button
        type="button"
        className="btn-secondary mt-3 w-full"
        disabled={escolher.isPending}
        onClick={() =>
          escolher.mutate(membro.usuarioId, {
            onSuccess: () => {
              showToast(`${nome} é o seu Rival da semana!`);
              onEscolhido(membro.usuarioId);
            },
          })
        }
      >
        {escolher.isPending ? "Escolhendo…" : `Escolher ${nome} como Rival`}
      </button>
    </div>
  );
}

/** A vitrine de Enfeites de quem foi tocado; sem Enfeites, não mostra nada. */
function Vitrine({ usuarioId }: { usuarioId: string }) {
  const vitrine = useVitrine(usuarioId);
  if (vitrine.length === 0) return null;
  return (
    <div>
      <p className="eyebrow mb-2">Vitrine</p>
      <ul className="flex gap-4">
        {vitrine.map((enfeite) => (
          <li key={enfeite.codigo} className="flex flex-col items-center gap-1">
            <EnfeiteIcone conquista={enfeite.conquista} tamanho={48} />
            <span className="text-2xs text-muted">{enfeite.nome}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Tocar num avatar: acesso à página do membro e, para um colega, jogar uma carta. */
export function MenuAvatar({
  grupoId,
  membro,
  usuarioId,
  onClose,
  onRival,
}: {
  grupoId: string;
  membro: MembroCena | null;
  usuarioId: string;
  onClose: () => void;
  /** Quem vê a cena acabou de escolher este colega como Rival. */
  onRival: (rivalId: string) => void;
}) {
  return (
    <Dialog
      open={membro !== null}
      title={membro ? primeiroNome(membro.nomeExibicao) : ""}
      onClose={onClose}
    >
      {membro && (
        <div className="space-y-5">
          <Vitrine usuarioId={membro.usuarioId} />
          {membro.usuarioId !== usuarioId && (
            <JogarCarta grupoId={grupoId} membro={membro} onJogada={onClose} />
          )}
          {membro.usuarioId !== usuarioId && (
            <EscolherRival
              grupoId={grupoId}
              membro={membro}
              onEscolhido={(rivalId) => {
                onClose();
                onRival(rivalId);
              }}
            />
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
