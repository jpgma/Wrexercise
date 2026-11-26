import { Check } from "lucide-react";
import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import { SectionTitle } from "@/components/PageHeader";
import { TipoAtividadeIcone, tipoAtividadeLabels } from "@/features/atividades";
import { getErrorMessage } from "@/services/api";
import {
  MinhaParte,
  fraseConvocacao,
  getResumoConvocacao,
} from "./PainelConvocacao";
import { primeiroNome } from "./textosCartas";
import {
  type Convocacao,
  type SituacaoParticipante,
  useConvocacao,
  useConvocacoes,
} from "./useConvocacoes";

/**
 * O resultado de cada pessoa, em tom positivo: só quem concluiu ou ainda está no jogo ganha destaque.
 * Quem recusou, saiu ou não cumpriu aparece sem destaque, sem dizer o motivo.
 */
const textoParticipante: Record<SituacaoParticipante, string> = {
  Pendente: "Ainda não respondeu",
  Participando: "Tá dentro",
  AguardandoVerificacao: "Treino em verificação",
  Concluida: "Concluiu",
  Recusada: "Dessa vez não",
  Desistiu: "Dessa vez não",
  Falhou: "Dessa vez não",
  Expirada: "Dessa vez não",
  ForaDoGrupo: "Saiu do grupo",
};

/** A Convocação inteira (decisão 0091): a galera, o resultado e as Convocações anteriores do grupo. */
export function DetalheConvocacao({
  grupoId,
  convocacaoId,
}: {
  grupoId: string;
  convocacaoId: string;
}) {
  const convocacao = useConvocacao(grupoId, convocacaoId);
  const lista = useConvocacoes(grupoId);

  if (convocacao.isPending)
    return (
      <p role="status" className="py-8 text-center text-sm text-muted">
        Carregando…
      </p>
    );
  if (convocacao.isError)
    return (
      <p role="alert" className="form-error">
        {getErrorMessage(convocacao.error)}
      </p>
    );

  const c = convocacao.data;
  const anteriores = (lista.data?.recentes ?? []).filter((r) => r.id !== c.id);

  return (
    <>
      <section className="card p-4">
        <div className="flex items-start gap-3">
          <span className="grid size-12 shrink-0 place-items-center rounded-lg border-2 border-outline bg-reward text-on-reward">
            <TipoAtividadeIcone tipo={c.tipoAtividade} className="size-7" />
          </span>
          <div className="min-w-0">
            <p className="eyebrow">{getResumoConvocacao(c)}</p>
            <p className="text-lg font-semibold">{fraseConvocacao(c)}</p>
            <p className="text-sm text-muted">
              Lançado por {primeiroNome(c.anfitriaoNome)} em{" "}
              {new Date(c.criadaEm).toLocaleDateString("pt-BR")}. Prazo:{" "}
              {new Date(c.expiraEm).toLocaleString("pt-BR", {
                dateStyle: "short",
                timeStyle: "short",
              })}
              .
            </p>
          </div>
        </div>
        <p className="mt-3 font-score text-3xl">
          {c.concluidas}
          <span className="text-base text-muted">
            {" "}
            de {c.participantes.length} concluíram
          </span>
        </p>
        <MinhaParte grupoId={grupoId} convocacao={c} />
      </section>

      <section className="mt-6">
        <SectionTitle>A galera</SectionTitle>
        <ul className="card divide-y divide-border">
          {c.participantes.map((p) => (
            <li key={p.usuarioId} className="flex items-center gap-3 p-3">
              <Avatar nome={p.nomeExibicao} id={p.usuarioId} size={36} />
              <span className="min-w-0 flex-1 truncate">
                {p.nomeExibicao}
                {p.anfitriao && (
                  <span className="ml-1 text-xs text-muted">(convocou)</span>
                )}
              </span>
              <span
                className={`flex items-center gap-1 text-xs ${p.situacao === "Concluida" ? "font-semibold text-success" : "text-muted"}`}
              >
                {p.situacao === "Concluida" && (
                  <Check aria-hidden="true" className="size-4" />
                )}
                {textoParticipante[p.situacao]}
              </span>
            </li>
          ))}
        </ul>
      </section>

      {anteriores.length > 0 && (
        <section className="mt-6">
          <SectionTitle>Convocações anteriores</SectionTitle>
          <ul className="space-y-2">
            {anteriores.map((a) => (
              <ItemAnterior key={a.id} grupoId={grupoId} convocacao={a} />
            ))}
          </ul>
        </section>
      )}
    </>
  );
}

function ItemAnterior({
  grupoId,
  convocacao: c,
}: {
  grupoId: string;
  convocacao: Convocacao;
}) {
  return (
    <li>
      <Link
        to={`/grupos/${grupoId}/convocacoes/${c.id}`}
        className="card flex items-center gap-3 p-3 tap"
      >
        <TipoAtividadeIcone tipo={c.tipoAtividade} className="size-6" />
        <span className="min-w-0 flex-1 text-sm">
          {tipoAtividadeLabels[c.tipoAtividade]} · por{" "}
          {primeiroNome(c.anfitriaoNome)}
        </span>
        <span className="text-xs text-muted">
          {c.concluidas} de {c.participantes.length}
        </span>
      </Link>
    </li>
  );
}
