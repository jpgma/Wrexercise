import { CloudOff, Flame, Trophy, Users } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { HistoryChart } from "@/components/HistoryChart";
import { PageHeader, SectionTitle } from "@/components/PageHeader";
import { ProgressRing } from "@/components/ProgressRing";
import { SplashScreen } from "@/components/SplashScreen";
import { useAtividadesFeed } from "@/features/atividades";
import { useSessao } from "@/features/autenticacao";
import { formatarSemanaCurta } from "@/features/desafios";
import { FeedAtividade } from "@/features/feed";
import {
  canAlterarPapel,
  canRemover,
  papelLabels,
  useAlterarPapel,
  useGrupo,
  useRemoverMembro,
  useTransferirGrupo,
} from "@/features/grupos";
import { RedefinirAvatar } from "@/features/jogo";
import { SituacaoParticipante } from "@/features/progresso";
import { usePerfilMembro } from "@/features/usuarios";
import { ApiError, getErrorMessage } from "@/services/api";

type Acao = "promover" | "rebaixar" | "remover" | "transferir";

function getTextos(acao: Acao, nome: string) {
  switch (acao) {
    case "promover":
      return {
        title: `Tornar ${nome} administrador?`,
        message: "Administradores podem convidar pessoas e remover membros.",
        confirmLabel: "Tornar administrador",
      };
    case "rebaixar":
      return {
        title: `Tornar ${nome} membro?`,
        message:
          "Essa pessoa deixará de poder convidar e remover. O link de convite que ela criou continua válido.",
        confirmLabel: "Tornar membro",
      };
    case "remover":
      return {
        title: `Remover ${nome} do grupo?`,
        message:
          "Essa pessoa perderá o acesso ao grupo, e o link de convite atual será cancelado.",
        confirmLabel: "Remover",
      };
    case "transferir":
      return {
        title: `Transferir o grupo para ${nome}?`,
        message:
          "Essa pessoa passará a ser a proprietária e você se tornará administrador. Só ela poderá desfazer isso.",
        confirmLabel: "Transferir",
      };
  }
}

