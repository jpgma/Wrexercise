import { ChevronRight, Clock, CloudOff, Plus, Target } from "lucide-react";
import { Link, useParams } from "react-router";
import { Avatar } from "@/components/Avatar";
import { EmptyState } from "@/components/EmptyState";
import { SectionTitle } from "@/components/PageHeader";
import { ProgressRing } from "@/components/ProgressRing";
import { SplashScreen } from "@/components/SplashScreen";
import { useAtividadesFeed } from "@/features/atividades";
import { useSessao } from "@/features/autenticacao";
import {
  canGerenciarDesafios,
  FaixaSemana,
  formatarDuracao,
  formatarTempoAteFim,
  useDesafio,
  useProgresso,
} from "@/features/desafios";
import { AvisoAvaliacao, FeedAtividade } from "@/features/feed";
import { useGrupo } from "@/features/grupos";
import { CartasDoGrupo, CenaGrupo, ConviteJogo } from "@/features/jogo";
import { SinoNotificacoes } from "@/features/notificacoes";
import {
  LinhaParticipante,
  useResumoGrupo,
  type ResumoGrupo,
} from "@/features/progresso";
import { getErrorMessage } from "@/services/api";

function getSaudacao(agora: Date): string {
  const hora = agora.getHours();
  if (hora < 12) return "Bom dia";
  return hora < 18 ? "Boa tarde" : "Boa noite";
}

function getMotivacao(verificadas: number, provisorias: number, meta: number) {
  if (verificadas >= meta) {
    return "Mandou bem! Meta da semana batida.";
  }
  if (provisorias >= meta) return "Meta no papo. Só falta a galera conferir.";
  const faltam = meta - provisorias;
  return faltam === 1
    ? "Falta só mais um treino!"
    : `Faltam ${faltam} treinos. Ainda dá tempo!`;
}

