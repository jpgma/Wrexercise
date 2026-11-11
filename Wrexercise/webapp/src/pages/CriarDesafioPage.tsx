import { zodResolver } from "@hookform/resolvers/zod";
import {
  CalendarDays,
  Clock,
  CloudOff,
  Plus,
  Repeat,
  Tag,
  Target,
  Trash2,
  Users,
} from "lucide-react";
import {
  Controller,
  useFieldArray,
  useForm,
  useWatch,
  type Control,
  type FieldErrors,
  type UseFormRegister,
} from "react-hook-form";
import { useNavigate, useParams } from "react-router";
import { z } from "zod";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import { TextField } from "@/components/TextField";
import {
  tipoAtividadeIcones,
  tipoAtividadeLabels,
  tiposAtividade,
  type TipoAtividade,
} from "@/features/atividades";
import {
  canGerenciarDesafios,
  descreverMeta,
  formatarData,
  getDataFim,
  getHojeNoFuso,
  getProximasSegundas,
  useCriarDesafio,
} from "@/features/desafios";
import { useGrupo } from "@/features/grupos";
import { ApiError, getErrorMessage } from "@/services/api";

const maxMetasPorTipo = 4;

const metaPorTipoSchema = z.object({
  nome: z.string().trim().max(20, "Use no máximo 20 caracteres"),
  tipos: z
    .array(z.enum(tiposAtividade as [TipoAtividade, ...TipoAtividade[]]))
    .min(1, "Escolha pelo menos um tipo"),
  minutos: z
    .number({ error: "Informe os minutos" })
    .int("Use um número inteiro")
    .min(1, "Use pelo menos 1 minuto")
    .max(1440, "Use no máximo 1440 minutos"),
});

// Os limites acompanham Features/Desafios/ValidacaoDesafio.cs; o backend é quem decide.
const schema = z
  .object({
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
    metasPorTipo: z.array(metaPorTipoSchema).max(maxMetasPorTipo),
  })
  .superRefine((data, ctx) => {
    const usados = new Set<TipoAtividade>();
    data.metasPorTipo.forEach((meta, indice) => {
      if (meta.minutos === data.duracaoMinutos) {
        ctx.addIssue({
          code: "custom",
          path: ["metasPorTipo", indice, "minutos"],
          message: `Use um valor diferente dos ${data.duracaoMinutos} min do dia, senão esta meta não muda nada`,
        });
      }
      const repetido = meta.tipos.find((t) => usados.has(t));
      if (repetido) {
        ctx.addIssue({
          code: "custom",
          path: ["metasPorTipo", indice, "tipos"],
          message: `${tipoAtividadeLabels[repetido]} já está em outra meta`,
        });
      }
      meta.tipos.forEach((t) => usados.add(t));
    });
  });

type FormData = z.infer<typeof schema>;

const totalSegundasOferecidas = 12;

