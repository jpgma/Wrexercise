import {
  CheckCircle2,
  ChevronRight,
  CloudOff,
  LogOut,
  Plus,
  UserPlus,
  Users,
} from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { SectionTitle } from "@/components/PageHeader";
import { Segments } from "@/components/Segments";
import { SplashScreen } from "@/components/SplashScreen";
import { useAtividadesFeed } from "@/features/atividades";
import { useSessao } from "@/features/autenticacao";
import {
  canGerenciarDesafios,
  formatarData,
  formatarMeta,
  formatarTempoAteFim,
  getTotalSemanas,
  RegrasDesafio,
  situacaoLabels,
  useDesafio,
  useDesafios,
  useProgresso,
  type Desafio,
} from "@/features/desafios";
import { AvisoAvaliacao, FeedAtividade } from "@/features/feed";
import {
  canConvidar,
  canRevisarAtividade,
  papelLabels,
  useGrupo,
  useMembros,
  useSairDoGrupo,
} from "@/features/grupos";
import {
  ComoFuncionaClassificacao,
  ListaClassificacao,
  useResumoGrupo,
  type ClassificacaoParticipante,
} from "@/features/progresso";
import { ApiError, getErrorMessage } from "@/services/api";

function getIniciais(nome: string): string {
  return nome
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((palavra) => palavra.charAt(0).toUpperCase())
    .join("");
}

/** "Semana 3 de 8 · até 29 de dez. de 2026", conforme a situação do desafio. */
function getAndamento(desafio: Desafio, semana: number | undefined): string {
  const total = getTotalSemanas(desafio.dataInicio, desafio.dataFim);
  const semanas = `${total} ${total === 1 ? "semana" : "semanas"}`;
  switch (desafio.situacao) {
    case "Agendado":
      return `Começa em ${formatarData(desafio.dataInicio)} · ${semanas}`;
    case "Ativo":
      return semana
        ? `Semana ${semana} de ${total} · até ${formatarData(desafio.dataFim)}`
        : semanas;
    case "Encerrado":
      return `Terminou em ${formatarData(desafio.dataFim)} · ${semanas}`;
    case "Cancelado":
      return "Cancelado";
  }
}

/**
 * O desafio em destaque, inteiro: andamento, regras, classificação e uma linha sobre a semana.
 * A classificação vem do `resumo`, que já traz todos os participantes na ordem.
 */
function DesafioEmDestaque({
  desafioId,
  grupoId,
  usuarioId,
  classificacao,
}: {
  desafioId: string;
  grupoId: string;
  usuarioId: string | undefined;
  classificacao: ClassificacaoParticipante[];
}) {
  const desafio = useDesafio(desafioId);
  const progresso = useProgresso(desafioId);

  if (desafio.isError) {
    return (
      <p role="alert" className="form-error mt-4">
        {getErrorMessage(desafio.error)}
      </p>
    );
  }
  if (!desafio.data) {
    return (
      <p role="status" className="py-8 text-center text-sm text-muted">
        Carregando o desafio…
      </p>
    );
  }

  const d = desafio.data;
  const total = getTotalSemanas(d.dataInicio, d.dataFim);
  const semanaAtual = progresso.data?.semanaAtual ?? null;
  const semanasPassadas =
    d.situacao === "Encerrado"
      ? total
      : semanaAtual
        ? semanaAtual.numero - 1
        : 0;
  const comecou = d.situacao === "Ativo" || d.situacao === "Encerrado";
  const alguemPontuou = classificacao.some((c) => c.sessoesVerificadas > 0);

  // A semana do grupo: os dias verificados de quem conta nesta semana, até a meta de cada um.
  const meta = progresso.data?.sessoesPorSemana ?? 0;
  const contando =
    progresso.data?.participantes.filter((p) => p.contaNestaSemana) ?? [];
  const feitoNaSemana = contando.reduce(
    (soma, p) => soma + Math.min(p.sessoesVerificadas, meta),
    0,
  );
  const objetivoDaSemana = meta * contando.length;

  return (
    <>
      <section className="card mt-4 p-5">
        <div className="flex items-start justify-between gap-3">
          <p className="text-xs font-semibold tracking-wide text-muted uppercase">
            {d.situacao === "Agendado"
              ? "Próximo desafio"
              : d.situacao === "Encerrado"
                ? "Último desafio"
                : "Desafio atual"}
          </p>
          <span className="chip">{situacaoLabels[d.situacao]}</span>
        </div>
        <h2 className="mt-1 text-2xl break-words uppercase">{d.nome}</h2>
        {d.descricao && (
          <p className="mt-1 text-sm break-words whitespace-pre-line text-muted">
            {d.descricao}
          </p>
        )}
        <p className="mt-3 text-sm font-semibold">
          {getAndamento(d, semanaAtual?.numero)}
        </p>
        {comecou && (
          <div className="mt-2">
            <Segments
              label={`Semanas do desafio: ${semanasPassadas} de ${total} concluídas`}
              valor={semanasPassadas}
              provisorio={semanaAtual ? semanaAtual.numero : semanasPassadas}
              max={total}
            />
          </div>
        )}

        <h3 className="mt-5 text-xs font-semibold tracking-wide text-muted uppercase">
          Regras do desafio
        </h3>
        <RegrasDesafio desafio={d} />

        <Link
          to={`/desafios/${d.id}`}
          className="mt-3 flex items-center gap-1 text-sm font-semibold text-link"
        >
          Detalhes e seu histórico
          <ChevronRight aria-hidden="true" className="size-4" />
        </Link>
      </section>

      <SectionTitle>
        {d.situacao === "Encerrado" ? "Classificação final" : "Classificação"}
      </SectionTitle>
      {comecou && alguemPontuou ? (
        <ListaClassificacao
          participantes={classificacao}
          usuarioId={usuarioId}
          grupoId={grupoId}
        />
      ) : (
        <p className="card p-4 text-sm text-muted">
          {d.situacao === "Agendado"
            ? `A classificação começa em ${formatarData(d.dataInicio)}.`
            : "A classificação aparece com o primeiro dia de treino verificado."}
        </p>
      )}
      <ComoFuncionaClassificacao />

      {d.situacao === "Ativo" && semanaAtual && objetivoDaSemana > 0 && (
        <p className="mt-3 text-sm text-muted">
          <span className="font-semibold text-foreground">Esta semana:</span>{" "}
          <span className="font-score text-base text-foreground">
            {feitoNaSemana}/{objetivoDaSemana}
          </span>{" "}
          dias verificados do grupo · fecha em{" "}
          {formatarTempoAteFim(semanaAtual.fim, d.fusoHorario)}
        </p>
      )}
    </>
  );
}