function SuaSemana({
  grupoId,
  desafioId,
  usuarioId,
}: {
  grupoId: string;
  desafioId: string;
  usuarioId: string;
}) {
  const desafio = useDesafio(desafioId);
  const progresso = useProgresso(desafioId);

  if (progresso.isError || desafio.isError) {
    return (
      <p role="alert" className="form-error">
        {getErrorMessage(progresso.error ?? desafio.error)}
      </p>
    );
  }
  if (!progresso.data || !desafio.data) {
    return (
      <p role="status" className="card p-5 text-sm text-muted">
        Carregando sua semana…
      </p>
    );
  }

  const {
    sessoesPorSemana: meta,
    semanaAtual,
    participantes,
    seusDias,
  } = progresso.data;
  const eu = participantes.find((p) => p.usuarioId === usuarioId);
  const verificadas = eu?.sessoesVerificadas ?? 0;
  const provisorias = eu?.sessoesProvisorias ?? 0;
  const ordenados = [...participantes].sort(
    (a, b) =>
      b.sessoesVerificadas - a.sessoesVerificadas ||
      b.sessoesProvisorias - a.sessoesProvisorias,
  );
  const contando = participantes.filter((p) => p.contaNestaSemana);
  const bateram = contando.filter((p) => p.metaCumprida).length;

  return (
    <div className="grid gap-5 lg:grid-cols-[1.1fr_1fr]">
      <section className="card-sticker relative overflow-hidden p-5">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-6 -right-6 size-32 halftone"
        />
        <div className="relative flex items-center justify-between gap-2">
          <p className="eyebrow">Sua semana</p>
          <span className="chip chip-soft">{meta} treinos por semana</span>
        </div>
        <div className="relative mt-4 flex items-center gap-5">
          <ProgressRing
            label="Seu progresso nesta semana"
            valor={verificadas}
            provisorio={provisorias}
            max={meta}
          >
            <div>
              <p className="font-display text-4xl leading-none">
                {Math.min(verificadas, meta)}
                <span className="text-xl text-muted">/{meta}</span>
              </p>
              <p className="mt-1 text-2xs text-muted">dias</p>
            </div>
          </ProgressRing>
          <div className="min-w-0 flex-1">
            <p className="font-display text-2xl leading-tight uppercase">
              {getMotivacao(verificadas, provisorias, meta)}
            </p>
            {semanaAtual && (
              <p className="mt-2 flex items-center gap-1.5 text-sm text-muted">
                <Clock aria-hidden="true" className="size-4 shrink-0" />
                Semana fecha em{" "}
                {formatarTempoAteFim(semanaAtual.fim, desafio.data.fusoHorario)}
              </p>
            )}
            <p className="mt-1 text-sm text-muted">
              Mínimo de {formatarDuracao(desafio.data.duracaoMinimaSegundos)}{" "}
              por dia
            </p>
          </div>
        </div>
        {semanaAtual && (
          <FaixaSemana
            inicio={semanaAtual.inicio}
            diasComTreino={seusDias}
            fusoHorario={desafio.data.fusoHorario}
          />
        )}
        {eu && !eu.contaNestaSemana && (
          <p className="mt-3 rounded-lg bg-surface-muted p-3 text-sm text-muted">
            Você entrou no meio desta semana. Seus treinos contam para a meta a
            partir da próxima segunda.
          </p>
        )}
        {provisorias > verificadas && (
          <p className="mt-3 text-xs text-muted">
            {provisorias - verificadas} aguardando verificação
          </p>
        )}
        <Link
          to={`/grupos/${grupoId}/atividades/nova`}
          className="btn-primary mt-5 w-full"
        >
          <Plus aria-hidden="true" className="size-5" />
          Registrar atividade
        </Link>
      </section>

      <section className="card p-4">
        <div className="flex items-center justify-between gap-2 px-2 pb-2">
          <div className="min-w-0">
            <p className="eyebrow">Desafio</p>
            <p className="truncate font-display text-xl uppercase">
              {desafio.data.nome}
            </p>
            <p className="text-xs text-muted">
              {bateram} de {contando.length} já bateram a meta
            </p>
          </div>
          <Link
            to={`/grupos/${grupoId}`}
            className="flex shrink-0 items-center text-sm font-semibold text-link"
          >
            Ver grupo
            <ChevronRight aria-hidden="true" className="size-4" />
          </Link>
        </div>
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
      </section>
    </div>
  );
}

function InicioJogo({
  grupoId,
  usuarioId,
  resumo,
}: {
  grupoId: string;
  usuarioId: string;
  resumo: ResumoGrupo | undefined;
}) {
  const progresso = resumo?.meuProgresso;
  const meta = resumo?.sessoesPorSemana;

  return (
    <div className="mt-6 space-y-4">
      <CenaGrupo grupoId={grupoId} usuarioId={usuarioId} />
      {progresso && meta ? (
        <div className="card flex items-center gap-4 p-4">
          <ProgressRing
            label="Seu progresso nesta semana"
            valor={progresso.sessoesVerificadas}
            provisorio={progresso.sessoesProvisorias}
            max={meta}
            size={72}
          >
            <p className="font-display text-xl leading-none">
              {Math.min(progresso.sessoesVerificadas, meta)}
              <span className="text-sm text-muted">/{meta}</span>
            </p>
          </ProgressRing>
          <p className="min-w-0 flex-1 font-display text-lg leading-tight uppercase">
            {getMotivacao(
              progresso.sessoesVerificadas,
              progresso.sessoesProvisorias,
              meta,
            )}
          </p>
        </div>
      ) : (
        resumo && (
          <p className="card p-4 text-sm text-muted">
            {!resumo.desafio
              ? "O grupo está sem desafio no momento."
              : resumo.desafio.situacao === "Agendado"
                ? `O desafio ${resumo.desafio.nome} ainda não começou.`
                : "Sem meta para você nesta semana."}
          </p>
        )
      )}
      <CartasDoGrupo grupoId={grupoId} />
    </div>
  );
}

