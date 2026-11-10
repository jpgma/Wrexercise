import {
  CheckCircle2,
  ChevronRight,
  CloudOff,
  LogOut,
  Plus,
  Target,
  UserPlus,
  Users,
} from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { SectionTitle } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import { useAtividadesFeed } from "@/features/atividades";
import { useSessao } from "@/features/autenticacao";
import {
  canGerenciarDesafios,
  formatarData,
  formatarMeta,
  formatarTempoAteFim,
  situacaoLabels,
  useDesafio,
  useDesafios,
  useProgresso,
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
import { LinhaParticipante, useResumoGrupo } from "@/features/progresso";
import { ApiError, getErrorMessage } from "@/services/api";

function getIniciais(nome: string): string {
  return nome
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((palavra) => palavra.charAt(0).toUpperCase())
    .join("");
}

/** O cartão do grupo e a turma mostram o desafio atual; sem ele, sobra só o nome do grupo. */
function CabecalhoComDesafio({
  desafioId,
  nome,
  totalMembros,
  usuarioId,
  grupoId,
}: {
  desafioId: string;
  nome: string;
  totalMembros: number;
  usuarioId: string | undefined;
  grupoId: string;
}) {
  const desafio = useDesafio(desafioId);
  const progresso = useProgresso(desafioId);

  const meta = progresso.data?.sessoesPorSemana ?? 0;
  const participantes = progresso.data?.participantes ?? [];
  const contando = participantes.filter((p) => p.contaNestaSemana);
  const totalFeito = contando.reduce(
    (soma, p) => soma + Math.min(p.sessoesVerificadas, meta),
    0,
  );
  const objetivo = meta * contando.length;
  const faltam = objetivo - totalFeito;
  const fim = progresso.data?.semanaAtual?.fim;
  const ordenados = [...participantes].sort(
    (a, b) =>
      b.sessoesVerificadas - a.sessoesVerificadas ||
      b.sessoesProvisorias - a.sessoesProvisorias,
  );

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
              {totalMembros} {totalMembros === 1 ? "membro" : "membros"}
              {desafio.data && ` · ${desafio.data.nome}`}
            </p>
          </div>
        </div>
        {objetivo > 0 && (
          <div className="relative mt-5">
            <div className="flex items-end justify-between">
              <p className="text-sm opacity-90">Treinos do grupo na semana</p>
              <p className="font-display text-2xl">
                {totalFeito}
                <span className="text-base opacity-80">/{objetivo}</span>
              </p>
            </div>
            <div
              aria-hidden="true"
              className="mt-2 h-3 overflow-hidden rounded-sm border-2 border-outline bg-surface"
            >
              <div
                className="h-full bg-progress"
                style={{ width: `${(totalFeito / objetivo) * 100}%` }}
              />
            </div>
            <p className="mt-2 text-xs opacity-90">
              {faltam > 0
                ? `Faltam ${faltam} treinos verificados para todo mundo bater a meta`
                : "Todo mundo bateu a meta!"}
              {fim &&
                desafio.data &&
                ` · ${formatarTempoAteFim(fim, desafio.data.fusoHorario)}`}
            </p>
          </div>
        )}
      </section>

      {ordenados.length > 0 && (
        <>
          <SectionTitle>Como está a turma</SectionTitle>
          <div className="card p-3">
            {ordenados.map((p) => (
              <LinhaParticipante
                key={p.usuarioId}
                usuarioId={p.usuarioId}
                nomeExibicao={p.nomeExibicao}
                verificadas={p.sessoesVerificadas}
                provisorias={p.sessoesProvisorias}
                meta={meta}
                voce={p.usuarioId === usuarioId}
                contaNestaSemana={p.contaNestaSemana}
                grupoId={grupoId}
              />
            ))}
          </div>
        </>
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
      {desafioAtual ? (
        <CabecalhoComDesafio
          desafioId={desafioAtual.id}
          nome={nome}
          totalMembros={totalMembros}
          usuarioId={usuario?.id}
          grupoId={id}
        />
      ) : (
        <section className="rounded-2xl border-2 border-outline bg-brand p-5 text-on-brand shadow-sticker">
          <div className="flex items-center gap-3">
            <div className="grid size-14 shrink-0 place-items-center rounded-2xl bg-reward font-display text-xl text-on-reward">
              {getIniciais(nome)}
            </div>
            <div className="min-w-0 flex-1">
              <h1 className="truncate text-2xl uppercase">{nome}</h1>
              <p className="text-sm opacity-90">
                {totalMembros} de {maxMembros} membros
              </p>
            </div>
          </div>
        </section>
      )}

      <AvisoAvaliacao grupoId={id} />

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
        {desafioAtual ? (
          <Link
            to={`/desafios/${desafioAtual.id}`}
            className="card flex items-center gap-3 p-4 tap"
          >
            <Target aria-hidden="true" className="size-5 text-link" />
            <span className="flex-1 text-sm font-semibold">Meta do grupo</span>
            <ChevronRight aria-hidden="true" className="size-4 text-muted" />
          </Link>
        ) : (
          canGerenciarDesafios(papel) && (
            <Link
              to={`/grupos/${id}/desafios/novo`}
              className="card flex items-center gap-3 p-4 tap"
            >
              <Plus aria-hidden="true" className="size-5 text-link" />
              <span className="flex-1 text-sm font-semibold">
                Criar desafio
              </span>
              <ChevronRight aria-hidden="true" className="size-4 text-muted" />
            </Link>
          )
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
        Desafios
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
