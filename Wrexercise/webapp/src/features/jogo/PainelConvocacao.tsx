import { Check } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import { TipoAtividadeIcone, tipoAtividadeLabels } from "@/features/atividades";
import { getErrorMessage } from "@/services/api";
import { primeiroNome } from "./textosCartas";
import {
  type Convocacao,
  useConvocacoes,
  useResponderConvocacao,
} from "./useConvocacoes";

/** "5 dias restantes", "3 h restantes" ou "menos de 1 h". */
export function getTempoRestante(expiraEm: string, agora = Date.now()): string {
  const ms = new Date(expiraEm).getTime() - agora;
  const horas = Math.floor(ms / 3_600_000);
  if (horas >= 24) {
    // Arredondado: logo depois de convocar, são "7 dias", não "6".
    const dias = Math.round(horas / 24);
    return dias === 1 ? "1 dia restante" : `${dias} dias restantes`;
  }
  return horas >= 1 ? `${horas} h restantes` : "menos de 1 h";
}

/** O texto da situação da Convocação, sem expor quem recusou. */
export function getResumoConvocacao(c: Convocacao): string {
  if (c.situacao === "Ativa") return getTempoRestante(c.expiraEm);
  if (c.situacao === "AguardandoVerificacao") return "Esperando verificações";
  return "Encerrada";
}

/** A frase da carta: "Essa semana todo mundo vai de Ciclismo!". */
export const fraseConvocacao = (c: Convocacao) =>
  `Essa semana todo mundo vai de ${tipoAtividadeLabels[c.tipoAtividade]}!`;

/** Os rostos da galera, com um ✓ em quem concluiu. */
export function RostosConvocacao({ convocacao }: { convocacao: Convocacao }) {
  return (
    <ul className="flex flex-wrap gap-1.5">
      {convocacao.participantes.map((p) => (
        <li key={p.usuarioId} className="relative flex">
          <Avatar nome={p.nomeExibicao} id={p.usuarioId} size={32} />
          <span className="sr-only">
            {p.nomeExibicao}
            {p.situacao === "Concluida" ? ": concluiu" : ""}
          </span>
          {p.situacao === "Concluida" && (
            <span
              aria-hidden="true"
              className="absolute -right-1 -bottom-1 grid size-4 place-items-center rounded-full bg-success text-surface"
            >
              <Check className="size-3" strokeWidth={3} />
            </span>
          )}
        </li>
      ))}
    </ul>
  );
}

/**
 * O painel do TODO MUNDO na praça, acima da Mão (decisão 0091): a Convocação ativa, ou o resultado da última,
 * com o convite que não trava o app ("Tô dentro!" / "Dessa vez não").
 */
export function PainelConvocacao({ grupoId }: { grupoId: string }) {
  const convocacoes = useConvocacoes(grupoId);
  const destaque = convocacoes.data?.destaque;
  if (!destaque) return null;
  return <Painel grupoId={grupoId} convocacao={destaque} />;
}

function Painel({
  grupoId,
  convocacao: c,
}: {
  grupoId: string;
  convocacao: Convocacao;
}) {
  return (
    <section
      aria-labelledby="convocacao-titulo"
      className="card mt-2 border-2 border-outline p-3"
    >
      <div className="flex items-start gap-3">
        <span className="grid size-11 shrink-0 place-items-center rounded-lg border-2 border-outline bg-reward text-on-reward">
          <TipoAtividadeIcone tipo={c.tipoAtividade} className="size-6" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-2">
            <span className="chip bg-reward px-1 text-2xs text-on-reward uppercase">
              Global
            </span>
            <span className="text-xs text-muted">{getResumoConvocacao(c)}</span>
          </p>
          <h2
            id="convocacao-titulo"
            className="mt-1 text-lg leading-tight uppercase"
          >
            {c.nome}
          </h2>
          <p className="text-sm">{fraseConvocacao(c)}</p>
          <p className="text-xs text-muted">
            Lançado por {primeiroNome(c.anfitriaoNome)} · {c.concluidas} de{" "}
            {c.participantes.length} concluíram
          </p>
        </div>
      </div>
      <div className="mt-3 flex items-center justify-between gap-3">
        <RostosConvocacao convocacao={c} />
        <Link
          to={`/grupos/${grupoId}/convocacoes/${c.id}`}
          className="shrink-0 text-xs font-semibold text-link tap"
        >
          Detalhes
        </Link>
      </div>
      <MinhaParte grupoId={grupoId} convocacao={c} />
    </section>
  );
}

