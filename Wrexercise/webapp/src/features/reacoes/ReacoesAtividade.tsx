import { useState } from "react";
import { Avatar } from "@/components/Avatar";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useToast } from "@/components/Toast";
import { SituacaoBadge, type SituacaoVerificacao } from "@/features/atividades";
import { canRevisarAtividade, useGrupo } from "@/features/grupos";
import { getErrorMessage } from "@/services/api";
import { Comentarios } from "./Comentarios";
import { tipoReacaoEmojis, tipoReacaoLabels } from "./labels";
import type { TipoReacao } from "./types";
import {
  useReacoes,
  useRegistrarReacao,
  useRevisarAtividade,
} from "./useReacoes";

const descricoes: Record<TipoReacao, { texto: string; sua: string }> = {
  Kudos: { texto: "deu moral", sua: "Você deu moral" },
  Suspeito: { texto: "achou suspeito", sua: "Você marcou como suspeito" },
  Rejeitar: { texto: "rejeitou", sua: "Você rejeitou" },
};

function Emoji({ tipo }: { tipo: TipoReacao }) {
  return <span aria-hidden="true">{tipoReacaoEmojis[tipo]}</span>;
}

export function ReacoesAtividade({
  atividadeId,
  grupoId,
  situacaoVerificacao,
  voce,
  nomeAutor,
  onReagida,
}: {
  atividadeId: string;
  grupoId: string;
  situacaoVerificacao: SituacaoVerificacao;
  voce: boolean;
  nomeAutor: string;
  /** Chamado depois que a reação de quem usa o aplicativo é registrada. */
  onReagida?: () => void;
}) {
  const reacoes = useReacoes(atividadeId);
  const registrar = useRegistrarReacao(atividadeId);
  const revisar = useRevisarAtividade(atividadeId);
  const grupo = useGrupo(grupoId);
  const { showToast } = useToast();
  const [confirmandoRejeicao, setConfirmandoRejeicao] = useState(false);
  const [justificativa, setJustificativa] = useState("");
  const [decisao, setDecisao] = useState<"Verificada" | "Rejeitada">(
    "Verificada",
  );

  const primeiroNome = nomeAutor.trim().split(/\s+/)[0];

  const reagir = (tipo: TipoReacao, mensagem: string) =>
    registrar.mutate(tipo, {
      onSuccess: () => {
        setConfirmandoRejeicao(false);
        showToast(mensagem);
        onReagida?.();
      },
    });

  if (reacoes.isPending) {
    return (
      <p role="status" className="card p-5 text-sm text-muted">
        Carregando reações…
      </p>
    );
  }

  if (reacoes.isError) {
    return (
      <p role="alert" className="form-error">
        {getErrorMessage(reacoes.error)}
      </p>
    );
  }

  const {
    totalRevisores,
    rejeicoesNecessarias,
    rejeitar,
    suaReacao,
    voceERevisor,
    janelaAberta,
    reacoes: lista,
  } = reacoes.data;

  const podeReagir =
    !voce &&
    voceERevisor &&
    janelaAberta &&
    (situacaoVerificacao === "AguardandoFeedback" ||
      situacaoVerificacao === "EmRevisao");

  const mostrarRevisaoAdmin =
    grupo.data !== undefined &&
    canRevisarAtividade(grupo.data.papel) &&
    situacaoVerificacao === "EmRevisao";

  return (
    <div className="space-y-5">
      <div className="card p-5">
        <div className="flex items-center justify-between gap-2">
          <p className="text-sm font-semibold">Verificação</p>
          <SituacaoBadge situacao={situacaoVerificacao} />
        </div>
        {totalRevisores === 0 ? (
          <p className="mt-4 text-sm text-muted">
            Não havia outros membros no grupo quando a atividade foi registrada,
            então ela foi verificada automaticamente.
          </p>
        ) : (
          <ul className="mt-4 space-y-3">
            {lista.length === 0 && (
              <li className="text-sm text-muted">Ninguém reagiu ainda.</li>
            )}
            {lista.map((item) => {
              const { texto } = descricoes[item.tipo];
              return (
                <li
                  key={item.usuarioId}
                  className="flex items-center gap-3 text-sm"
                >
                  <Avatar
                    nome={item.nomeExibicao}
                    id={item.usuarioId}
                    size={30}
                  />
                  <span className="min-w-0 flex-1 truncate">
                    <b>{item.nomeExibicao.trim().split(/\s+/)[0]}</b> {texto}
                  </span>
                  <Emoji tipo={item.tipo} />
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {situacaoVerificacao === "EmRevisao" && (
        <p className="rounded-2xl bg-warning-soft p-4 text-sm text-on-warning-soft">
          <Emoji tipo="Rejeitar" /> {rejeitar} de {rejeicoesNecessarias}{" "}
          {rejeicoesNecessarias === 1
            ? "rejeição necessária"
            : "rejeições necessárias"}
          .{" "}
          {janelaAberta
            ? "Abaixo disso, um administrador decide."
            : "O prazo acabou, e um administrador decide."}
        </p>
      )}

      {voce && totalRevisores > 0 && (
        <p className="rounded-2xl bg-surface-muted p-4 text-sm text-muted">
          Essa atividade é sua. Seus amigos vão avaliar.
        </p>
      )}

      {!podeReagir && suaReacao && (
        <p className="rounded-2xl bg-surface-muted p-4 text-sm font-medium">
          {descricoes[suaReacao].sua}
        </p>
      )}

      {podeReagir && (
        <div className="space-y-2">
          {suaReacao && (
            <div className="rounded-2xl bg-surface-muted p-4 text-sm">
              <p className="font-medium">{descricoes[suaReacao].sua}</p>
              <p className="mt-1 text-muted">
                Trocar reação: escolha outra opção até o prazo acabar.
              </p>
            </div>
          )}
          <button
            type="button"
            className="btn-primary w-full"
            disabled={registrar.isPending || suaReacao === "Kudos"}
            onClick={() =>
              reagir("Kudos", `Você deu moral para ${primeiroNome}!`)
            }
          >
            <Emoji tipo="Kudos" />
            Dar moral
          </button>
          <div className="grid grid-cols-2 gap-2">
            <button
              type="button"
              className="btn-warning"
              disabled={registrar.isPending || suaReacao === "Suspeito"}
              onClick={() =>
                reagir("Suspeito", "Marcado como suspeito para revisão.")
              }
            >
              <Emoji tipo="Suspeito" />
              {tipoReacaoLabels.Suspeito}
            </button>
            <button
              type="button"
              className="btn-danger"
              disabled={registrar.isPending || suaReacao === "Rejeitar"}
              onClick={() => setConfirmandoRejeicao(true)}
            >
              <Emoji tipo="Rejeitar" />
              {tipoReacaoLabels.Rejeitar}
            </button>
          </div>
          <p className="pt-1 text-center text-xs text-muted">
            “Suspeito” sinaliza uma dúvida, e um administrador decide.
          </p>
          {registrar.isError && !confirmandoRejeicao && (
            <p role="alert" className="form-error">
              {getErrorMessage(registrar.error)}
            </p>
          )}
        </div>
      )}

      <Comentarios atividadeId={atividadeId} />

      {mostrarRevisaoAdmin && (
        <div className="card p-5">
          <h3 className="text-base font-bold">Revisão administrativa</h3>
          <label className="mt-4 block">
            <span className="input-label">Decisão</span>
            <select
              className="input-field"
              value={decisao}
              onChange={(event) =>
                setDecisao(event.target.value as "Verificada" | "Rejeitada")
              }
            >
              <option value="Verificada">Aprovar atividade</option>
              <option value="Rejeitada">Rejeitar atividade</option>
            </select>
          </label>
          <label className="mt-3 block">
            <span className="input-label">Justificativa</span>
            <textarea
              className="input-field h-auto min-h-20 resize-y py-3"
              maxLength={500}
              value={justificativa}
              onChange={(event) => setJustificativa(event.target.value)}
              placeholder="Explique brevemente a decisão."
            />
          </label>
          <button
            type="button"
            className="btn-primary mt-4 w-full"
            disabled={revisar.isPending || justificativa.trim().length === 0}
            onClick={() =>
              revisar.mutate({ decisao, justificativa: justificativa.trim() })
            }
          >
            {revisar.isPending ? "Registrando…" : "Registrar decisão"}
          </button>
          {revisar.isError && (
            <p role="alert" className="form-error mt-3">
              {getErrorMessage(revisar.error)}
            </p>
          )}
        </div>
      )}

      <ConfirmDialog
        open={confirmandoRejeicao}
        title="Rejeitar atividade?"
        message={`Isso pode fazer este treino de ${primeiroNome} deixar de contar na meta. Se você só tem dúvidas, prefira “Suspeito”.`}
        confirmLabel="Rejeitar atividade"
        pending={registrar.isPending}
        error={registrar.isError ? getErrorMessage(registrar.error) : undefined}
        onConfirm={() => reagir("Rejeitar", "Atividade rejeitada.")}
        onCancel={() => {
          registrar.reset();
          setConfirmandoRejeicao(false);
        }}
      />
    </div>
  );
}