export function GrupoPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: usuario } = useSessao();
  const grupo = useGrupo(id);
  const resumo = useResumoGrupo(id);
  const membros = useMembros(id);
  const desafios = useDesafios(id);
  const feed = useAtividadesFeed(id, 3);
  const podeRevisar = grupo.data
    ? canRevisarAtividade(grupo.data.papel)
    : false;
  const disputas = useAtividadesFeed(id, 20, "EmRevisao", podeRevisar);
  const sair = useSairDoGrupo(id);
  const [saindo, setSaindo] = useState(false);

  if (grupo.isPending) return <SplashScreen />;

  if (grupo.isError) {
    const notFound =
      grupo.error instanceof ApiError && grupo.error.status === 404;
    return (
      <div className="card">
        <EmptyState
          icon={notFound ? Users : CloudOff}
          title={
            notFound ? "Grupo não encontrado" : "Não foi possível carregar"
          }
          description={
            notFound
              ? "Este grupo não existe ou você não faz parte dele."
              : getErrorMessage(grupo.error)
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

  const { nome, papel, totalMembros, maxMembros, fusoHorario } = grupo.data;
  const desafioAtual = resumo.data?.desafio;
  const atividades = feed.data?.pages[0]?.atividades ?? [];
  const emDisputa = disputas.data?.pages.flatMap((p) => p.atividades) ?? [];

  return (
    <>
      <section className="relative overflow-hidden rounded-2xl border-2 border-outline bg-brand p-5 text-on-brand shadow-sticker">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-4 -right-4 size-36 halftone-ink"
        />
        <div className="relative flex items-center gap-3">
          <div className="grid size-14 shrink-0 rotate-[-4deg] place-items-center rounded-xl border-2 border-outline bg-reward font-display text-xl text-on-reward">
            {getIniciais(nome)}
          </div>
          <div className="min-w-0 flex-1">
            <h1 className="truncate text-3xl uppercase">{nome}</h1>
            <p className="truncate text-sm opacity-90">
              {totalMembros} de {maxMembros} membros
            </p>
          </div>
        </div>
      </section>

      <AvisoAvaliacao grupoId={id} />

      {desafioAtual && resumo.data && (
        <DesafioEmDestaque
          desafioId={desafioAtual.id}
          grupoId={id}
          usuarioId={usuario?.id}
          classificacao={resumo.data.classificacao}
        />
      )}

      {emDisputa.length > 0 && (
        <>
          <SectionTitle>Disputas para decidir</SectionTitle>
          <div className="grid gap-3 lg:grid-cols-2">
            {emDisputa.map((atividade) => (
              <FeedAtividade key={atividade.id} atividade={atividade} />
            ))}
          </div>
        </>
      )}

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        {!desafioAtual && canGerenciarDesafios(papel) && (
          <Link
            to={`/grupos/${id}/desafios/novo`}
            className="card flex items-center gap-3 p-4 tap"
          >
            <Plus aria-hidden="true" className="size-5 text-link" />
            <span className="flex-1 text-sm font-semibold">Criar desafio</span>
            <ChevronRight aria-hidden="true" className="size-4 text-muted" />
          </Link>
        )}
        {canConvidar(papel) && (
          <Link
            to={`/grupos/${id}/convidar`}
            className="card flex items-center gap-3 p-4 tap"
          >
            <UserPlus aria-hidden="true" className="size-5 text-link" />
            <span className="flex-1 text-sm font-semibold">
              Convidar amigos
            </span>
            <ChevronRight aria-hidden="true" className="size-4 text-muted" />
          </Link>
        )}
      </div>

      <div className="grid gap-x-6 lg:grid-cols-2">
        <div>
          <SectionTitle>Membros</SectionTitle>
          {membros.isError && (
            <p role="alert" className="form-error">
              {getErrorMessage(membros.error)}
            </p>
          )}
          {membros.data && (
            <ul className="card divide-y divide-border overflow-hidden">
              {membros.data.map((membro) => (
                <li key={membro.usuarioId}>
                  <Link
                    to={`/grupos/${id}/membros/${membro.usuarioId}`}
                    className="flex items-center gap-3 p-4 transition-colors hover:bg-surface-muted"
                  >
                    <Avatar
                      nome={membro.nomeExibicao}
                      id={membro.usuarioId}
                      size={38}
                    />
                    <p className="min-w-0 flex-1 truncate text-sm font-semibold">
                      {membro.nomeExibicao}
                      {membro.usuarioId === usuario?.id && (
                        <span className="font-normal text-muted"> (você)</span>
                      )}
                    </p>
                    <span className="chip">{papelLabels[membro.papel]}</span>
                    <ChevronRight
                      aria-hidden="true"
                      className="size-4 shrink-0 text-muted"
                    />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div>
          <SectionTitle
            action={
              <Link
                to={`/grupos/${id}/atividades`}
                className="text-sm font-semibold text-link"
              >
                Ver todas
              </Link>
            }
          >
            Atividade recente
          </SectionTitle>
          {feed.isError && (
            <p role="alert" className="form-error">
              {getErrorMessage(feed.error)}
            </p>
          )}
          {feed.data && atividades.length === 0 && (
            <div className="card">
              <EmptyState
                icon={CheckCircle2}
                title="Quadra vazia por enquanto"
                description="Quando alguém do grupo registrar um treino, ele aparece aqui."
              />
            </div>
          )}
          <div className="space-y-3">
            {atividades.map((atividade) => (
              <FeedAtividade key={atividade.id} atividade={atividade} />
            ))}
          </div>
        </div>
      </div>

      <SectionTitle
        action={
          canGerenciarDesafios(papel) && (
            <Link
              to={`/grupos/${id}/desafios/novo`}
              className="text-sm font-semibold text-link"
            >
              Criar desafio
            </Link>
          )
        }
      >
        Todos os desafios
      </SectionTitle>
      {desafios.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(desafios.error)}
        </p>
      )}
      {desafios.data?.length === 0 && (
        <p className="card p-4 text-sm text-muted">
          Este grupo ainda não tem desafios.
        </p>
      )}
      {desafios.data && desafios.data.length > 0 && (
        <ul className="card divide-y divide-border overflow-hidden">
          {desafios.data.map((desafio) => (
            <li key={desafio.id}>
              <Link
                to={`/desafios/${desafio.id}`}
                className="flex items-center gap-3 p-4 transition-colors hover:bg-surface-muted"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold">
                    {desafio.nome}
                  </p>
                  <p className="text-xs text-muted">
                    {desafio.sessoesPorSemana} dias por semana ·{" "}
                    {formatarMeta(desafio)} · {formatarData(desafio.dataInicio)}{" "}
                    a {formatarData(desafio.dataFim)}
                  </p>
                </div>
                <span className="chip">{situacaoLabels[desafio.situacao]}</span>
                <ChevronRight
                  aria-hidden="true"
                  className="size-4 shrink-0 text-muted"
                />
              </Link>
            </li>
          ))}
        </ul>
      )}

      <p className="mt-6 text-center text-xs text-muted">
        Fuso do grupo: {fusoHorario.replaceAll("_", " ")}
      </p>

      <section className="mt-3">
        {papel === "Proprietario" ? (
          <p className="text-center text-xs text-muted">
            Para sair do grupo, transfira a propriedade para outro membro. Se
            você estiver sozinho, ainda não é possível sair nem apagar o grupo.
          </p>
        ) : (
          <button
            type="button"
            className="flex h-12 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold text-danger hover:bg-danger-soft"
            onClick={() => setSaindo(true)}
          >
            <LogOut aria-hidden="true" className="size-4" />
            Sair do grupo
          </button>
        )}
      </section>

      <ConfirmDialog
        open={saindo}
        title="Sair do grupo?"
        message={`Você deixará de ver ${nome}. Para voltar, precisará de um novo convite.`}
        confirmLabel="Sair do grupo"
        pending={sair.isPending}
        error={sair.isError ? getErrorMessage(sair.error) : undefined}
        onConfirm={() =>
          sair.mutate(undefined, {
            onSuccess: () => void navigate("/", { replace: true }),
          })
        }
        onCancel={() => {
          sair.reset();
          setSaindo(false);
        }}
      />
    </>
  );
}