/** O que cabe a quem vê: responder, sair, ou o resultado. */
export function MinhaParte({
  grupoId,
  convocacao: c,
}: {
  grupoId: string;
  convocacao: Convocacao;
}) {
  const responder = useResponderConvocacao(grupoId, c.id);
  const [confirmarSaida, setConfirmarSaida] = useState(false);
  const anfitriao = c.devolucao !== null;
  const tipo = tipoAtividadeLabels[c.tipoAtividade];

  let conteudo: ReactNode = null;
  switch (c.minhaSituacao) {
    case "Pendente":
      conteudo = (
        <>
          <p className="text-xs text-muted">
            Um treino verificado de {tipo} até o prazo rende +30 EN. Sem
            resposta até lá, FADIGA.
          </p>
          <div className="mt-2 flex gap-2">
            <button
              type="button"
              className="btn-primary h-11 flex-1"
              disabled={responder.isPending}
              onClick={() => responder.mutate("Aceita")}
            >
              Tô dentro!
            </button>
            <button
              type="button"
              className="btn-ghost h-11 flex-1 text-xs"
              disabled={responder.isPending}
              onClick={() => responder.mutate("Recusada")}
            >
              Dessa vez não · FADIGA
            </button>
          </div>
        </>
      );
      break;
    case "Participando":
      conteudo = confirmarSaida ? (
        <>
          <p className="text-xs">Sair conta como não cumprida: dá FADIGA.</p>
          <div className="mt-2 flex gap-2">
            <button
              type="button"
              className="btn-danger h-11 flex-1"
              disabled={responder.isPending}
              onClick={() => responder.mutate("Desistiu")}
            >
              Sair · FADIGA
            </button>
            <button
              type="button"
              className="btn-ghost h-11 flex-1"
              onClick={() => setConfirmarSaida(false)}
            >
              Ficar
            </button>
          </div>
        </>
      ) : (
        <div className="flex items-center justify-between gap-2">
          <p className="text-xs">
            {anfitriao ? "Você convocou: vale pra você também. " : ""}Faça um
            treino de {tipo}. Recompensa: <strong>+30 EN</strong>
          </p>
          {c.podeSair && (
            <button
              type="button"
              className="btn-ghost h-11 shrink-0 px-3 text-xs"
              onClick={() => setConfirmarSaida(true)}
            >
              Sair
            </button>
          )}
        </div>
      );
      break;
    case "AguardandoVerificacao":
      conteudo = (
        <p className="text-xs">Treino registrado! Aguardando verificação.</p>
      );
      break;
    case "Concluida":
      conteudo = (
        <p className="font-semibold text-success">
          Missão cumprida! +{c.minhaRecompensa} EN
        </p>
      );
      break;
    case "Recusada":
    case "Desistiu":
    case "Falhou":
    case "Expirada":
      conteudo = <p className="text-xs text-muted">Dessa vez não rolou.</p>;
      break;
  }

  return (
    <div className="mt-3 border-t border-border pt-3" aria-live="polite">
      {conteudo}
      {anfitriao && (
        <p className="mt-1 text-xs text-muted">
          {c.devolucao
            ? `Já voltaram +${c.devolucao} EN da carta (+${c.devolucaoPorPessoa} EN por quem cumpre ou recusa, metade por quem não cumpre).`
            : `Cada pessoa da galera devolve +${c.devolucaoPorPessoa} EN da carta se cumprir ou recusar, e +${Math.floor((c.devolucaoPorPessoa ?? 0) / 2)} EN se não cumprir.`}
        </p>
      )}
      {responder.isError && (
        <p role="alert" className="form-error mt-2">
          {getErrorMessage(responder.error)}
        </p>
      )}
    </div>
  );
}