function MetaPorTipoCard({
  indice,
  control,
  register,
  errors,
  onRemover,
}: {
  indice: number;
  control: Control<FormData>;
  register: UseFormRegister<FormData>;
  errors: FieldErrors<FormData>;
  onRemover: () => void;
}) {
  const erros = errors.metasPorTipo?.[indice];
  const tiposErroId = `meta-${indice}-tipos-erro`;
  // Um tipo fica em no máximo uma meta (decisão 0045).
  const todas = useWatch({ control, name: "metasPorTipo" });
  const emOutraMeta = new Set(
    todas.flatMap((m, i) => (i === indice ? [] : m.tipos)),
  );

  return (
    <li className="card space-y-3 p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm font-semibold">Meta por tipo {indice + 1}</p>
        <button
          type="button"
          aria-label={`Remover a meta por tipo ${indice + 1}`}
          className="grid size-11 place-items-center rounded-xl text-muted tap hover:text-danger"
          onClick={onRemover}
        >
          <Trash2 aria-hidden="true" className="size-5" />
        </button>
      </div>

      <Controller
        control={control}
        name={`metasPorTipo.${indice}.tipos`}
        render={({ field }) => (
          <fieldset aria-describedby={erros?.tipos ? tiposErroId : undefined}>
            <legend className="input-label">Tipos que somam</legend>
            <div className="flex flex-wrap gap-2">
              {tiposAtividade.map((tipo) => {
                const Icone = tipoAtividadeIcones[tipo];
                const marcado = field.value.includes(tipo);
                const bloqueado = !marcado && emOutraMeta.has(tipo);
                return (
                  <button
                    key={tipo}
                    type="button"
                    aria-pressed={marcado}
                    disabled={bloqueado}
                    title={bloqueado ? "Já está em outra meta" : undefined}
                    onClick={() =>
                      field.onChange(
                        marcado
                          ? field.value.filter((t) => t !== tipo)
                          : [...field.value, tipo],
                      )
                    }
                    className={`inline-flex h-11 items-center gap-1.5 rounded-full border px-3.5 text-sm font-medium transition-colors ${
                      marcado
                        ? "border-primary bg-primary text-on-primary"
                        : "border-border bg-surface text-muted disabled:opacity-40"
                    }`}
                  >
                    <Icone aria-hidden="true" className="size-4" />
                    {tipoAtividadeLabels[tipo]}
                  </button>
                );
              })}
            </div>
            {erros?.tipos && (
              <p id={tiposErroId} className="input-error">
                {erros.tipos.message}
              </p>
            )}
          </fieldset>
        )}
      />

      <TextField
        label="Mínimo por dia desses tipos, em minutos"
        icon={Clock}
        type="number"
        inputMode="numeric"
        min={1}
        max={1440}
        error={erros?.minutos?.message}
        {...register(`metasPorTipo.${indice}.minutos`, { valueAsNumber: true })}
      />

      <TextField
        label="Nome (opcional)"
        icon={Tag}
        type="text"
        autoComplete="off"
        placeholder="Ex.: Cardio"
        maxLength={20}
        error={erros?.nome?.message}
        {...register(`metasPorTipo.${indice}.nome`)}
      />
    </li>
  );
}

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
      metasPorTipo: [],
    },
  });
  const metas = useFieldArray({ control, name: "metasPorTipo" });

  const dataInicio = useWatch({ control, name: "dataInicio" });
  const semanas = useWatch({ control, name: "semanas" });
  const semanasValidas = Number.isInteger(semanas) && semanas >= 1;
  const duracaoMinutos = useWatch({ control, name: "duracaoMinutos" });
  const metasAtuais = useWatch({ control, name: "metasPorTipo" });
  const resumoValido =
    Number.isInteger(duracaoMinutos) &&
    duracaoMinutos > 0 &&
    metasAtuais.every(
      (m) => m.tipos.length > 0 && Number.isInteger(m.minutos) && m.minutos > 0,
    );

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
              metasPorTipo: data.metasPorTipo.map((m) => ({
                nome: m.nome || null,
                tipos: m.tipos,
                duracaoMinimaSegundos: m.minutos * 60,
              })),
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

      <section aria-labelledby="metas-por-tipo-titulo" className="space-y-3">
        <div>
          <p id="metas-por-tipo-titulo" className="input-label mb-0">
            Mínimo por tipo (opcional)
          </p>
          <p className="mt-1 text-sm text-muted">
            Dê a alguns tipos um mínimo próprio: menos minutos para um treino
            puxado, como 20 min de corrida, ou mais para um que não deve bastar
            sozinho, como 90 min de pedal para quem vai de bicicleta ao
            trabalho. Tipos diferentes somam em proporção: metade de um mínimo e
            metade de outro fecham o dia.
          </p>
        </div>

        {metas.fields.length > 0 && (
          <ul className="space-y-3">
            {metas.fields.map((field, indice) => (
              <MetaPorTipoCard
                key={field.id}
                indice={indice}
                control={control}
                register={register}
                errors={errors}
                onRemover={() => metas.remove(indice)}
              />
            ))}
          </ul>
        )}

        {metas.fields.length < maxMetasPorTipo && (
          <button
            type="button"
            className="btn-secondary w-full"
            onClick={() =>
              metas.append({
                nome: "",
                tipos: [],
                minutos: Math.max(1, Math.floor((duracaoMinutos || 2) / 2)),
              })
            }
          >
            <Plus aria-hidden="true" className="size-5" />
            Adicionar meta por tipo
          </button>
        )}

        {resumoValido && metasAtuais.length > 0 && (
          <p
            role="status"
            className="rounded-xl bg-surface-muted p-3 text-sm text-balance"
          >
            O dia conta com{" "}
            {descreverMeta({
              duracaoMinimaSegundos: duracaoMinutos * 60,
              metasPorTipo: metasAtuais.map((m, i) => ({
                id: String(i),
                nome: m.nome.trim() || null,
                tipos: m.tipos,
                duracaoMinimaSegundos: m.minutos * 60,
              })),
            })}
            .
          </p>
        )}
      </section>

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
        domingo. Um dia conta quando os treinos dele alcançam o mínimo do dia,
        com cada tipo valendo em proporção ao mínimo dele.
      </p>

      <CriarDesafioForm grupoId={id} fusoHorario={grupo.data.fusoHorario} />
    </div>
  );
}
