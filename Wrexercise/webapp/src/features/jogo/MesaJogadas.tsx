import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { Dialog } from "@/components/Dialog";
import { useToast } from "@/components/Toast";
import { getErrorMessage } from "@/services/api";
import { IconeCarta } from "./CartaJogo";
import {
  formatarDiaDoPrazo,
  formatarPrazo,
  formatarPrazoCurto,
  getNomeJogada,
  getObjetivo,
  getObjetivoCurto,
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

const encerradas: readonly string[] = [
  "Concluida",
  "Recusada",
  "Expirada",
  "Falhou",
];

function getTitulo(jogada: Jogada): string {
  const remetente = primeiroNome(jogada.remetenteNome);
  if (!jogada.recebida) return `Para ${primeiroNome(jogada.destinatarioNome)}`;
  return jogada.desafioDuplo
    ? `${remetente} devolveu o desafio`
    : `${remetente} te provocou`;
}

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

/** Os detalhes de uma carta da Mesa, dentro da janela: objetivo, prazo e, se pendente, a resposta. */
function DetalheJogada({
  jogada,
  grupoId,
}: {
  jogada: Jogada;
  grupoId: string;
}) {
  const responder = useResponderJogada(grupoId);
  const loja = useLoja();
  const energia = useEnergia();
  const { showToast } = useToast();
  const [confirmandoRecusa, setConfirmandoRecusa] = useState(false);
  // FADIGA não acumula nem renova: com ela ativa, recusar não muda nada (decisão 0059).
  const comFadiga = (energia.data?.fadigaRestante ?? 0) > 0;
  const selo = getSeloSituacao(jogada.situacao, jogada.recebida);
  const pendente = jogada.recebida && jogada.situacao === "Enviada";
  const encerrada = encerradas.includes(jogada.situacao);

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
          setConfirmandoRecusa(false);
          if (resposta === "Recusada" && !comFadiga) {
            // O aviso já aparece aqui: o da mudança da FADIGA não deve repetir.
            marcarFadigaVista(3);
          }
          showToast(
            codigoResposta
              ? avisoResposta[codigoResposta]
              : resposta === "Aceita"
                ? "Carta aceita. Bora treinar!"
                : comFadiga
                  ? "Carta recusada. Você já está com FADIGA, então nada muda."
                  : avisoFadigaAplicada,
          );
        },
      },
    );
  };

  const aposta = jogada.cartaResposta?.codigo === "Aposta";

  return (
    <div>
      <div className="flex gap-3">
        <span className="flex shrink-0 gap-1 self-start">
          <span className="grid size-11 place-items-center rounded-md bg-carta-giz">
            <IconeCarta codigo={jogada.carta.codigo} />
          </span>
          {jogada.cartaFusao && (
            <span className="grid size-11 place-items-center rounded-md bg-carta-giz">
              <IconeCarta codigo={jogada.cartaFusao.codigo} />
            </span>
          )}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <p className="font-display text-base leading-tight uppercase">
              {getNomeJogada(jogada)}
            </p>
            {jogada.cartaFusao && (
              <span className="chip chip-game uppercase">Fusão</span>
            )}
            <span className={`chip uppercase ${selo.classe}`}>
              {selo.texto}
            </span>
            {jogada.cartaResposta && (
              <span className="chip chip-game uppercase">
                {jogada.cartaResposta.nome}
              </span>
            )}
          </div>
          <p className="mt-0.5 text-sm text-muted">{getObjetivo(jogada)}</p>
          {jogada.cartaFusao && !encerrada && (
            <p className="mt-1 text-xs text-muted">
              Tudo ou nada: os dois objetivos valem no mesmo treino.
            </p>
          )}
          {!encerrada && (
            <p className="mt-1 text-xs text-muted">
              {pendente ? "Responda até" : "Vale até"}{" "}
              {formatarPrazo(jogada.expiraEm)}
            </p>
          )}
          {jogada.recebida && (pendente || jogada.situacao === "Aceita") && (
            <p className="mt-1 text-xs text-muted">
              Só vale o próximo treino depois de aceitar.
            </p>
          )}
          {jogada.recebida && jogada.situacao === "Falhou" && (
            <p className="mt-1 text-xs text-muted">
              O próximo treino não cumpriu a carta.
            </p>
          )}
          {!jogada.recebida &&
            !jogada.desafioDuplo &&
            encerrada &&
            jogada.situacao !== "Concluida" && (
              <p className="mt-1 text-xs text-muted">
                Carta perdida: ela não volta para a Mão.
              </p>
            )}
          {jogada.recebida && aposta && !encerrada && (
            <p className="mt-1 text-xs font-semibold text-game">
              Apostado: cumprindo, você ganha {jogada.recompensa} EN.
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
              onClick={() => setConfirmandoRecusa(true)}
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
          <ul className="mt-2 space-y-0.5 text-xs text-muted">
            <li>
              Cumprir:{" "}
              <span className="font-semibold text-game">
                +{jogada.recompensa} EN
              </span>
            </li>
            <li>
              Recusar ou falhar:{" "}
              <span className="font-semibold text-foreground">
                FADIGA — 3 treinos com −50% EN
              </span>
            </li>
          </ul>
          <ConfirmDialog
            open={confirmandoRecusa}
            title="Recusar a carta?"
            message={
              comFadiga
                ? "Você já está com FADIGA. Ela não acumula nem renova: recusar não muda nada para você. Quem enviou perde a carta."
                : "Você fica com FADIGA: seus próximos 3 treinos verificados rendem metade da EN. A Energia que você já tem não muda, e o treino conta normalmente no desafio. Quem enviou perde a carta."
            }
            confirmLabel="Recusar"
            pending={responder.isPending}
            error={
              responder.isError ? getErrorMessage(responder.error) : undefined
            }
            onConfirm={() => enviar("Recusada")}
            onCancel={() => {
              responder.reset();
              setConfirmandoRecusa(false);
            }}
          />
          <AceitarComResposta
            jogada={jogada}
            pendente={responder.isPending}
            onResponder={(extra) => enviar("Aceita", extra)}
          />
        </>
      )}
    </div>
  );
}