export function InicioPage() {
  const { id = "" } = useParams();
  const { data: usuario } = useSessao();
  const grupo = useGrupo(id);
  const resumo = useResumoGrupo(id);

  if (grupo.isPending || !usuario) return <SplashScreen />;

  if (grupo.isError) {
    return (
      <div className="card">
        <EmptyState
          icon={CloudOff}
          title="Não foi possível carregar"
          description={getErrorMessage(grupo.error)}
          action={
            <Link to="/" className="btn-secondary">
              Voltar
            </Link>
          }
        />
      </div>
    );
  }

  const agora = new Date();
  const hoje = new Intl.DateTimeFormat("pt-BR", {
    weekday: "short",
    day: "numeric",
    month: "long",
  })
    .format(agora)
    .replace(".", "");
  const primeiroNome = usuario.nomeExibicao.trim().split(/\s+/)[0];

  return (
    <>
      <header className="flex items-center gap-3">
        <Avatar nome={usuario.nomeExibicao} id={usuario.id} size={44} />
        <div className="min-w-0 flex-1">
          <p className="text-sm text-muted first-letter:uppercase">{hoje}</p>
          <h1 className="truncate text-3xl uppercase">
            {getSaudacao(agora)}, {primeiroNome}!
          </h1>
        </div>
        <SinoNotificacoes />
      </header>

      {usuario.modoInicio === "Jogo" ? (
        <>
          <InicioJogo
            grupoId={id}
            usuarioId={usuario.id}
            resumo={resumo.data}
          />
          <AvisoAvaliacao grupoId={id} />
        </>
      ) : (
        <InicioApp
          grupoId={id}
          papel={grupo.data.papel}
          usuarioId={usuario.id}
          resumo={resumo}
        />
      )}
    </>
  );
}

function InicioApp({
  grupoId: id,
  papel,
  usuarioId,
  resumo,
}: {
  grupoId: string;
  papel: Parameters<typeof canGerenciarDesafios>[0];
  usuarioId: string;
  resumo: ReturnType<typeof useResumoGrupo>;
}) {
  const feed = useAtividadesFeed(id, 4);
  const desafio = resumo.data?.desafio;
  const atividades = feed.data?.pages[0]?.atividades ?? [];

  return (
    <>
      <ConviteJogo />

      <div className="mt-6">
        {resumo.isError && (
          <p role="alert" className="form-error">
            {getErrorMessage(resumo.error)}
          </p>
        )}
        {resumo.data && desafio && (
          <SuaSemana
            grupoId={id}
            desafioId={desafio.id}
            usuarioId={usuarioId}
          />
        )}
        {resumo.data && !desafio && (
          <div className="card">
            <EmptyState
              icon={Target}
              title="Ninguém lançou um desafio ainda"
              description={
                canGerenciarDesafios(papel)
                  ? "O grupo está sem meta no momento. Crie um desafio para começar."
                  : "O grupo está sem meta no momento. Peça a um administrador para criar um desafio."
              }
              action={
                canGerenciarDesafios(papel) ? (
                  <Link
                    to={`/grupos/${id}/desafios/novo`}
                    className="btn-primary"
                  >
                    Criar desafio
                  </Link>
                ) : undefined
              }
            />
          </div>
        )}
      </div>

      <AvisoAvaliacao grupoId={id} />

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
        Atividades recentes
      </SectionTitle>
      {feed.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(feed.error)}
        </p>
      )}
      {feed.data && atividades.length === 0 && (
        <p className="card p-4 text-sm text-muted">
          Quando alguém do grupo registrar um treino, ele aparece aqui.
        </p>
      )}
      <div className="grid gap-3 lg:grid-cols-2">
        {atividades.map((atividade) => (
          <FeedAtividade key={atividade.id} atividade={atividade} />
        ))}
      </div>
    </>
  );
}
