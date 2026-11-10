import {
  ChevronRight,
  Flame,
  Plus,
  Settings,
  Timer,
  Trophy,
  type LucideIcon,
} from "lucide-react";
import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import { HistoryChart } from "@/components/HistoryChart";
import { ProgressBar } from "@/components/ProgressBar";
import { PageHeader, SectionTitle } from "@/components/PageHeader";
import { useSessao } from "@/features/autenticacao";
import { ConviteForm } from "@/features/convites";
import { formatarSemanaCurta } from "@/features/desafios";
import { papelLabels, useGrupos } from "@/features/grupos";
import {
  EnergiaPerfil,
  EnfeiteBloqueadoIcone,
  EnfeiteIcone,
} from "@/features/jogo";
import { SinoNotificacoes } from "@/features/notificacoes";
import { useMeuPerfil, type Conquista } from "@/features/usuarios";
import { getErrorMessage } from "@/services/api";

function Numero({
  icon: Icon,
  valor,
  rotulo,
}: {
  icon: LucideIcon;
  valor: string;
  rotulo: string;
}) {
  return (
    <div className="card p-3">
      <Icon aria-hidden="true" className="size-4 text-link" />
      <p className="mt-2 font-display text-xl">{valor}</p>
      <p className="text-2xs leading-tight text-muted">{rotulo}</p>
    </div>
  );
}

/** Uma conquista ainda bloqueada: só o desenho fica apagado, o texto continua legível. */
function ConquistaBloqueada({ conquista }: { conquista: Conquista }) {
  return (
    <li className="card flex items-center gap-3 p-4">
      <div className="grid size-12 shrink-0 place-items-center rounded-lg bg-surface-muted">
        <EnfeiteBloqueadoIcone conquista={conquista.codigo} tamanho={32} />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold">
          {conquista.titulo}
          <span className="sr-only"> (bloqueada)</span>
        </p>
        <p className="text-xs text-muted">{conquista.descricao}</p>
        <div className="mt-2 flex items-center gap-2">
          <ProgressBar
            label={`Progresso em ${conquista.titulo}`}
            valor={conquista.atual}
            max={conquista.alvo}
            className="flex-1"
          />
          <span aria-hidden="true" className="text-xs text-muted tabular-nums">
            {conquista.atual}/{conquista.alvo}
          </span>
        </div>
      </div>
    </li>
  );
}

