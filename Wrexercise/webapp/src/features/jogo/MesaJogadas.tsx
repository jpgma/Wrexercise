import { useState } from "react";
import { Dialog } from "@/components/Dialog";
import { useToast } from "@/components/Toast";
import { IconeCarta } from "./CartaJogo";
import { PainelResposta } from "./RespostaCarta";
import {
  formatarDiaDoPrazo,
  formatarPrazo,
  formatarPrazoCurto,
  getCodigos,
  getNomeJogada,
  getObjetivo,
  getObjetivoCurto,
  getSeloSituacao,
  getVez,
  primeiroNome,
} from "./textosCartas";
import { type Jogada, useJogadas } from "./useCartas";

const encerradas: readonly string[] = [
  "Concluida",
  "Recusada",
  "Expirada",
  "Falhou",
];

export function getTitulo(jogada: Jogada): string {
  const remetente = primeiroNome(jogada.remetenteNome);
  if (!jogada.recebida) return `Para ${primeiroNome(jogada.destinatarioNome)}`;
  return jogada.desafioDuplo
    ? `${remetente} devolveu o desafio`
    : `${remetente} te provocou`;
}

/** A carta, o objetivo, o prazo e a situação de uma Jogada: na Mesa e na Chegada. */
export function CabecalhoJogada({ jogada }: { jogada: Jogada }) {
  const selo = getSeloSituacao(jogada.situacao, jogada.recebida);
  const pendente = jogada.recebida && jogada.situacao === "Enviada";
  const encerrada = encerradas.includes(jogada.situacao);
  const aposta = jogada.cartaResposta?.codigo === "Aposta";

  return (
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
          <span className={`chip uppercase ${selo.classe}`}>{selo.texto}</span>
          {jogada.cartaResposta && (
            <span className="chip chip-game uppercase">
              {jogada.cartaResposta.nome}
            </span>
          )}
          {!jogada.recebida && jogada.devolucao > 0 && (
            <span className="chip chip-success">
              +{jogada.devolucao} EN de volta
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
        {jogada.recebida && jogada.situacao === "Aceita" && (
          <p className="mt-1 text-xs text-muted">
            Vale {getVez(getCodigos(jogada))}. Os treinos fora disso não contam
            nem fazem falhar. Um treino feito depois que a carta chegou e antes
            do aceite também vale, se cumprir.
          </p>
        )}
        {jogada.recebida && jogada.situacao === "Falhou" && (
          <p className="mt-1 text-xs text-muted">
            O próximo treino não cumpriu a carta.
          </p>
        )}
        {!jogada.recebida && jogada.devolucao > 0 && (
          <p className="mt-1 text-xs text-muted">
            {jogada.situacao === "Recusada" &&
            jogada.devolucao >= jogada.carta.preco
              ? `A carta não volta para a Mão, mas o preço inteiro voltou pra você: +${jogada.devolucao} EN.`
              : `A carta não volta para a Mão, mas metade do preço voltou pra você: +${jogada.devolucao} EN.`}
          </p>
        )}
        {jogada.recebida && aposta && !encerrada && (
          <p className="mt-1 text-xs font-semibold text-game">
            Apostado: cumprindo, você ganha {jogada.recompensa} EN.
          </p>
        )}
      </div>
    </div>
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
  const { showToast } = useToast();
  const pendente = jogada.recebida && jogada.situacao === "Enviada";

  return (
    <div>
      <CabecalhoJogada jogada={jogada} />
      {pendente && (
        <PainelResposta
          jogada={jogada}
          grupoId={grupoId}
          onRespondida={({ aviso }) => showToast(aviso)}
        />
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
