import { CheckCircle2, CloudOff, Lock, LockOpen } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { Segments } from "@/components/Segments";
import { SplashScreen } from "@/components/SplashScreen";
import {
  AvisosAtividade,
  EvidenciasAtividade,
  formatarDataRelativa,
  formatarDuracao,
  tipoAtividadeLabels,
  TipoAtividadeIcone,
  useAtividade,
  useFilaAvaliacao,
} from "@/features/atividades";
import { useProgresso } from "@/features/desafios";
import { useResumoGrupo } from "@/features/progresso";
import { ReacoesAtividade } from "@/features/reacoes";
import { getErrorMessage } from "@/services/api";

function SemanaDoAutor({
  desafioId,
  usuarioId,
  nome,
}: {
  desafioId: string;
  usuarioId: string;
  nome: string;
}) {
  const progresso = useProgresso(desafioId);
  const participante = progresso.data?.participantes.find(
    (p) => p.usuarioId === usuarioId,
  );
  if (!progresso.data || !participante) return null;
  const meta = progresso.data.sessoesPorSemana;

  return (
    <div className="card mt-4 flex items-center gap-3 p-4">
      <Avatar nome={nome} id={usuarioId} size={40} />
      <div className="min-w-0 flex-1">
        <p className="mb-1.5 text-sm">
          <b>{nome.trim().split(/\s+/)[0]}</b> está com{" "}
          {participante.sessoesVerificadas} de {meta} nesta semana
        </p>
        <Segments
          label={`Progresso de ${nome}`}
          valor={participante.sessoesVerificadas}
          provisorio={participante.sessoesProvisorias}
          max={meta}
        />
      </div>
    </div>
  );
}

/** Igual a `--animate-avanco` em styles/index.css. */
const tempoDeAvancoMs = 3000;

/**
 * Segue sozinho para `para` depois de 3 segundos, com uma barra de progresso. Para quando a
 * pessoa toca em "Ficar aqui" ou começa a escrever um comentário.
 */
function AvancoAutomatico({
  para,
  texto,
  acao,
}: {
  para: string;
  texto: string;
  acao: string;
}) {
  const navigate = useNavigate();
  const [parado, setParado] = useState(false);

  useEffect(() => {
    if (parado) return;
    const timer = setTimeout(
      () => void navigate(para, { replace: true }),
      tempoDeAvancoMs,
    );
    return () => clearTimeout(timer);
  }, [para, parado, navigate]);

  useEffect(() => {
    const pararAoEscrever = ({ target }: FocusEvent) => {
      if (target instanceof HTMLTextAreaElement) setParado(true);
    };
    document.addEventListener("focusin", pararAoEscrever);
    return () => document.removeEventListener("focusin", pararAoEscrever);
  }, []);

  if (parado) {
    return (
      <Link to={para} replace className="btn-secondary mt-4 w-full">
        {acao}
      </Link>
    );
  }

  return (
    <div role="status" className="card mt-5 animate-slide-up p-4">
      <p className="text-sm font-semibold">{texto}</p>
      <div
        aria-hidden="true"
        className="mt-3 h-1.5 overflow-hidden rounded-full bg-soft"
      >
        <div className="h-full origin-left animate-avanco rounded-full bg-progress" />
      </div>
      <div className="mt-3 flex gap-2">
        <button
          type="button"
          className="btn-ghost flex-1"
          onClick={() => setParado(true)}
        >
          Ficar aqui
        </button>
        <Link to={para} replace className="btn-primary flex-1">
          {acao}
        </Link>
      </div>
    </div>
  );
}

/** Onde a pessoa está na fila que separa a tela de registro do formulário. */
function EtapaAvaliacao({
  posicao,
  feitas,
  total,
}: {
  posicao: number;
  feitas: number;
  total: number;
}) {
  return (
    <div className="card mb-4 p-4">
      <div className="mb-2 flex items-center justify-between gap-3 text-sm">
        <b>
          Avaliação {posicao} de {total}
        </b>
        <span className="flex items-center gap-1 text-xs text-muted">
          <Lock aria-hidden="true" className="size-3.5" /> Depois: seu treino
        </span>
      </div>
      <Segments label="Avaliações feitas" valor={feitas} max={total} />
    </div>
  );
}

/** Fim da fila vindo do registro: o formulário está liberado. */
function Liberado({ grupoId }: { grupoId: string }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // O cartão aparece abaixo das reações; no celular, ele costuma ficar fora da tela.
    const reduzir = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;
    ref.current?.scrollIntoView({
      behavior: reduzir ? "auto" : "smooth",
      block: "center",
    });
    if ("vibrate" in navigator) navigator.vibrate([30, 60, 30]);
  }, []);

  return (
    <div
      ref={ref}
      role="status"
      className="card mt-5 flex animate-slide-up flex-col items-center p-6 text-center"
    >
      <div className="relative grid size-20 place-items-center">
        <div
          aria-hidden="true"
          className="absolute inset-0 animate-burst bg-reward burst"
        />
        <div className="relative grid size-16 animate-pop place-items-center rounded-xl border-2 border-outline bg-progress text-on-progress shadow-sticker">
          <LockOpen aria-hidden="true" className="size-8" />
        </div>
      </div>
      <h2 className="mt-4 text-3xl uppercase">Liberado!</h2>
      <p className="mt-1 text-sm text-muted">
        Valeu por avaliar a galera. Agora é a sua vez.
      </p>
      <Link
        to={`/grupos/${grupoId}/atividades/nova`}
        replace
        className="btn-primary mt-5 w-full"
      >
        Registrar meu treino
      </Link>
    </div>
  );
}

