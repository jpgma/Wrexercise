import {
  CalendarDays,
  Clock,
  CloudOff,
  Flame,
  Globe,
  Medal,
  Repeat,
  Target,
  type LucideIcon,
} from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { HistoryChart } from "@/components/HistoryChart";
import { PageHeader, SectionTitle } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import { useSessao } from "@/features/autenticacao";
import {
  canGerenciarDesafios,
  formatarData,
  formatarDiaEMes,
  formatarDuracao,
  formatarSemanaCurta,
  isDesafioAberto,
  situacaoLabels,
  useCancelarDesafio,
  useDesafio,
  useEditarDesafio,
  useProgresso,
  type Desafio,
} from "@/features/desafios";
import {
  LinhaParticipante,
  useClassificacao,
  useHistoricoSemanal,
} from "@/features/progresso";
import { ApiError, getErrorMessage } from "@/services/api";

function Dado({
  icon: Icon,
  titulo,
  valor,
}: {
  icon: LucideIcon;
  titulo: string;
  valor: string;
}) {
  return (
    <div className="rounded-xl bg-surface-muted p-3">
      <Icon aria-hidden="true" className="size-4 text-link" />
      <p className="mt-2 text-xs text-muted">{titulo}</p>
      <p className="text-sm font-semibold break-words">{valor}</p>
    </div>
  );
}

function ProgressoSection({ desafio }: { desafio: Desafio }) {
  const { data: usuario } = useSessao();
  const progresso = useProgresso(desafio.id);

  return (
    <>
      <SectionTitle>Progresso desta semana</SectionTitle>
      {progresso.isPending && (
        <p role="status" className="py-8 text-center text-sm text-muted">
          Carregando…
        </p>
      )}
      {progresso.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(progresso.error)}
        </p>
      )}
      {progresso.data && (
        <>
          <p className="mb-3 text-sm text-muted">
            {progresso.data.semanaAtual
              ? `Semana ${progresso.data.semanaAtual.numero} de ${progresso.data.totalSemanas}, de ${formatarDiaEMes(progresso.data.semanaAtual.inicio)} a ${formatarDiaEMes(progresso.data.semanaAtual.fim)}`
              : `${progresso.data.totalSemanas} ${progresso.data.totalSemanas === 1 ? "semana" : "semanas"} no total`}
          </p>
          {progresso.data.participantes.length === 0 ? (
            <p className="card p-4 text-sm text-muted">
              Os membros do grupo aparecerão aqui.
            </p>
          ) : (
            <div className="card p-3">
              {progresso.data.participantes.map((p) => (
                <LinhaParticipante
                  key={p.usuarioId}
                  usuarioId={p.usuarioId}
                  nomeExibicao={p.nomeExibicao}
                  verificadas={p.sessoesVerificadas}
                  provisorias={p.sessoesProvisorias}
                  meta={progresso.data.sessoesPorSemana}
                  voce={p.usuarioId === usuario?.id}
                  contaNestaSemana={p.contaNestaSemana}
                  grupoId={desafio.grupoId}
                />
              ))}
            </div>
          )}
        </>
      )}
    </>
  );
}

