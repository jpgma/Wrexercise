import { useState } from "react";
import { Avatar } from "@/components/Avatar";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useToast } from "@/components/Toast";
import { SituacaoBadge, type SituacaoVerificacao } from "@/features/atividades";
import { useSessao } from "@/features/autenticacao";
import { canRevisarAtividade, useGrupo } from "@/features/grupos";
import { getErrorMessage } from "@/services/api";
import { Comentarios } from "./Comentarios";
import { tipoReacaoEmojis, tipoReacaoLabels } from "./labels";
import type { RevisaoAtividadeData, TipoReacao } from "./types";
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

function BotaoReacao({
  tipo,
  label = tipoReacaoLabels[tipo],
  className,
  suaReacao,
  pending,
  onClick,
}: {
  tipo: TipoReacao;
  label?: string;
  className: string;
  suaReacao: TipoReacao | null;
  pending: boolean;
  onClick: () => void;
}) {
  const selecionada = suaReacao === tipo;
  const apagada = suaReacao !== null && !selecionada;
  // Só o "Dar moral" tem largura garantida para o texto.
  const largo = tipo === "Kudos";
  return (
    <button
      type="button"
      aria-pressed={selecionada}
      className={`${className} h-11 min-w-0 gap-1.5 px-2 ${
        selecionada
          ? "ring-2 ring-foreground ring-offset-2 ring-offset-background"
          : ""
      } ${apagada ? "opacity-40 shadow-none" : ""}`}
      disabled={pending}
      onClick={selecionada ? undefined : onClick}
    >
      <Emoji tipo={tipo} />
      <span className={largo ? "truncate" : "truncate @max-[24rem]:sr-only"}>
        {label}
      </span>
    </button>
  );
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
  const { data: usuario } = useSessao();
  const { showToast } = useToast();
  const [confirmandoRejeicao, setConfirmandoRejeicao] = useState(false);
  const [justificativa, setJustificativa] = useState("");
  const [decisao, setDecisao] =
    useState<RevisaoAtividadeData["decisao"]>("Verificada");
  const [anulando, setAnulando] = useState(false);

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

  // Ninguém decide a própria disputa (ADR 0052): outro administrador decide.
  const podeDecidir =
    grupo.data !== undefined && canRevisarAtividade(grupo.data.papel) && !voce;
  const emDisputa = podeDecidir && situacaoVerificacao === "EmRevisao";
  // Antes da disputa, o administrador só pode anular um registro errado (ADR 0054).
  const podeAnular =
    podeDecidir && situacaoVerificacao === "AguardandoFeedback";
  const mostrarRevisaoAdmin = emDisputa || (podeAnular && anulando);
  const decisaoEnviada = emDisputa ? decisao : "Anulada";

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
              const { texto, sua } = descricoes[item.tipo];
              const ehVoce = item.usuarioId === usuario?.id;
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
                    {ehVoce ? (
                      <b>{sua}</b>
                    ) : (
                      <>
                        <b>{item.nomeExibicao.trim().split(/\s+/)[0]}</b>{" "}
                        {texto}
                      </>
                    )}
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

      {podeReagir && (
        <div className="space-y-2">
          {/* Uma linha: Rejeitar (1/4), Suspeito (1/4), Dar moral (2/4). A
              opção escolhida fica destacada e as outras, apagadas, mas ainda
              tocáveis para trocar a reação até o prazo acabar. Nos botões
              estreitos, o texto some quando não cabe e fica só o símbolo. */}
          <div className="@container grid grid-cols-4 gap-2">
            <BotaoReacao
              tipo="Rejeitar"
              className="btn-danger"
              suaReacao={suaReacao}
              pending={registrar.isPending}
              onClick={() => setConfirmandoRejeicao(true)}
            />
            <BotaoReacao
              tipo="Suspeito"
              className="btn-warning"
              suaReacao={suaReacao}
              pending={registrar.isPending}
              onClick={() =>
                reagir("Suspeito", "Marcado como suspeito para revisão.")
              }
            />
            <BotaoReacao
              tipo="Kudos"
              label="Dar moral"
              className="btn-primary col-span-2"
              suaReacao={suaReacao}
              pending={registrar.isPending}
              onClick={() =>
                reagir("Kudos", `Você deu moral para ${primeiroNome}!`)
              }
            />
          </div>
          {registrar.isError && !confirmandoRejeicao && (
            <p role="alert" className="form-error">
              {getErrorMessage(registrar.error)}
            </p>
          )}
        </div>
      )}

      <Comentarios atividadeId={atividadeId} />

      {podeAnular && !anulando && (
        <div className="flex flex-wrap items-center justify-between gap-2 px-1">
          <p className="text-sm text-muted">Registro errado ou em dobro?</p>
          <button
            type="button"
            className="btn-ghost"
            onClick={() => setAnulando(true)}
          >
            Anular atividade
          </button>
        </div>
      )}

      {mostrarRevisaoAdmin && (
        <div className="card p-5">
          <h3 className="text-base font-bold">
            {emDisputa ? "Revisão administrativa" : "Anular atividade"}
          </h3>
          {emDisputa ? (
            <label className="mt-4 block">
              <span className="input-label">Decisão</span>
              <select
                className="input-field"
                value={decisao}
                onChange={(event) =>
                  setDecisao(
                    event.target.value as RevisaoAtividadeData["decisao"],
                  )
                }
              >
                <option value="Verificada">Aprovar atividade</option>
                <option value="Rejeitada">Rejeitar atividade</option>
                <option value="Anulada">Anular (registro errado)</option>
              </select>
            </label>
          ) : (
            <p className="mt-2 text-sm text-muted">
              A atividade deixa de contar e não recebe mais reações. Use para
              registro errado ou em dobro, não para suspeita.
            </p>
          )}
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
              revisar.mutate({
                decisao: decisaoEnviada,
                justificativa: justificativa.trim(),
              })
            }
          >
            {revisar.isPending
              ? "Registrando…"
              : emDisputa
                ? "Registrar decisão"
                : "Anular atividade"}
          </button>
          {!emDisputa && (
            <button
              type="button"
              className="btn-ghost mt-2 w-full"
              disabled={revisar.isPending}
              onClick={() => {
                revisar.reset();
                setAnulando(false);
              }}
            >
              Cancelar
            </button>
          )}
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
