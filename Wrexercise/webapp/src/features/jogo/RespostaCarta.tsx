import { useState } from "react";
import { ApiError, getErrorMessage } from "@/services/api";
import { IconeCarta } from "./CartaJogo";
import { objetivoCurto } from "./textosCartas";
import {
  type CodigoProvocacao,
  type CodigoResposta,
  type Jogada,
  type RespostaJogada,
  useLoja,
  useResponderJogada,
} from "./useCartas";
import {
  avisoFadigaAplicada,
  marcarFadigaVista,
  useEnergia,
} from "./useEnergia";

const avisoResposta: Record<CodigoResposta, string> = {
  DesafioDuplo: "Carta aceita e devolvida. Agora são dois desafios!",
  Aposta: "Aposta feita. Cumprindo, a recompensa dobra!",
  PrazoExtra: "Carta aceita com 24 horas a mais.",
  MudancaDePlanos: "Objetivo trocado. Bora treinar!",
};

/** O que aconteceu ao responder: para a encenação e para o aviso de quem respondeu. */
export type RespostaEnviada = {
  aceitou: boolean;
  /** O texto do aviso: o da Resposta usada, o do aceite ou o da FADIGA. */
  aviso: string;
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
    <div className="mt-3 border-t-2 border-border pt-3">
      <p id={`respostas-${jogada.id}`} className="eyebrow mb-2">
        Ou aceite com uma Resposta
      </p>
      <div
        role="radiogroup"
        aria-labelledby={`respostas-${jogada.id}`}
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
                setCartaId(marcada ? undefined : carta.id);
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
      {escolhida && (
        <button
          type="button"
          className="btn-primary mt-3 h-11 w-full"
          disabled={!pronta || pendente}
          onClick={() =>
            onResponder({
              cartaRespostaId: escolhida.id,
              objetivo: precisaObjetivo ? objetivo : undefined,
            })
          }
        >
          {pronta ? `Aceitar com ${escolhida.nome}` : "Escolha o objetivo"}
        </button>
      )}
    </div>
  );
}

/**
 * A resposta a uma Provocação recebida (decisão 0076): aceitar, recusar com um toque (o custo da FADIGA
 * fica no próprio botão) ou aceitar com uma Resposta da Mão. Usado na Chegada e na Mesa.
 */
export function PainelResposta({
  jogada,
  grupoId,
  onRespondida,
  onEncerrada,
}: {
  jogada: Jogada;
  grupoId: string;
  onRespondida: (resposta: RespostaEnviada) => void;
  /** A carta expirou ou já foi respondida noutro aparelho. */
  onEncerrada?: () => void;
}) {
  const responder = useResponderJogada(grupoId);
  const loja = useLoja();
  const energia = useEnergia();
  // FADIGA não acumula nem renova: com ela ativa, recusar não muda nada (decisão 0059).
  const comFadiga = (energia.data?.fadigaRestante ?? 0) > 0;
  const encerrada =
    responder.error instanceof ApiError &&
    responder.error.code === "JOGADA_ENCERRADA";

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
        onSuccess: () => {
          if (resposta === "Recusada" && !comFadiga) {
            // O aviso já aparece aqui: o da mudança da FADIGA não deve repetir.
            marcarFadigaVista(3);
          }
          onRespondida({
            aceitou: resposta === "Aceita",
            aviso: codigoResposta
              ? avisoResposta[codigoResposta]
              : resposta === "Aceita"
                ? "Carta aceita. Bora treinar!"
                : comFadiga
                  ? "Carta recusada. Você já está com FADIGA, então nada muda."
                  : avisoFadigaAplicada,
          });
        },
      },
    );
  };

  if (encerrada) {
    return (
      <div role="alert" className="mt-3">
        <p className="text-sm">Essa carta expirou ou já foi respondida.</p>
        {onEncerrada && (
          <button
            type="button"
            className="btn-primary mt-3 h-11 w-full"
            onClick={onEncerrada}
          >
            Entendi
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="mt-3">
      <p
        className={`rounded-md px-3 py-2 text-sm ${jogada.jaCumpre ? "bg-game-soft font-semibold text-game" : "bg-surface-muted text-muted"}`}
      >
        {jogada.jaCumpre
          ? "Seu treino já cumpre esta carta. Aceite e ganhe!"
          : "Vale o próximo treino depois do aceite. Já treinou depois que a carta chegou? Se cumprir, vale também."}
      </p>
      {responder.isError && (
        <p role="alert" className="form-error mt-3">
          {getErrorMessage(responder.error)}
        </p>
      )}
      <button
        type="button"
        className="btn-primary mt-3 h-12 w-full"
        disabled={responder.isPending}
        onClick={() => enviar("Aceita")}
      >
        Aceitar · +{jogada.recompensa} EN
      </button>
      <button
        type="button"
        className="btn-ghost mt-2 min-h-11 w-full border-2 border-border text-sm"
        disabled={responder.isPending}
        onClick={() => enviar("Recusada")}
      >
        {comFadiga
          ? "Recusar · você já está com FADIGA"
          : "Recusar · FADIGA: 3 treinos com −50% EN"}
      </button>
      <p className="mt-1 text-center text-xs text-muted">
        Recusando, quem enviou perde a carta.
      </p>
      <AceitarComResposta
        jogada={jogada}
        pendente={responder.isPending}
        onResponder={(extra) => enviar("Aceita", extra)}
      />
    </div>
  );
}