export function PerfilPage() {
  const { data: usuario } = useSessao();
  const perfil = useMeuPerfil();
  const grupos = useGrupos();

  if (!usuario) return null;

  const conquistas = perfil.data?.conquistas ?? [];
  const liberadas = conquistas.filter((c) => c.liberada);
  const proximas = conquistas.filter((c) => !c.liberada);

  return (
    <div className="mx-auto max-w-4xl">
      <PageHeader
        title="Perfil"
        action={
          <div className="flex gap-2">
            <SinoNotificacoes />
            <Link
              to="/perfil/configuracoes"
              aria-label="Configurações"
              className="grid size-10 place-items-center rounded-full bg-surface shadow-card tap"
            >
              <Settings aria-hidden="true" className="size-5" />
            </Link>
          </div>
        }
      />

      <div className="grid gap-x-6 lg:grid-cols-2">
        <div>
          <div className="card flex items-center gap-4 p-5">
            <Avatar nome={usuario.nomeExibicao} id={usuario.id} size={72} />
            <div className="min-w-0">
              <p className="truncate font-display text-xl">
                {usuario.nomeExibicao}
              </p>
              <p className="truncate text-sm text-muted">{usuario.email}</p>
              {perfil.data && (
                <p className="mt-1 text-xs text-muted">
                  No Wrexercise desde{" "}
                  {new Date(perfil.data.membroDesde).toLocaleDateString(
                    "pt-BR",
                    {
                      month: "short",
                      year: "numeric",
                    },
                  )}
                </p>
              )}
            </div>
          </div>

          <Link
            to="/perfil/avatar"
            className="btn-secondary mt-3 w-full justify-center"
          >
            Trocar avatar
          </Link>

          {perfil.isError && (
            <p role="alert" className="form-error mt-4">
              {getErrorMessage(perfil.error)}
            </p>
          )}
          {perfil.data && (
            <>
              <div className="mt-4 grid grid-cols-3 gap-3">
                <Numero
                  icon={Trophy}
                  valor={String(perfil.data.totalTreinos)}
                  rotulo="treinos verificados"
                />
                <Numero
                  icon={Timer}
                  valor={`${Math.round(perfil.data.totalSegundos / 3600)}h`}
                  rotulo="em movimento"
                />
                <Numero
                  icon={Flame}
                  valor={String(perfil.data.melhorSequencia)}
                  rotulo="melhor sequência de semanas"
                />
              </div>
              <div className="mt-4">
                <HistoryChart
                  titulo="Consistência semanal"
                  colunas={perfil.data.semanas.map((semana) => ({
                    chave: semana.inicio,
                    rotulo: formatarSemanaCurta(semana.inicio),
                    valor: semana.sessoesVerificadas,
                    provisorio: semana.sessoesProvisorias,
                  }))}
                />
              </div>
            </>
          )}
        </div>

        <div>
          {/* Quem está no modo App não vê a Energia, embora a acumule. */}
          {usuario.modoInicio === "Jogo" && <EnergiaPerfil />}

          {perfil.data && (
            <>
              <SectionTitle>Conquistas</SectionTitle>
              {liberadas.length === 0 ? (
                <p className="card p-4 text-sm text-muted">
                  Nenhuma conquista ainda. A primeira pode vir já no próximo
                  treino.
                </p>
              ) : (
                <ul className="grid gap-3 sm:grid-cols-2">
                  {liberadas.map((conquista, indice) => (
                    <li
                      key={conquista.codigo}
                      className="card-sticker flex animate-pop items-center gap-3 p-4"
                      style={{ animationDelay: `${indice * 60}ms` }}
                    >
                      <div
                        className={`relative grid size-12 shrink-0 place-items-center rounded-lg border-2 border-outline bg-reward text-on-reward ${indice % 2 ? "rotate-3" : "-rotate-3"}`}
                      >
                        <EnfeiteIcone
                          conquista={conquista.codigo}
                          tamanho={32}
                        />
                      </div>
                      <div className="min-w-0">
                        <p className="font-display text-lg leading-tight uppercase">
                          {conquista.titulo}
                        </p>
                        <p className="text-xs text-muted">
                          {conquista.descricao}
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              {proximas.length > 0 && (
                <>
                  <h3 className="mt-5 mb-2 text-sm font-semibold text-muted">
                    Próximas
                  </h3>
                  <ul className="grid gap-3 sm:grid-cols-2">
                    {proximas.map((conquista) => (
                      <ConquistaBloqueada
                        key={conquista.codigo}
                        conquista={conquista}
                      />
                    ))}
                  </ul>
                </>
              )}
            </>
          )}

          <SectionTitle>Seus grupos</SectionTitle>
          {grupos.isError && (
            <p role="alert" className="form-error">
              {getErrorMessage(grupos.error)}
            </p>
          )}
          {grupos.data && grupos.data.length > 0 && (
            <ul className="card divide-y divide-border overflow-hidden">
              {grupos.data.map((grupo) => (
                <li key={grupo.id}>
                  <Link
                    to={`/grupos/${grupo.id}`}
                    className="flex items-center gap-3 p-4 transition-colors hover:bg-surface-muted"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold">
                        {grupo.nome}
                      </p>
                      <p className="text-xs text-muted">
                        {grupo.totalMembros}{" "}
                        {grupo.totalMembros === 1 ? "membro" : "membros"} ·{" "}
                        {papelLabels[grupo.papel]}
                      </p>
                    </div>
                    <ChevronRight
                      aria-hidden="true"
                      className="size-4 shrink-0 text-muted"
                    />
                  </Link>
                </li>
              ))}
            </ul>
          )}
          <Link to="/grupos/novo" className="btn-secondary mt-3 w-full">
            <Plus aria-hidden="true" className="size-5" />
            Criar grupo
          </Link>

          <div className="mt-4">
            <ConviteForm />
          </div>
        </div>
      </div>
    </div>
  );
}
