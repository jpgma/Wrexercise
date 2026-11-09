import { zodResolver } from "@hookform/resolvers/zod";
import { Camera, CheckCircle2, Info, Minus, Plus, Trash2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useParams } from "react-router";
import { z } from "zod";
import { PageHeader } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import {
  anexarEvidencia,
  prepararEvidencia,
  tipoAtividadeIcones,
  tipoAtividadeLabels,
  tiposAtividade,
  useCriarAtividade,
  type TipoAtividade,
} from "@/features/atividades";
import { useDesafio, useProgresso } from "@/features/desafios";
import { useGrupo } from "@/features/grupos";
import { useResumoGrupo } from "@/features/progresso";
import { getErrorMessage } from "@/services/api";

const schema = z.object({
  tipo: z.enum(tiposAtividade as [TipoAtividade, ...TipoAtividade[]], {
    error: "Escolha o tipo de atividade.",
  }),
  duracaoMinutos: z
    .number({ error: "Informe a duração" })
    .int("Use um número inteiro")
    .min(1, "Use pelo menos 1 minuto")
    .max(1440, "Use no máximo 1440 minutos"),
  startedAt: z.string().min(1, "Informe quando você treinou"),
  descricao: z.string().trim().max(500, "Use no máximo 500 caracteres"),
});

type FormData = z.infer<typeof schema>;

type Anexo = { arquivo: File; url: string };

const maximoDeEvidencias = 3;
const passoEmMinutos = 5;

function toLocalInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

type Registrada = {
  id: string;
  /** Os minutos que ainda faltavam no dia para contar na meta; 0 se já conta. */
  faltamNoDia: number;
  enviadas: number;
  anexadas: number;
};

function TreinoRegistrado({
  registrada,
  grupoId,
}: {
  registrada: Registrada;
  grupoId: string;
}) {
  const faltaram = registrada.anexadas - registrada.enviadas;

  return (
    <div className="mx-auto flex max-w-md animate-fade-in flex-col items-center pt-16 text-center">
      <div className="relative grid size-24 place-items-center">
        <div
          aria-hidden="true"
          className="absolute inset-0 animate-burst bg-reward burst"
        />
        <div className="relative grid size-20 animate-pop place-items-center rounded-xl border-2 border-outline bg-progress text-on-progress shadow-sticker">
          <CheckCircle2 aria-hidden="true" className="size-10" />
        </div>
      </div>
      <h1 className="mt-6 text-4xl uppercase">Boa! Tá registrado.</h1>
      <p className="mt-2 text-muted">
        {registrada.faltamNoDia > 0
          ? `Registramos. Faltam ${registrada.faltamNoDia} min nesse dia para contar na meta. `
          : ""}
        Seus amigos já podem dar moral.
      </p>
      {faltaram > 0 && (
        <p role="alert" className="form-error mt-4 w-full">
          {faltaram === 1
            ? "Uma imagem não pôde ser enviada."
            : `${faltaram} imagens não puderam ser enviadas.`}
        </p>
      )}
      <div className="mt-8 w-full space-y-2">
        <Link
          to={`/atividades/${registrada.id}`}
          replace
          className="btn-primary w-full"
        >
          Ver atividade
        </Link>
        <Link to={`/grupos/${grupoId}/inicio`} className="btn-ghost w-full">
          Voltar ao início
        </Link>
      </div>
    </div>
  );
}