function ClassificacaoSection({ desafioId }: { desafioId: string }) {
  const { data: usuario } = useSessao();
  const classificacao = useClassificacao(desafioId);

  return (
    <>
      <SectionTitle>Classificação</SectionTitle>
      {classificacao.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(classificacao.error)}
        </p>
      )}
      {classificacao.data &&
        (classificacao.data.participantes.length === 0 ? (
          <p className="card p-4 text-sm text-muted">
            Ainda não há participantes neste desafio.
          </p>
        ) : (
          <ol className="card divide-y divide-border">
            {classificacao.data.participantes.map((item) => (
              <li
                key={item.usuarioId}
                className="flex items-center gap-3 p-3.5"
              >
                <span
                  className={`badge-rank ${item.posicao === 1 ? "badge-rank-1" : ""}`}
                >
                  {item.posicao}
                </span>
                <Avatar
                  nome={item.nomeExibicao}
                  id={item.usuarioId}
                  size={34}
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold">
                    {item.nomeExibicao}
                    {item.usuarioId === usuario?.id && (
                      <span className="font-normal text-muted"> (você)</span>
                    )}
                  </p>
                  <p className="text-xs text-muted">
                    {item.semanasCumpridas}{" "}
                    {item.semanasCumpridas === 1
                      ? "semana cumprida"
                      : "semanas cumpridas"}{" "}
                    · {item.sessoesVerificadas}{" "}
                    {item.sessoesVerificadas === 1
                      ? "verificada"
                      : "verificadas"}
                  </p>
                </div>
                {item.sequenciaSemanas > 0 && (
                  <span className="flex shrink-0 items-center gap-1 text-xs font-semibold">
                    <Flame aria-hidden="true" className="size-4 text-warning" />
                    {item.sequenciaSemanas}
                    <span className="sr-only">semanas seguidas</span>
                  </span>
                )}
                {item.posicao === 1 && (
                  <Medal
                    aria-label="Primeiro lugar"
                    className="size-5 shrink-0 text-link"
                  />
                )}
              </li>
            ))}
          </ol>
        ))}
    </>
  );
}

function HistoricoSection({ desafioId }: { desafioId: string }) {
  const historico = useHistoricoSemanal(desafioId);

  return (
    <>
      <SectionTitle>Seu histórico</SectionTitle>
      {historico.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(historico.error)}
        </p>
      )}
      {historico.data && (
        <HistoryChart
          titulo="Histórico do desafio"
          meta={historico.data.sessoesPorSemana}
          colunas={historico.data.semanas.map((semana) => ({
            chave: semana.numero,
            rotulo: formatarSemanaCurta(semana.inicio),
            valor: semana.sessoesVerificadas,
            provisorio: semana.sessoesProvisorias,
            naoParticipou: !semana.participou,
          }))}
        />
      )}
    </>
  );
}

function EditarDesafioForm({
  desafio,
  onFechar,
}: {
  desafio: Desafio;
  onFechar: () => void;
}) {
  const editar = useEditarDesafio(desafio.id, desafio.grupoId);
  const [nome, setNome] = useState(desafio.nome);
  const [descricao, setDescricao] = useState(desafio.descricao ?? "");

  return (
    <form
      className="mt-4 space-y-3"
      onSubmit={(event) => {
        event.preventDefault();
        editar.mutate(
          { nome: nome.trim(), descricao: descricao.trim() },
          { onSuccess: onFechar },
        );
      }}
    >
      <label className="block">
        <span className="input-label">Nome</span>
        <input
          className="input-field"
          maxLength={80}
          value={nome}
          onChange={(event) => setNome(event.target.value)}
        />
      </label>
      <label className="block">
        <span className="input-label">Descrição (opcional)</span>
        <textarea
          className="input-field h-auto min-h-20 resize-y py-3"
          maxLength={500}
          value={descricao}
          onChange={(event) => setDescricao(event.target.value)}
        />
      </label>
      <p className="text-xs text-muted">
        A meta e as datas não mudam depois da criação.
      </p>
      {editar.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(editar.error)}
        </p>
      )}
      <div className="grid grid-cols-2 gap-2">
        <button type="button" className="btn-ghost" onClick={onFechar}>
          Voltar
        </button>
        <button
          type="submit"
          className="btn-primary h-11"
          disabled={editar.isPending || nome.trim().length === 0}
        >
          {editar.isPending ? "Salvando…" : "Salvar"}
        </button>
      </div>
    </form>
  );
}

