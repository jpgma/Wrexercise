import { CalendarDays, Clock, CloudOff } from "lucide-react";
import { Link, useParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import {
  AvisosAtividade,
  EvidenciasAtividade,
  ExcluirAtividade,
  formatarDataRelativa,
  formatarDuracao,
  tipoAtividadeLabels,
  TipoAtividadeIcone,
  useAtividade,
} from "@/features/atividades";
import { ReacoesAtividade } from "@/features/reacoes";
import { ApiError, getErrorMessage } from "@/services/api";

export function AtividadePage() {
  const { id = "" } = useParams();
  const atividade = useAtividade(id);

  if (atividade.isPending) return <SplashScreen />;

  if (atividade.isError) {
    const notFound =
      atividade.error instanceof ApiError && atividade.error.status === 404;
    return (
      <div className="card">
        <EmptyState
          icon={CloudOff}
          title={
            notFound ? "Atividade não encontrada" : "Não foi possível carregar"
          }
          description={
            notFound
              ? "Esta atividade não existe ou você não tem acesso a ela."
              : getErrorMessage(atividade.error)
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

  const {
    usuarioId,
    nomeExibicao,
    grupoId,
    grupoNome,
    tipo,
    duracaoSegundos,
    startedAt,
    descricao,
    situacaoVerificacao,
    voce,
    evidencias,
    avisos,
    exclusaoPermitidaAte,
  } = atividade.data;

  return (
    <div className="mx-auto max-w-4xl">
      <PageHeader title="Atividade" back={`/grupos/${grupoId}/atividades`} />

      <div className="grid gap-5 lg:grid-cols-[1.2fr_1fr]">
        <div className="card overflow-hidden">
          <EvidenciasAtividade
            atividadeId={id}
            evidencias={evidencias}
            legenda={atividade.data}
          />

          <div className="p-5">
            <div className="flex items-center gap-3">
              <Avatar nome={nomeExibicao} id={usuarioId} size={44} />
              <div className="min-w-0">
                <p className="truncate font-semibold">
                  {nomeExibicao}
                  {voce && (
                    <span className="font-normal text-muted"> (você)</span>
                  )}
                </p>
                <p className="truncate text-xs text-muted">{grupoNome}</p>
              </div>
            </div>

            <div className="mt-5 flex items-center gap-3">
              <TipoAtividadeIcone tipo={tipo} className="size-12" />
              <div className="min-w-0">
                <p className="font-display text-xl">
                  {tipoAtividadeLabels[tipo]}
                </p>
                <p className="flex flex-wrap items-center gap-x-3 text-sm text-muted">
                  <span className="inline-flex items-center gap-1">
                    <Clock aria-hidden="true" className="size-4" />
                    {formatarDuracao(duracaoSegundos)}
                  </span>
                  <span className="inline-flex items-center gap-1">
                    <CalendarDays aria-hidden="true" className="size-4" />
                    {formatarDataRelativa(startedAt)}
                  </span>
                </p>
              </div>
            </div>

            {descricao && (
              <p className="mt-4 text-[15px] leading-relaxed break-words">
                {descricao}
              </p>
            )}
            {avisos.length > 0 && (
              <div className="mt-4">
                <AvisosAtividade avisos={avisos} />
              </div>
            )}
            {exclusaoPermitidaAte && (
              <ExcluirAtividade
                atividadeId={id}
                grupoId={grupoId}
                permitidaAte={exclusaoPermitidaAte}
              />
            )}
          </div>
        </div>

        <ReacoesAtividade
          atividadeId={id}
          grupoId={grupoId}
          situacaoVerificacao={situacaoVerificacao}
          voce={voce}
          nomeAutor={nomeExibicao}
        />
      </div>
    </div>
  );
}
