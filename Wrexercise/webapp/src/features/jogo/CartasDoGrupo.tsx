import { Store } from "lucide-react";
import { useEffect } from "react";
import { Link } from "react-router";
import { SectionTitle } from "@/components/PageHeader";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { CartaJogo, IconeCarta } from "./CartaJogo";
import {
  formatarPrazo,
  getObjetivo,
  getSeloSituacao,
  primeiroNome,
} from "./textosCartas";
import {
  type Jogada,
  useJogadas,
  useLoja,
  useMarcarJogadasVistas,
  useResponderJogada,
} from "./useCartas";

/** A Mão de quem está vendo a cena, com acesso à Loja. */
function MaoCartas() {
  const loja = useLoja();
  if (!loja.data) return null;
  const { mao, limiteMao } = loja.data;

  return (
    <section aria-labelledby="mao-titulo">
      <SectionTitle
        action={
          <Link to="/jogo/loja" className="btn-ghost h-11 px-3">
            <Store aria-hidden="true" className="size-4" />
            Loja
          </Link>
        }
      >
        <span id="mao-titulo">
          Sua Mão{" "}
          <span className="font-score text-base text-muted">
            {mao.length}/{limiteMao}
          </span>
        </span>
      </SectionTitle>
      {mao.length === 0 ? (
        <p className="card p-4 text-sm text-muted">
          Nenhuma carta por aqui. Use a sua Energia na{" "}
          <Link to="/jogo/loja" className="font-semibold text-link">
            Loja
          </Link>{" "}
          e provoque um colega tocando no avatar dele.
        </p>
      ) : (
        <>
          <ul className="-mx-4 flex gap-3 overflow-x-auto px-4 pt-1 pb-3">
            {mao.map((carta) => (
              <li key={carta.id} className="flex flex-col items-center gap-2">
                <CartaJogo carta={carta} apagada={carta.emJogo} />
                {carta.emJogo && <span className="chip">Em jogo</span>}
              </li>
            ))}
          </ul>
          <p className="text-xs text-muted">
            Toque no avatar de um colega para jogar uma carta.
          </p>
        </>
      )}
    </section>
  );
}

function JogadaItem({ jogada, grupoId }: { jogada: Jogada; grupoId: string }) {
  const responder = useResponderJogada(grupoId);
  const { showToast } = useToast();
  const selo = getSeloSituacao(jogada.situacao, jogada.recebida);
  const pendente = jogada.recebida && jogada.situacao === "Enviada";
  const encerrada = ["Concluida", "Recusada", "Expirada"].includes(
    jogada.situacao,
  );

  const titulo = jogada.recebida
    ? `${primeiroNome(jogada.remetenteNome)} te provocou`
    : `Para ${primeiroNome(jogada.destinatarioNome)}`;

  const enviar = (resposta: "Aceita" | "Recusada") =>
    responder.mutate(
      { id: jogada.id, resposta },
      {
        onSuccess: () =>
          showToast(
            resposta === "Aceita"
              ? "Carta aceita. Bora treinar!"
              : "Carta recusada, sem problema.",
          ),
      },
    );

  return (
    <li className="card p-3">
      <div className="flex gap-3">
        <span className="grid size-11 shrink-0 place-items-center self-start rounded-md bg-carta-giz">
          <IconeCarta codigo={jogada.carta.codigo} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <p className="text-sm font-semibold">{titulo}</p>
            <span className={`chip uppercase ${selo.classe}`}>
              {selo.texto}
            </span>
          </div>
          <p className="mt-0.5 font-display text-base leading-tight uppercase">
            {jogada.carta.nome}
          </p>
          <p className="text-sm text-muted">{getObjetivo(jogada)}</p>
          {!encerrada && (
            <p className="mt-1 text-xs text-muted">
              {pendente ? "Responda até" : "Vale até"}{" "}
              {formatarPrazo(jogada.expiraEm)}
            </p>
          )}
        </div>
      </div>
      {pendente && (
        <>
          {responder.isError && (
            <p role="alert" className="form-error mt-3">
              {getErrorMessage(responder.error)}
            </p>
          )}
          <div className="mt-3 grid grid-cols-2 gap-2">
            <button
              type="button"
              className="btn-ghost border-2 border-border"
              disabled={responder.isPending}
              onClick={() => enviar("Recusada")}
            >
              Recusar
            </button>
            <button
              type="button"
              className="btn-primary h-11"
              disabled={responder.isPending}
              onClick={() => enviar("Aceita")}
            >
              Aceitar
            </button>
          </div>
          <p className="mt-2 text-xs text-muted">
            Aceitar e recusar são de graça. Cumprindo, você ganha{" "}
            {jogada.carta.preco} EN.
          </p>
        </>
      )}
    </li>
  );
}

/** As Jogadas de quem está vendo a cena neste grupo: recebidas primeiro. */
function MesaJogadas({ grupoId }: { grupoId: string }) {
  const jogadas = useJogadas(grupoId);
  const marcar = useMarcarJogadasVistas(grupoId);
  const { mutate: marcarVistas, isPending: marcando } = marcar;

  const novas = (jogadas.data ?? []).filter((j) => j.nova).map((j) => j.id);
  const chaveNovas = novas.join(",");

  // Ver a lista já conta como ter visto a chegada da carta ou a resposta.
  useEffect(() => {
    if (chaveNovas && !marcando) marcarVistas(chaveNovas.split(","));
  }, [chaveNovas, marcando, marcarVistas]);

  const lista = [...(jogadas.data ?? [])].sort((a, b) => ordem(a) - ordem(b));
  if (lista.length === 0) return null;

  return (
    <section aria-labelledby="mesa-titulo">
      <SectionTitle>
        <span id="mesa-titulo">Cartas na mesa</span>
      </SectionTitle>
      <ul className="space-y-2">
        {lista.map((jogada) => (
          <JogadaItem key={jogada.id} jogada={jogada} grupoId={grupoId} />
        ))}
      </ul>
    </section>
  );
}

/** Pede resposta primeiro, depois o que ainda vale e, por fim, o que já acabou. */
function ordem(jogada: Jogada): number {
  if (jogada.recebida && jogada.situacao === "Enviada") return 0;
  if (jogada.recebida && jogada.situacao === "Aceita") return 1;
  if (["Enviada", "Aceita", "AguardandoVerificacao"].includes(jogada.situacao))
    return 2;
  return 3;
}

export function CartasDoGrupo({ grupoId }: { grupoId: string }) {
  return (
    <div className="space-y-2">
      <MesaJogadas grupoId={grupoId} />
      <MaoCartas />
    </div>
  );
}