function DesafioConteudo({ desafio }: { desafio: Desafio }) {
  const cancelar = useCancelarDesafio(desafio.id, desafio.grupoId);
  const [cancelando, setCancelando] = useState(false);
  const [editando, setEditando] = useState(false);

  const aberto = isDesafioAberto(desafio.situacao);
  const podeGerenciar = aberto && canGerenciarDesafios(desafio.papel);

  const fecharDialogo = () => {
    cancelar.reset();
    setCancelando(false);
  };

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader title="Meta do grupo" back={`/grupos/${desafio.grupoId}`} />

      <div className="card p-5">
        <div className="flex items-start justify-between gap-3">
          <p className="text-xs font-semibold tracking-wide text-muted uppercase">
            {desafio.situacao === "Ativo" ? "Desafio atual" : "Desafio"}
          </p>
          <span className="chip">{situacaoLabels[desafio.situacao]}</span>
        </div>
        {editando ? (
          <EditarDesafioForm
            desafio={desafio}
            onFechar={() => setEditando(false)}
          />
        ) : (
          <>
            <h2 className="mt-1 text-2xl break-words uppercase">
              {desafio.nome}
            </h2>
            {desafio.descricao && (
              <p className="mt-2 break-words whitespace-pre-line text-muted">
                {desafio.descricao}
              </p>
            )}
            {podeGerenciar && (
              <button
                type="button"
                className="mt-2 text-sm font-semibold text-link"
                onClick={() => setEditando(true)}
              >
                Editar nome e descrição
              </button>
            )}
          </>
        )}
        <div className="mt-5 grid grid-cols-2 gap-3">
          <Dado
            icon={Repeat}
            titulo="Dias por semana"
            valor={String(desafio.sessoesPorSemana)}
          />
          <Dado
            icon={Clock}
            titulo="Mínimo por dia"
            valor={formatarDuracao(desafio.duracaoMinimaSegundos)}
          />
          <Dado
            icon={CalendarDays}
            titulo="Período"
            valor={`${formatarData(desafio.dataInicio)} – ${formatarData(desafio.dataFim)}`}
          />
          <Dado
            icon={Globe}
            titulo="Fuso do grupo"
            valor={desafio.fusoHorario.replaceAll("_", " ")}
          />
        </div>
      </div>

      <ProgressoSection desafio={desafio} />
      <ClassificacaoSection desafioId={desafio.id} />
      {desafio.participando && <HistoricoSection desafioId={desafio.id} />}

      {podeGerenciar && (
        <button
          type="button"
          className="mt-6 flex h-12 w-full items-center justify-center rounded-xl text-sm font-semibold text-danger hover:bg-danger-soft"
          onClick={() => setCancelando(true)}
        >
          Cancelar desafio
        </button>
      )}

      <ConfirmDialog
        open={cancelando}
        title="Cancelar este desafio?"
        message="O desafio deixa de valer para todo o grupo, e isso não pode ser desfeito. Ele continua visível no histórico."
        confirmLabel="Cancelar desafio"
        pending={cancelar.isPending}
        error={cancelar.isError ? getErrorMessage(cancelar.error) : undefined}
        onConfirm={() =>
          cancelar.mutate(undefined, { onSuccess: fecharDialogo })
        }
        onCancel={fecharDialogo}
      />
    </div>
  );
}

export function DesafioPage() {
  const { id = "" } = useParams();
  const desafio = useDesafio(id);

  if (desafio.isPending) return <SplashScreen />;

  if (desafio.isError) {
    const notFound =
      desafio.error instanceof ApiError && desafio.error.status === 404;
    return (
      <div className="card">
        <EmptyState
          icon={notFound ? Target : CloudOff}
          title={
            notFound ? "Desafio não encontrado" : "Não foi possível carregar"
          }
          description={
            notFound
              ? "Este desafio não existe ou você não faz parte do grupo dele."
              : getErrorMessage(desafio.error)
          }
          action={
            <Link to="/" className="btn-secondary">
              Ir para o início
            </Link>
          }
        />
      </div>
    );
  }

  return <DesafioConteudo desafio={desafio.data} />;
}