/** Quanto do prazo ainda resta, de 0 a 1. */
function getRestante(jogada: Jogada, agora: number): number {
  const inicio = Date.parse(jogada.enviadaEm);
  const fim = Date.parse(jogada.expiraEm);
  if (!(fim > inicio)) return 0;
  return Math.min(1, Math.max(0, (fim - agora) / (fim - inicio)));
}

/** O ícone da carta com um anel em volta: a parte cheia é o prazo que ainda resta. */
function PrazoCarta({
  jogada,
  restante,
}: {
  jogada: Jogada;
  restante: number | null;
}) {
  const lado = 36;
  const raio = 16;
  const volta = 2 * Math.PI * raio;
  return (
    <span className="relative grid size-9 shrink-0 place-items-center">
      {restante !== null && (
        <svg
          aria-hidden="true"
          width={lado}
          height={lado}
          className="absolute inset-0 -rotate-90"
        >
          <circle
            cx={lado / 2}
            cy={lado / 2}
            r={raio}
            fill="none"
            stroke="var(--color-border)"
            strokeWidth={3}
          />
          <circle
            cx={lado / 2}
            cy={lado / 2}
            r={raio}
            fill="none"
            stroke="var(--color-game)"
            strokeWidth={3}
            strokeDasharray={volta}
            strokeDashoffset={volta * (1 - restante)}
          />
        </svg>
      )}
      {jogada.cartaFusao ? (
        // Fusão: os dois ícones, um sobre o outro em diagonal.
        <span className="relative size-6 [&_img]:size-4">
          <span className="absolute top-0 left-0 grid size-4 place-items-center overflow-hidden rounded-sm bg-carta-giz">
            <IconeCarta codigo={jogada.carta.codigo} />
          </span>
          <span className="absolute right-0 bottom-0 grid size-4 place-items-center overflow-hidden rounded-sm bg-carta-giz ring-1 ring-carta-tinta">
            <IconeCarta codigo={jogada.cartaFusao.codigo} />
          </span>
        </span>
      ) : (
        <span className="grid size-6 place-items-center overflow-hidden rounded-sm bg-carta-giz [&>img]:size-6">
          <IconeCarta codigo={jogada.carta.codigo} />
        </span>
      )}
    </span>
  );
}

/** Pede resposta primeiro, depois o que ainda vale e, por fim, o que já acabou. */
function ordem(jogada: Jogada): number {
  if (jogada.recebida && jogada.situacao === "Enviada") return 0;
  if (jogada.recebida && jogada.situacao === "Aceita") return 1;
  if (!encerradas.includes(jogada.situacao)) return 2;
  return 3;
}