export function RevisarPage() {
  const { id: grupoId = "", atividadeId = "" } = useParams();
  const atividade = useAtividade(atividadeId);
  const fila = useFilaAvaliacao(grupoId);
  const resumo = useResumoGrupo(grupoId);
  // Veio do registro: avaliar a fila é o que libera o formulário.
  const [busca] = useSearchParams();
  const depoisRegistrar = busca.get("depois") === "registrar";
  // Tamanho da fila quando a pessoa começou, para mostrar "Avaliação X de N".
  const totalInicial = Number(busca.get("total")) || 0;
  const sufixo = depoisRegistrar
    ? `?depois=registrar&total=${totalInicial}`
    : "";
  // Só avança sozinho depois de uma reação feita nesta tela, não ao abrir uma já avaliada.
  const [reagidaAqui, setReagidaAqui] = useState<string | null>(null);
  const avancar = reagidaAqui === atividadeId;

  const naFila = fila.data ?? [];
  const avaliada = !naFila.some(({ id }) => id === atividadeId);
  const proxima = naFila.find(({ id }) => id !== atividadeId);
  // A fila pode crescer no meio do caminho: o total nunca fica menor que o que ainda falta.
  const total = Math.max(totalInicial, naFila.length + (avaliada ? 1 : 0));
  const feitas = total - naFila.length;
  // Carrega a próxima durante a contagem: a troca de tela fica sem tela de carregamento.
  useAtividade(proxima?.id ?? "", avancar && proxima !== undefined);

  if (atividade.isPending) return <SplashScreen />;

  if (atividade.isError) {
    return (
      <div className="card">
        <EmptyState
          icon={CloudOff}
          title="Não foi possível carregar"
          description={getErrorMessage(atividade.error)}
          action={
            <Link to={`/grupos/${grupoId}/inicio`} className="btn-secondary">
              Voltar ao início
            </Link>
          }
        />
      </div>
    );
  }

  const {
    usuarioId,
    nomeExibicao,
    tipo,
    duracaoSegundos,
    startedAt,
    descricao,
    situacaoVerificacao,
    voce,
    evidencias,
    avisos,
  } = atividade.data;
  return (
    <div key={atividadeId} className="mx-auto max-w-xl animate-fade-in">
      <PageHeader
        title="Avaliar atividade"
        back={`/grupos/${grupoId}/inicio`}
        subtitle={depoisRegistrar ? undefined : `${naFila.length} na fila`}
      />
      {depoisRegistrar && fila.isSuccess && (
        <EtapaAvaliacao
          posicao={avaliada ? feitas : feitas + 1}
          feitas={feitas}
          total={total}
        />
      )}
      <p className="mb-4 text-sm text-muted">
        Confira o registro com calma. Na dúvida, prefira “Suspeito” e explique
        nos comentários em vez de rejeitar.
      </p>

      <div className="card overflow-hidden">
        <EvidenciasAtividade
          atividadeId={atividadeId}
          evidencias={evidencias}
          legenda={atividade.data}
        />
        <div className="p-5">
          <div className="flex items-center gap-3">
            <TipoAtividadeIcone tipo={tipo} className="size-11" />
            <div className="min-w-0 flex-1">
              <p className="font-semibold">
                {tipoAtividadeLabels[tipo]} · {formatarDuracao(duracaoSegundos)}
              </p>
              <p className="text-xs text-muted">
                {formatarDataRelativa(startedAt)}
              </p>
            </div>
          </div>
          {descricao && (
            <p className="mt-3 text-sm break-words">“{descricao}”</p>
          )}
          {avisos.length > 0 && (
            <div className="mt-4">
              <AvisosAtividade avisos={avisos} />
            </div>
          )}
        </div>
      </div>

      {resumo.data?.desafio && (
        <SemanaDoAutor
          desafioId={resumo.data.desafio.id}
          usuarioId={usuarioId}
          nome={nomeExibicao}
        />
      )}

      <div className="mt-5">
        <ReacoesAtividade
          atividadeId={atividadeId}
          grupoId={grupoId}
          situacaoVerificacao={situacaoVerificacao}
          voce={voce}
          nomeAutor={nomeExibicao}
          onReagida={() => setReagidaAqui(atividadeId)}
        />
      </div>

      {fila.isSuccess &&
        avaliada &&
        (proxima ? (
          avancar ? (
            <AvancoAutomatico
              para={`/grupos/${grupoId}/revisar/${proxima.id}${sufixo}`}
              texto={`Indo para a próxima… ${naFila.length === 1 ? "Falta 1" : `Faltam ${naFila.length}`}.`}
              acao="Próxima avaliação"
            />
          ) : (
            <Link
              to={`/grupos/${grupoId}/revisar/${proxima.id}${sufixo}`}
              replace
              className="btn-secondary mt-4 w-full"
            >
              Próxima avaliação
            </Link>
          )
        ) : depoisRegistrar ? (
          <Liberado grupoId={grupoId} />
        ) : (
          <div className="card mt-5">
            <EmptyState
              icon={CheckCircle2}
              title="Tudo em dia"
              description="Ninguém espera sua avaliação agora. Valeu por fazer a sua parte!"
              action={
                <Link to={`/grupos/${grupoId}/inicio`} className="btn-primary">
                  Voltar ao início
                </Link>
              }
            />
          </div>
        ))}
    </div>
  );
}
