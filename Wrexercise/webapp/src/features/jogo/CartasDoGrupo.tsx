import { Store } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { SectionTitle } from "@/components/PageHeader";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { CartaJogo, IconeCarta } from "./CartaJogo";
import {
  formatarPrazo,
  getObjetivo,
  getSeloSituacao,
  objetivoCurto,
  primeiroNome,
} from "./textosCartas";
import {
  type CodigoProvocacao,
  type CodigoResposta,
  type Jogada,
  type RespostaJogada,
  useJogadas,
  useLoja,
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

const avisoResposta: Record<CodigoResposta, string> = {
  DesafioDuplo: "Carta aceita e devolvida. Agora são dois desafios!",
  Aposta: "Aposta feita. Cumprindo, a recompensa dobra!",
  PrazoExtra: "Carta aceita com 24 horas a mais.",
  MudancaDePlanos: "Objetivo trocado. Bora treinar!",
};

/** Aceitar usando uma Resposta da Mão: escolhe a carta e, na MUDANÇA DE PLANOS, o objetivo novo. */
function AceitarComResposta({
  jogada,
  pendente,
  onResponder,
}: {
  jogada: Jogada;
  pendente: boolean;
  onResponder: (dados: Omit<RespostaJogada, "id" | "resposta">) => void;
}) {
  const loja = useLoja();
  const [cartaId, setCartaId] = useState<string>();
  const [objetivo, setObjetivo] = useState<CodigoProvocacao>();

  const respostas = (loja.data?.mao ?? []).filter(
    (c) =>
      c.tipo === "Resposta" &&
      !(c.codigo === "DesafioDuplo" && jogada.desafioDuplo) &&
      !(
        c.codigo === "MudancaDePlanos" &&
        jogada.objetivosEquivalentes.length === 0
      ),
  );
  if (respostas.length === 0) return null;

  const escolhida = respostas.find((c) => c.id === cartaId);
  const precisaObjetivo = escolhida?.codigo === "MudancaDePlanos";
  const pronta = escolhida && (!precisaObjetivo || objetivo);

  return (
    <details className="mt-3 rounded-md border-2 border-border">
      <summary className="flex min-h-11 cursor-pointer items-center px-3 text-sm font-semibold">
        Aceitar com uma Resposta
      </summary>
      <div className="px-3 pb-3">
        <div
          role="radiogroup"
          aria-label="Resposta para usar"
          className="flex flex-wrap gap-2"
        >
          {respostas.map((carta) => {
            const marcada = carta.id === cartaId;
            return (
              <button
                key={carta.id}
                type="button"
                role="radio"
                aria-checked={marcada}
                className={`flex min-h-11 items-center gap-2 rounded-md border-2 px-2 text-left tap ${marcada ? "border-game bg-game-soft" : "border-border"}`}
                onClick={() => {
                  setCartaId(carta.id);
                  setObjetivo(undefined);
                }}
              >
                <IconeCarta codigo={carta.codigo} />
                <span className="font-display text-sm uppercase">
                  {carta.nome}
                </span>
              </button>
            );
          })}
        </div>
        {escolhida && (
          <p className="mt-2 text-sm text-muted">{escolhida.efeito}</p>
        )}
        {precisaObjetivo && (
          <fieldset className="mt-3">
            <legend className="eyebrow mb-2">Trocar por</legend>
            <div className="grid gap-2">
              {jogada.objetivosEquivalentes.map((codigo) => (
                <label
                  key={codigo}
                  className={`flex min-h-11 cursor-pointer items-center gap-3 rounded-md border-2 px-3 text-sm ${objetivo === codigo ? "border-game bg-game-soft" : "border-border"}`}
                >
                  <input
                    type="radio"
                    name={`objetivo-${jogada.id}`}
                    value={codigo}
                    checked={objetivo === codigo}
                    onChange={() => setObjetivo(codigo)}
                    className="accent-game"
                  />
                  {objetivoCurto[codigo]}
                </label>
              ))}
            </div>
          </fieldset>
        )}
        <button
          type="button"
          className="btn-primary mt-3 h-11 w-full"
          disabled={!pronta || pendente}
          onClick={() =>
            escolhida &&
            onResponder({
              cartaRespostaId: escolhida.id,
              objetivo: precisaObjetivo ? objetivo : undefined,
            })
          }
        >
          {escolhida ? `Aceitar com ${escolhida.nome}` : "Escolha uma Resposta"}
        </button>
      </div>
    </details>
  );
}

function JogadaItem({ jogada, grupoId }: { jogada: Jogada; grupoId: string }) {
  const responder = useResponderJogada(grupoId);
  const loja = useLoja();
  const { showToast } = useToast();
  const selo = getSeloSituacao(jogada.situacao, jogada.recebida);
  const pendente = jogada.recebida && jogada.situacao === "Enviada";
  const encerrada = ["Concluida", "Recusada", "Expirada"].includes(
    jogada.situacao,
  );

  const remetente = primeiroNome(jogada.remetenteNome);
  const titulo = jogada.recebida
    ? jogada.desafioDuplo
      ? `${remetente} devolveu o desafio`
      : `${remetente} te provocou`
    : `Para ${primeiroNome(jogada.destinatarioNome)}`;

  const enviar = (
    resposta: "Aceita" | "Recusada",
    extra: Omit<RespostaJogada, "id" | "resposta"> = {},
  ) => {
    const codigoResposta = loja.data?.mao.find(
      (c) => c.id === extra.cartaRespostaId,
    )?.codigo as CodigoResposta | undefined;
    responder.mutate(
      { id: jogada.id, resposta, ...extra },
      {
        onSuccess: () =>
          showToast(
            codigoResposta
              ? avisoResposta[codigoResposta]
              : resposta === "Aceita"
                ? "Carta aceita. Bora treinar!"
                : "Carta recusada, sem problema.",
          ),
      },
    );
  };

  const aposta = jogada.cartaResposta?.codigo === "Aposta";

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
            {jogada.cartaResposta && (
              <span className="chip chip-game uppercase">
                {jogada.cartaResposta.nome}
              </span>
            )}
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
          {jogada.recebida && aposta && !encerrada && (
            <p className="mt-1 text-xs font-semibold text-game">
              Apostado: cumprindo, você ganha {jogada.carta.preco * 2} EN.
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
          <AceitarComResposta
            jogada={jogada}
            pendente={responder.isPending}
            onResponder={(extra) => enviar("Aceita", extra)}
          />
        </>
      )}
    </li>
  );
}

/** As Jogadas de quem está vendo a cena neste grupo: recebidas primeiro. */
function MesaJogadas({ grupoId }: { grupoId: string }) {
  const jogadas = useJogadas(grupoId);
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
