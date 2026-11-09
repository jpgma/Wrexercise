import { zodResolver } from "@hookform/resolvers/zod";
import {
  CalendarDays,
  Clock,
  CloudOff,
  Repeat,
  Target,
  Users,
} from "lucide-react";
import { useForm, useWatch } from "react-hook-form";
import { useNavigate, useParams } from "react-router";
import { z } from "zod";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import { TextField } from "@/components/TextField";
import {
  canGerenciarDesafios,
  formatarData,
  getDataFim,
  getHojeNoFuso,
  getProximasSegundas,
  useCriarDesafio,
} from "@/features/desafios";
import { useGrupo } from "@/features/grupos";
import { ApiError, getErrorMessage } from "@/services/api";

// Os limites acompanham Features/Desafios/Rules.cs; o backend é quem decide.
const schema = z.object({
  nome: z
    .string()
    .trim()
    .min(1, "Digite o nome do desafio")
    .max(80, "Use no máximo 80 caracteres"),
  descricao: z.string().trim().max(500, "Use no máximo 500 caracteres"),
  dataInicio: z.string().min(1, "Escolha quando o desafio começa"),
  semanas: z
    .number({ error: "Informe a quantidade de semanas" })
    .int("Use um número inteiro")
    .min(1, "Use pelo menos 1 semana")
    .max(52, "Use no máximo 52 semanas"),
  duracaoMinutos: z
    .number({ error: "Informe a duração mínima" })
    .int("Use um número inteiro")
    .min(1, "Use pelo menos 1 minuto")
    .max(1440, "Use no máximo 1440 minutos"),
  sessoesPorSemana: z
    .number({ error: "Informe a quantidade de dias" })
    .int("Use um número inteiro")
    .min(1, "Use pelo menos 1 dia")
    .max(7, "Use no máximo 7 dias"),
});

type FormData = z.infer<typeof schema>;

const totalSegundasOferecidas = 12;

function CriarDesafioForm({
  grupoId,
  fusoHorario,
}: {
  grupoId: string;
  fusoHorario: string;
}) {
  const criar = useCriarDesafio(grupoId);
  const navigate = useNavigate();
  const segundas = getProximasSegundas(
    getHojeNoFuso(fusoHorario),
    totalSegundasOferecidas,
  );
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      nome: "",
      descricao: "",
      dataInicio: segundas[0],
      semanas: 4,
      duracaoMinutos: 60,
      sessoesPorSemana: 4,
    },
  });

  const dataInicio = useWatch({ control, name: "dataInicio" });
  const semanas = useWatch({ control, name: "semanas" });
  const semanasValidas = Number.isInteger(semanas) && semanas >= 1;

  return (
    <form
      noValidate
      onSubmit={(event) =>
        void handleSubmit((data) =>
          criar.mutate(
            {
              nome: data.nome,
              descricao: data.descricao,
              dataInicio: data.dataInicio,
              dataFim: getDataFim(data.dataInicio, data.semanas),
              duracaoMinimaSegundos: data.duracaoMinutos * 60,
              sessoesPorSemana: data.sessoesPorSemana,
            },
            {
              onSuccess: ({ id }) =>
                void navigate(`/desafios/${id}`, { replace: true }),
            },
          ),
        )(event)
      }
      className="space-y-4"
    >
      <TextField
        label="Nome do desafio"
        icon={Target}
        type="text"
        autoComplete="off"
        placeholder="Ex.: Consistência de novembro"
        error={errors.nome?.message}
        {...register("nome")}
      />

      <div>
        <label htmlFor="descricao" className="input-label">
          Descrição (opcional)
        </label>
        <textarea
          id="descricao"
          rows={3}
          className={`input-field h-auto resize-none py-3 ${errors.descricao ? "border-danger" : ""}`}
          aria-invalid={errors.descricao ? true : undefined}
          aria-describedby={errors.descricao ? "descricao-erro" : undefined}
          {...register("descricao")}
        />
        {errors.descricao && (
          <p id="descricao-erro" className="input-error">
            {errors.descricao.message}
          </p>
        )}
      </div>

      <TextField
        label="Dias de treino por semana"
        icon={Repeat}
        type="number"
        inputMode="numeric"
        min={1}
        max={7}
        error={errors.sessoesPorSemana?.message}
        {...register("sessoesPorSemana", { valueAsNumber: true })}
      />

      <TextField
        label="Duração mínima por dia, em minutos"
        icon={Clock}
        type="number"
        inputMode="numeric"
        min={1}
        max={1440}
        error={errors.duracaoMinutos?.message}
        {...register("duracaoMinutos", { valueAsNumber: true })}
      />

      <div>
        <label htmlFor="data-inicio" className="input-label">
          Começa na segunda-feira
        </label>
        <select
          id="data-inicio"
          className="input-field"
          {...register("dataInicio")}
        >
          {segundas.map((segunda) => (
            <option key={segunda} value={segunda}>
              {formatarData(segunda)}
            </option>
          ))}
        </select>
      </div>

      <div>
        <TextField
          label="Duração do desafio, em semanas"
          icon={CalendarDays}
          type="number"
          inputMode="numeric"
          min={1}
          max={52}
          error={errors.semanas?.message}
          {...register("semanas", { valueAsNumber: true })}
        />
        {semanasValidas && !errors.semanas && (
          <p className="mt-1.5 text-sm text-muted">
            Termina no domingo, {formatarData(getDataFim(dataInicio, semanas))}.
          </p>
        )}
      </div>

      <p className="text-sm text-muted">
        A meta e as datas não poderão ser alteradas depois. Para corrigir, será
        preciso cancelar o desafio e criar outro.
      </p>

      {criar.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(criar.error)}
        </p>
      )}

      <button
        type="submit"
        className="btn-primary mt-6 w-full"
        disabled={criar.isPending}
      >
        {criar.isPending ? "Criando…" : "Criar desafio"}
      </button>
    </form>
  );
}

export function CriarDesafioPage() {
  const { id = "" } = useParams();
  const grupo = useGrupo(id);

  if (grupo.isPending) return <SplashScreen />;

  if (grupo.isError || !canGerenciarDesafios(grupo.data.papel)) {
    const notFound =
      grupo.error instanceof ApiError && grupo.error.status === 404;
    return (
      <div className="mx-auto max-w-xl">
        <PageHeader title="Criar desafio" back={`/grupos/${id}`} />
        {grupo.isError && !notFound ? (
          <EmptyState
            icon={CloudOff}
            title="Não foi possível carregar"
            description={getErrorMessage(grupo.error)}
          />
        ) : (
          <EmptyState
            icon={Users}
            title={notFound ? "Grupo não encontrado" : "Sem permissão"}
            description={
              notFound
                ? "Este grupo não existe ou você não faz parte dele."
                : "Só o proprietário e os administradores podem criar desafios."
            }
          />
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-xl">
      <PageHeader title="Criar desafio" back={`/grupos/${id}`} />
      <p className="mb-6 text-sm text-balance text-muted">
        Todos os participantes seguem a mesma meta, em semanas de segunda a
        domingo.
      </p>

      <CriarDesafioForm grupoId={id} fusoHorario={grupo.data.fusoHorario} />
    </div>
  );
}
