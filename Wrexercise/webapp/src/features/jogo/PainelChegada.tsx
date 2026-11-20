import { CabecalhoJogada, getTitulo } from "./MesaJogadas";
import { PainelResposta, type RespostaEnviada } from "./RespostaCarta";
import { primeiroNome } from "./textosCartas";
import type { Jogada } from "./useCartas";

/** Para onde a pessoa vai depois da Chegada: registrar o treino ou seguir para onde estava. */
export type DestinoChegada = "treino" | "depois";

/**
 * O painel da Chegada, embaixo da praça, no lugar da Mão (decisão 0076). Enquanto a carta voa, fica
 * vazio; depois, a carta cresce até virar a resposta, sem botão de fechar. Respondida, diz o que
 * aconteceu e oferece registrar o treino.
 */
export function PainelChegada({
  jogada,
  carregando,
  grupoId,
  chegando,
  reduzido,
  respondida,
  onRespondida,
  onConcluir,
}: {
  jogada: Jogada | undefined;
  /** As Jogadas do grupo ainda não chegaram. */
  carregando: boolean;
  grupoId: string;
  /** A carta ainda está voando: a resposta espera o impacto. */
  chegando: boolean;
  reduzido: boolean;
  respondida: RespostaEnviada | null;
  onRespondida: (resposta: RespostaEnviada) => void;
  onConcluir: (destino: DestinoChegada) => void;
}) {
  if (chegando) {
    return (
      <p className="mt-3 text-center text-sm text-muted" aria-hidden="true">
        Toque na praça para pular.
      </p>
    );
  }

  if (respondida) {
    const cumprida =
      jogada?.situacao === "Concluida" ||
      jogada?.situacao === "AguardandoVerificacao";
    const nome = jogada ? primeiroNome(jogada.remetenteNome) : "";
    return (
      <section
        aria-live="polite"
        className="card mt-3 p-4 animate-fade-in"
        aria-label="Carta respondida"
      >
        <p className="font-display text-xl leading-tight uppercase">
          {!respondida.aceitou
            ? "Carta recusada"
            : cumprida
              ? "Carta cumprida!"
              : "Carta aceita"}
        </p>
        <p className="mt-1 text-sm text-muted">
          {respondida.aceitou && cumprida
            ? jogada?.situacao === "Concluida"
              ? `Seu treino já cumpriu a carta de ${nome}!`
              : `Seu treino já cumpre a carta de ${nome}. Falta só a verificação.`
            : respondida.aviso}
        </p>
        {respondida.aceitou && !cumprida ? (
          <div className="mt-4 grid gap-2">
            <button
              type="button"
              className="btn-primary h-12 w-full"
              onClick={() => onConcluir("treino")}
            >
              Registrar treino
            </button>
            <button
              type="button"
              className="btn-ghost min-h-11 w-full"
              onClick={() => onConcluir("depois")}
            >
              Depois
            </button>
          </div>
        ) : (
          <button
            type="button"
            className="btn-primary mt-4 h-12 w-full"
            onClick={() => onConcluir("depois")}
          >
            {respondida.aceitou ? "Continuar" : "Entendi"}
          </button>
        )}
      </section>
    );
  }

  if (carregando) return null;

  if (!jogada || !(jogada.recebida && jogada.situacao === "Enviada")) {
    return (
      <section role="alert" className="card mt-3 p-4">
        <p className="text-sm">Essa carta expirou ou já foi respondida.</p>
        <button
          type="button"
          className="btn-primary mt-3 h-12 w-full"
          onClick={() => onConcluir("depois")}
        >
          Entendi
        </button>
      </section>
    );
  }

  return (
    <section
      aria-labelledby="chegada-titulo"
      className={`card-sticker mt-3 p-4 ${reduzido ? "" : "carta-cresce"}`}
    >
      <h2
        id="chegada-titulo"
        className="mb-3 font-display text-xl leading-tight uppercase"
      >
        {getTitulo(jogada)}!
      </h2>
      <CabecalhoJogada jogada={jogada} />
      <PainelResposta
        jogada={jogada}
        grupoId={grupoId}
        onRespondida={onRespondida}
        onEncerrada={() => onConcluir("depois")}
      />
    </section>
  );
}