export function MembroPage() {
  const { id: grupoId = "", usuarioId = "" } = useParams();
  const navigate = useNavigate();
  const { data: usuario } = useSessao();
  const grupo = useGrupo(grupoId);
  const perfil = usePerfilMembro(grupoId, usuarioId);
  const feed = useAtividadesFeed(grupoId, 5, undefined, true, usuarioId);
  const alterarPapel = useAlterarPapel(grupoId);
  const remover = useRemoverMembro(grupoId);
  const transferir = useTransferirGrupo(grupoId);
  const [acao, setAcao] = useState<Acao | null>(null);

  if (perfil.isPending) return <SplashScreen />;

  if (perfil.isError) {
    const notFound =
      perfil.error instanceof ApiError && perfil.error.status === 404;
    return (
      <div className="mx-auto max-w-xl">
        <PageHeader title="Membro" back={`/grupos/${grupoId}`} />
        <div className="card">
          <EmptyState
            icon={notFound ? Users : CloudOff}
            title={
              notFound ? "Membro não encontrado" : "Não foi possível carregar"
            }
            description={
              notFound
                ? "Essa pessoa não faz parte do grupo."
                : getErrorMessage(perfil.error)
            }
          />
        </div>
      </div>
    );
  }

  const { nomeExibicao, papel, desafio } = perfil.data;
  const primeiroNome = nomeExibicao.trim().split(/\s+/)[0] ?? nomeExibicao;
  const meuPapel = grupo.data?.papel;
  const voce = usuario?.id === usuarioId;
  const podeAlterar =
    !voce && meuPapel !== undefined && canAlterarPapel(meuPapel, papel);
  const podeRemover =
    !voce && meuPapel !== undefined && canRemover(meuPapel, papel);
  const podeTransferir = !voce && meuPapel === "Proprietario";
  const podeRedefinirAvatar =
    !voce && (meuPapel === "Proprietario" || meuPapel === "Administrador");
  const atividades = feed.data?.pages[0]?.atividades ?? [];

  const mutacaoAtiva =
    acao === "promover" || acao === "rebaixar"
      ? alterarPapel
      : acao === "remover"
        ? remover
        : acao === "transferir"
          ? transferir
          : undefined;

  const fechar = () => {
    alterarPapel.reset();
    remover.reset();
    transferir.reset();
    setAcao(null);
  };

  const confirmar = () => {
    if (acao === "promover" || acao === "rebaixar") {
      alterarPapel.mutate(
        { usuarioId, papel: acao === "promover" ? "Administrador" : "Membro" },
        { onSuccess: fechar },
      );
    } else if (acao === "remover") {
      remover.mutate(usuarioId, {
        onSuccess: () => void navigate(`/grupos/${grupoId}`, { replace: true }),
      });
    } else if (acao === "transferir") {
      transferir.mutate(usuarioId, { onSuccess: fechar });
    }
  };

  const textos = acao
    ? getTextos(acao, nomeExibicao)
    : { title: "", message: "", confirmLabel: "" };

  return (
    <>
      <PageHeader title={primeiroNome} back={`/grupos/${grupoId}`} />

      <div className="grid gap-5 lg:grid-cols-[1fr_1.3fr]">
        <div className="space-y-4">
          <div className="card flex flex-col items-center p-6 text-center">
            <Avatar nome={nomeExibicao} id={usuarioId} size={84} />
            <p className="mt-3 font-display text-xl break-words">
              {nomeExibicao}
              {voce && <span className="font-normal text-muted"> (você)</span>}
            </p>
            <p className="text-xs font-medium text-muted">
              {papelLabels[papel]}
            </p>
            {desafio?.emAndamento &&
              (desafio.contaNestaSemana ? (
                <>
                  <div className="mt-5">
                    <ProgressRing
                      label={`Progresso de ${nomeExibicao} nesta semana`}
                      valor={desafio.sessoesVerificadas}
                      provisorio={desafio.sessoesProvisorias}
                      max={desafio.sessoesPorSemana}
                      size={120}
                    >
                      <div>
                        <p className="font-display text-3xl">
                          {Math.min(
                            desafio.sessoesVerificadas,
                            desafio.sessoesPorSemana,
                          )}
                          /{desafio.sessoesPorSemana}
                        </p>
                        <p className="text-2xs text-muted">esta semana</p>
                      </div>
                    </ProgressRing>
                  </div>
                  <div className="mt-3">
                    <SituacaoParticipante
                      verificadas={desafio.sessoesVerificadas}
                      meta={desafio.sessoesPorSemana}
                    />
                  </div>
                </>
              ) : (
                <p className="mt-4 text-sm text-muted">
                  Entrou nesta semana: conta a partir da próxima.
                </p>
              ))}
          </div>

          {desafio && (
            <>
              <div className="grid grid-cols-2 gap-3">
                <div className="card p-4">
                  <Flame aria-hidden="true" className="size-5 text-warning" />
                  <p className="mt-2 font-display text-2xl">
                    {desafio.sequenciaSemanas}
                  </p>
                  <p className="text-xs text-muted">
                    {desafio.sequenciaSemanas === 1
                      ? "semana seguida"
                      : "semanas seguidas"}
                  </p>
                </div>
                <div className="card p-4">
                  <Trophy aria-hidden="true" className="size-5 text-link" />
                  <p className="mt-2 font-display text-2xl">
                    {desafio.semanasCumpridas}/{desafio.semanasDisputadas}
                  </p>
                  <p className="text-xs text-muted">metas batidas</p>
                </div>
              </div>
              <HistoryChart
                titulo={desafio.nome}
                meta={desafio.sessoesPorSemana}
                colunas={desafio.semanas.map((semana) => ({
                  chave: semana.numero,
                  rotulo: formatarSemanaCurta(semana.inicio),
                  valor: semana.sessoesVerificadas,
                  provisorio: semana.sessoesProvisorias,
                  naoParticipou: !semana.participou,
                }))}
              />
            </>
          )}

          {podeRedefinirAvatar && (
            <RedefinirAvatar
              grupoId={grupoId}
              usuarioId={usuarioId}
              nome={primeiroNome}
            />
          )}

          {(podeAlterar || podeRemover || podeTransferir) && (
            <div className="card divide-y divide-border overflow-hidden">
              {podeAlterar && (
                <button
                  type="button"
                  className="block w-full p-4 text-left text-sm font-semibold hover:bg-surface-muted"
                  onClick={() =>
                    setAcao(papel === "Membro" ? "promover" : "rebaixar")
                  }
                >
                  {papel === "Membro"
                    ? "Tornar administrador"
                    : "Tornar membro"}
                </button>
              )}
              {podeTransferir && (
                <button
                  type="button"
                  className="block w-full p-4 text-left text-sm font-semibold hover:bg-surface-muted"
                  onClick={() => setAcao("transferir")}
                >
                  Transferir grupo
                </button>
              )}
              {podeRemover && (
                <button
                  type="button"
                  className="block w-full p-4 text-left text-sm font-semibold text-danger hover:bg-danger-soft"
                  onClick={() => setAcao("remover")}
                >
                  Remover do grupo
                </button>
              )}
            </div>
          )}
        </div>

        <div>
          <SectionTitle
            action={
              <Link
                to={`/grupos/${grupoId}/atividades`}
                className="text-sm font-semibold text-link"
              >
                Ver todas
              </Link>
            }
          >
            Atividades recentes
          </SectionTitle>
          {feed.isError && (
            <p role="alert" className="form-error">
              {getErrorMessage(feed.error)}
            </p>
          )}
          {feed.data && atividades.length === 0 && (
            <p className="card p-4 text-sm text-muted">
              {primeiroNome} ainda não registrou treinos neste grupo.
            </p>
          )}
          <div className="space-y-3">
            {atividades.map((atividade) => (
              <FeedAtividade key={atividade.id} atividade={atividade} />
            ))}
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={acao !== null}
        title={textos.title}
        message={textos.message}
        confirmLabel={textos.confirmLabel}
        pending={mutacaoAtiva?.isPending ?? false}
        error={
          mutacaoAtiva?.isError
            ? getErrorMessage(mutacaoAtiva.error)
            : undefined
        }
        onConfirm={confirmar}
        onCancel={fechar}
      />
    </>
  );
}