export function RegistrarAtividadePage() {
  const { id: grupoId = "" } = useParams();
  const grupo = useGrupo(grupoId);
  const resumo = useResumoGrupo(grupoId);
  const desafioId = resumo.data?.desafio?.id;
  const desafio = useDesafio(desafioId ?? "", desafioId !== undefined);
  const progresso = useProgresso(desafioId ?? "", desafioId !== undefined);
  const criar = useCriarAtividade(grupoId);
  const seletor = useRef<HTMLInputElement>(null);
  const [anexos, setAnexos] = useState<Anexo[]>([]);
  const [enviando, setEnviando] = useState<string | null>(null);
  const [registrada, setRegistrada] = useState<Registrada | null>(null);

  const {
    register,
    handleSubmit,
    control,
    setValue,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      duracaoMinutos: 60,
      startedAt: toLocalInputValue(new Date()),
      descricao: "",
    },
  });

  const duracaoMinutos = useWatch({ control, name: "duracaoMinutos" });
  const tipoAtual = useWatch({ control, name: "tipo" });
  const startedAt = useWatch({ control, name: "startedAt" });

  // As prévias ocupam memória até serem liberadas.
  const urls = useRef<string[]>([]);
  useEffect(() => {
    urls.current = anexos.map(({ url }) => url);
  }, [anexos]);
  useEffect(
    () => () => urls.current.forEach((url) => URL.revokeObjectURL(url)),
    [],
  );

  const minimoMinutos = desafio.data
    ? Math.ceil(desafio.data.duracaoMinimaSegundos / 60)
    : null;

  // As atividades do mesmo dia se somam: a meta é de minutos por dia. O dia do campo é o do
  // aparelho, que costuma ser o fuso do grupo.
  const diaEscolhido = startedAt?.slice(0, 10);
  const minutosJaNoDia = Math.floor(
    (progresso.data?.seusSegundosPorDia.find((d) => d.data === diaEscolhido)
      ?.segundos ?? 0) / 60,
  );
  const faltamNoDia =
    minimoMinutos === null
      ? 0
      : Math.max(0, minimoMinutos - minutosJaNoDia - (duracaoMinutos || 0));

  if (grupo.isPending) return <SplashScreen />;

  if (grupo.isError) {
    return (
      <div className="mx-auto max-w-xl">
        <p role="alert" className="form-error">
          {getErrorMessage(grupo.error)}
        </p>
        <Link to="/" className="btn-secondary mt-4">
          Voltar
        </Link>
      </div>
    );
  }

  if (registrada) {
    return <TreinoRegistrado registrada={registrada} grupoId={grupoId} />;
  }

  const adicionarAnexos = (lista: FileList | null) => {
    if (!lista) return;
    const novos = Array.from(lista)
      .slice(0, maximoDeEvidencias - anexos.length)
      .map((arquivo) => ({ arquivo, url: URL.createObjectURL(arquivo) }));
    setAnexos([...anexos, ...novos]);
  };

  const removerAnexo = ({ url }: Anexo) => {
    URL.revokeObjectURL(url);
    setAnexos(anexos.filter((anexo) => anexo.url !== url));
  };

  const enviar = async (data: FormData) => {
    const atividade = await criar.mutateAsync({
      tipo: data.tipo,
      duracaoSegundos: data.duracaoMinutos * 60,
      startedAt: new Date(data.startedAt).toISOString(),
      descricao: data.descricao,
      tipoComprovacao: anexos.length > 0 ? "Fotografia" : "RegistroManual",
    });

    // A atividade já existe: uma imagem que falhe não pode fazer a pessoa registrar de novo.
    let enviadas = 0;
    for (const [indice, { arquivo }] of anexos.entries()) {
      setEnviando(`Enviando ${indice + 1} de ${anexos.length}…`);
      try {
        const preparada = await prepararEvidencia(arquivo);
        await anexarEvidencia(
          atividade.id,
          preparada.arquivo,
          preparada.miniatura,
        );
        enviadas += 1;
      } catch {
        // O resumo final mostra quantas faltaram.
      }
    }
    setEnviando(null);

    setRegistrada({
      id: atividade.id,
      faltamNoDia,
      enviadas,
      anexadas: anexos.length,
    });
  };

  const alterarDuracao = (delta: number) =>
    setValue(
      "duracaoMinutos",
      Math.min(1440, Math.max(passoEmMinutos, duracaoMinutos + delta)),
      { shouldValidate: true },
    );

  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="Registrar atividade"
        back={`/grupos/${grupoId}/inicio`}
      />

      {minimoMinutos !== null && (
        <div className="mb-5 flex gap-3 rounded-2xl bg-soft p-4 text-sm text-on-soft">
          <Info aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <p>
            Para contar na meta do grupo, some{" "}
            <b>pelo menos {minimoMinutos} minutos</b> de atividade no dia. Vale
            dividir em mais de um treino.
          </p>
        </div>
      )}

      <form noValidate onSubmit={(event) => void handleSubmit(enviar)(event)}>
        <fieldset>
          <legend className="mb-2 text-sm font-semibold">
            Tipo de atividade
          </legend>
          <div className="grid grid-cols-3 gap-2">
            {tiposAtividade.map((tipo) => {
              const Icon = tipoAtividadeIcones[tipo];
              const selecionado = tipoAtual === tipo;
              return (
                <label
                  key={tipo}
                  className={`flex cursor-pointer flex-col items-center gap-1.5 rounded-xl border-2 bg-surface px-2 py-3.5 text-center text-xs font-semibold tap has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-focus ${selecionado ? "border-outline bg-soft text-on-soft shadow-sticker-sm" : errors.tipo ? "border-danger" : "border-border"}`}
                >
                  <input
                    type="radio"
                    value={tipo}
                    className="sr-only"
                    {...register("tipo")}
                  />
                  <Icon aria-hidden="true" className="size-6" />
                  {tipoAtividadeLabels[tipo]}
                </label>
              );
            })}
          </div>
          {errors.tipo && <p className="input-error">{errors.tipo.message}</p>}
        </fieldset>

        <p className="mt-6 mb-2 text-sm font-semibold">Duração</p>
        <div className="card flex items-center justify-between p-3">
          <button
            type="button"
            aria-label="Diminuir duração"
            className="grid size-12 place-items-center rounded-xl bg-soft text-on-soft tap"
            onClick={() => alterarDuracao(-passoEmMinutos)}
          >
            <Minus aria-hidden="true" className="size-5" />
          </button>
          <div className="text-center">
            <p className="font-score text-3xl">
              {duracaoMinutos}
              <span className="text-base text-muted"> min</span>
            </p>
            {minimoMinutos !== null && (
              <p
                className={`text-xs font-medium ${faltamNoDia === 0 ? "text-on-success-soft" : "text-on-warning-soft"}`}
              >
                {faltamNoDia === 0
                  ? "Conta para a meta"
                  : `Faltam ${faltamNoDia} min no dia para contar`}
              </p>
            )}
            {minimoMinutos !== null && minutosJaNoDia > 0 && (
              <p className="text-xs text-muted">
                Você já tem {minutosJaNoDia} min nesse dia
              </p>
            )}
          </div>
          <button
            type="button"
            aria-label="Aumentar duração"
            className="grid size-12 place-items-center rounded-xl bg-soft text-on-soft tap"
            onClick={() => alterarDuracao(passoEmMinutos)}
          >
            <Plus aria-hidden="true" className="size-5" />
          </button>
        </div>

        <label className="mt-6 block">
          <span className="mb-2 block text-sm font-semibold">
            Data e horário
          </span>
          <input
            type="datetime-local"
            max={toLocalInputValue(new Date())}
            className={`input-field ${errors.startedAt ? "border-danger" : ""}`}
            {...register("startedAt")}
          />
          {errors.startedAt && (
            <p className="input-error">{errors.startedAt.message}</p>
          )}
        </label>

        <p className="mt-6 mb-2 text-sm font-semibold">
          Comprovante <span className="font-normal text-muted">(opcional)</span>
        </p>
        <input
          ref={seletor}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          multiple
          className="hidden"
          onChange={(event) => {
            adicionarAnexos(event.target.files);
            event.target.value = "";
          }}
        />
        {anexos.length > 0 && (
          <ul className="mb-2 grid grid-cols-3 gap-2">
            {anexos.map((anexo, indice) => (
              <li
                key={anexo.url}
                className="relative overflow-hidden rounded-2xl border border-border"
              >
                <img
                  src={anexo.url}
                  alt={`Prévia do comprovante ${indice + 1}`}
                  className="aspect-square w-full object-cover"
                />
                <button
                  type="button"
                  aria-label={`Remover comprovante ${indice + 1}`}
                  className="absolute top-1.5 right-1.5 grid size-8 place-items-center rounded-full bg-surface text-danger shadow-card"
                  onClick={() => removerAnexo(anexo)}
                >
                  <Trash2 aria-hidden="true" className="size-4" />
                </button>
              </li>
            ))}
          </ul>
        )}
        {anexos.length < maximoDeEvidencias && (
          <button
            type="button"
            className="flex w-full flex-col items-center gap-2 rounded-2xl border-2 border-dashed border-border-strong bg-surface py-8 text-sm text-muted tap hover:border-outline"
            onClick={() => seletor.current?.click()}
          >
            <Camera aria-hidden="true" className="size-7 text-link" />
            <span className="font-medium text-foreground">
              Adicionar foto ou print
            </span>
            <span className="text-xs">
              Do relógio, app de corrida ou uma selfie no treino (até{" "}
              {maximoDeEvidencias})
            </span>
          </button>
        )}

        <label className="mt-6 block">
          <span className="mb-2 block text-sm font-semibold">
            Observações{" "}
            <span className="font-normal text-muted">(opcional)</span>
          </span>
          <textarea
            rows={3}
            placeholder="Como foi o treino?"
            className={`input-field h-auto resize-none py-3 ${errors.descricao ? "border-danger" : ""}`}
            {...register("descricao")}
          />
          {errors.descricao && (
            <p className="input-error">{errors.descricao.message}</p>
          )}
        </label>

        {errors.duracaoMinutos && (
          <p role="alert" className="form-error mt-4">
            {errors.duracaoMinutos.message}
          </p>
        )}
        {criar.isError && (
          <p role="alert" className="form-error mt-4">
            {getErrorMessage(criar.error)}
          </p>
        )}

        <button
          type="submit"
          className="btn-primary mt-6 w-full"
          disabled={criar.isPending || enviando !== null}
        >
          {enviando ?? (criar.isPending ? "Enviando…" : "Enviar atividade")}
        </button>
      </form>
    </div>
  );
}