/**
 * A Mesa, embaixo da Mão: as Jogadas de quem vê, numa fileira de mini-cartas. No Foco, só as
 * Jogadas entre quem vê e o alvo. Tocar numa abre os detalhes e, se for o caso, a resposta.
 */
export function MesaJogadas({
  grupoId,
  filtro,
}: {
  grupoId: string;
  /** O alvo do Foco: a Mesa mostra só as Jogadas com ele. */
  filtro?: string;
}) {
  const jogadas = useJogadas(grupoId);
  const [abertaId, setAbertaId] = useState<string | null>(null);
  // O prazo é lido uma vez por montagem: a Mesa remonta a cada Foco.
  const [agora] = useState(() => Date.now());
  const lista = (jogadas.data ?? [])
    .filter(
      (j) => !filtro || j.remetenteId === filtro || j.destinatarioId === filtro,
    )
    .sort((a, b) => ordem(a) - ordem(b));
  const aberta = jogadas.data?.find((j) => j.id === abertaId);

  // No Foco, sem Jogadas, a Mesa some para não gastar altura; fora dele, diz para que serve.
  if (lista.length === 0)
    return filtro || !jogadas.data ? null : (
      <p className="mt-1 border-t-2 border-border pt-2 text-2xs text-muted">
        Nada na mesa ainda. As cartas que você jogar ou receber aparecem aqui.
      </p>
    );

  return (
    <section
      aria-labelledby="mesa-titulo"
      className="mt-1 flex items-center gap-2 border-t-2 border-border pt-1"
    >
      {/* O rótulo fica ao lado da fileira, e não acima: a Mão inteira cabe com a praça. */}
      <h3 id="mesa-titulo" className="eyebrow w-12 shrink-0 leading-tight">
        {filtro ? "Entre vocês" : "Na mesa"}
      </h3>
      {/* O respiro vertical deixa o pulso da carta pendente aparecer: a rolagem corta o que passa. */}
      <ul className="-mr-3 flex min-w-0 flex-1 snap-x gap-2 overflow-x-auto py-1 pr-3 [scrollbar-width:none]">
        {lista.map((jogada) => {
          const selo = getSeloSituacao(jogada.situacao, jogada.recebida);
          const pendente = jogada.recebida && jogada.situacao === "Enviada";
          const encerrada = encerradas.includes(jogada.situacao);
          // O que fazer agora, sem abrir o detalhe: responder, ou o que falta e até quando.
          const resumo = pendente
            ? `Responda até ${formatarPrazoCurto(jogada.expiraEm)}`
            : jogada.situacao === "Aceita"
              ? `Falta: ${getObjetivoCurto(jogada)} · até ${formatarDiaDoPrazo(jogada.expiraEm)}`
              : selo.texto;
          const outro = primeiroNome(
            jogada.recebida ? jogada.remetenteNome : jogada.destinatarioNome,
          );
          return (
            <li key={jogada.id} className="shrink-0 snap-start">
              <button
                type="button"
                aria-haspopup="dialog"
                aria-label={`${getTitulo(jogada)}: ${jogada.cartaFusao ? "fusão " : ""}${getNomeJogada(jogada)}, ${jogada.situacao === "Aceita" ? `falta ${getObjetivoCurto(jogada)}` : selo.texto}${encerrada ? "" : `, até ${formatarPrazo(jogada.expiraEm)}`}`}
                onClick={() => setAbertaId(jogada.id)}
                className={`flex min-h-11 items-center gap-2 rounded-lg border-2 bg-surface py-0.5 pr-3 pl-0.5 text-left tap ${pendente ? "mesa-pendente border-game" : "border-border"} ${encerrada ? "opacity-60" : ""}`}
              >
                <PrazoCarta
                  jogada={jogada}
                  restante={encerrada ? null : getRestante(jogada, agora)}
                />
                <span className="leading-tight">
                  <span className="block text-xs font-semibold whitespace-nowrap">
                    {jogada.recebida ? `De ${outro}` : `Para ${outro}`}
                  </span>
                  <span
                    className={`block max-w-56 truncate text-2xs ${pendente ? "font-semibold text-game" : "text-muted"}`}
                  >
                    {resumo}
                  </span>
                </span>
              </button>
            </li>
          );
        })}
      </ul>
      <Dialog
        open={aberta !== undefined}
        title={aberta ? getTitulo(aberta) : ""}
        onClose={() => setAbertaId(null)}
      >
        {aberta && <DetalheJogada jogada={aberta} grupoId={grupoId} />}
      </Dialog>
    </section>
  );
}
