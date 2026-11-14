import { zodResolver } from "@hookform/resolvers/zod";
import {
  Camera,
  CheckCircle2,
  Images,
  Info,
  Lock,
  Minus,
  Play,
  Plus,
  Trash2,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useParams } from "react-router";
import { z } from "zod";
import { Avatar } from "@/components/Avatar";
import { PageHeader } from "@/components/PageHeader";
import { SplashScreen } from "@/components/SplashScreen";
import {
  anexarEvidencia,
  apagarRascunhoRegistro,
  CameraEvidencia,
  calcularAvisoDataFoto,
  formatarDataHora,
  lerRascunhoRegistro,
  marcarCapturaNoApp,
  maximoNoPassadoMs,
  analisarVideo,
  duracaoMaximaS,
  duracaoMinimaS,
  prepararEvidencia,
  prepararVideo,
  RecorteVideo,
  salvarRascunhoRegistro,
  type TrechoVideo,
  VideoNaoProcessadoError,
  type EvidenciaPreparada,
  tipoAtividadeIcones,
  tipoAtividadeLabels,
  tiposAtividade,
  useCriarAtividade,
  useFilaAvaliacao,
  useGaleria,
  type AtividadeResumo,
  type CriarAtividadeData,
  type TipoAtividade,
} from "@/features/atividades";
import {
  calcularFaltaNoDia,
  descreverMeta,
  formatarFaltaNoDia,
  useDesafio,
  useProgresso,
} from "@/features/desafios";
import { useSessao } from "@/features/autenticacao";
import { useGrupo } from "@/features/grupos";
import { useResumoGrupo } from "@/features/progresso";
import { ApiError, getErrorMessage } from "@/services/api";

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

/** Preparada na escolha: assim o autor vê o aviso da data antes de enviar (ADR 0052). */
type Anexo = { preparada: EvidenciaPreparada; url: string };

const maximoDeEvidencias = 3;
const passoEmMinutos = 5;
const maximoDeAutores = 5;

function toLocalInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function valoresPadrao(): Partial<FormData> {
  return {
    duracaoMinutos: 60,
    startedAt: toLocalInputValue(new Date()),
    descricao: "",
  };
}

function criarAnexo(preparada: EvidenciaPreparada): Anexo {
  // A prévia usa o arquivo já comprimido: é o que vai para o servidor e ocupa menos memória. No vídeo, a
  // miniatura (o quadro do meio).
  const previa =
    preparada.tipo === "Video" && preparada.miniatura
      ? preparada.miniatura
      : preparada.arquivo;
  return { preparada, url: URL.createObjectURL(previa) };
}

function formatarDuracaoVideo(ms: number) {
  const segundos = Math.round(ms / 1000);
  return `${Math.floor(segundos / 60)}:${String(segundos % 60).padStart(2, "0")}`;
}

type Registrada = {
  id: string;
  /** O que ainda faltava no dia para contar na meta; nulo se já conta ou sem desafio. */
  faltaNoDia: string | null;
  enviadas: number;
  anexadas: number;
  /** Motivos das imagens recusadas, sem repetição (ex.: foto já usada). */
  motivos: string[];
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
        {registrada.faltaNoDia
          ? `Registramos. ${registrada.faltaNoDia} para o dia contar na meta. `
          : ""}
        Seus amigos já podem dar moral.
      </p>
      {faltaram > 0 && (
        <p role="alert" className="form-error mt-4 w-full">
          {faltaram === 1
            ? "Um comprovante não pôde ser enviado."
            : `${faltaram} comprovantes não puderam ser enviados.`}
          {registrada.motivos.map((motivo) => ` ${motivo}`)}
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

/**
 * A API recusa o registro enquanto a fila de avaliação não estiver vazia: no lugar do formulário, a tela
 * mostra quem espera avaliação e leva à primeira atividade da fila.
 */
function AvisoFila({
  grupoId,
  pendentes,
}: {
  grupoId: string;
  pendentes: AtividadeResumo[];
}) {
  const autores = [
    ...new Map(pendentes.map((a) => [a.usuarioId, a.nomeExibicao])),
  ];
  const visiveis = autores.slice(0, maximoDeAutores);
  const outros = autores.length - visiveis.length;
  const total = pendentes.length;
  const atraso = (indice: number) => ({
    animationDelay: `${150 + indice * 70}ms`,
  });

  return (
    <div
      role="status"
      className="card flex animate-slide-up flex-col items-center p-6 text-center"
    >
      <ul
        aria-label={`Esperam sua avaliação: ${autores.map(([, nome]) => nome).join(", ")}`}
        className="flex -space-x-3"
      >
        {visiveis.map(([id, nome], indice) => (
          <li
            key={id}
            title={nome}
            className="animate-pop-in"
            style={atraso(indice)}
          >
            <Avatar nome={nome} id={id} size={56} ring />
          </li>
        ))}
        {outros > 0 && (
          <li
            className="grid size-14 animate-pop-in place-items-center rounded-full bg-soft text-sm font-semibold text-on-soft ring-2 ring-surface"
            style={atraso(visiveis.length)}
          >
            +{outros}
          </li>
        )}
      </ul>
      <h2 className="mt-5 text-2xl text-balance uppercase">
        {total === 1
          ? "Um treino espera sua avaliação"
          : `${total} treinos esperam sua avaliação`}
      </h2>
      <p className="mt-2 text-sm text-muted">
        Primeiro você confere os treinos da galera, depois registra o seu.
      </p>
      <ol className="mt-5 flex w-full items-center gap-2 text-xs font-semibold">
        <li className="flex shrink-0 items-center gap-1.5 rounded-full border-2 border-outline bg-reward px-3 py-1.5 text-on-reward">
          <span className="font-score">1</span> Avaliar {total}
        </li>
        <li
          aria-hidden="true"
          className="h-0.5 flex-1 rounded-full bg-border"
        />
        <li className="flex shrink-0 items-center gap-1.5 rounded-full border-2 border-border px-3 py-1.5 text-muted">
          <Lock aria-hidden="true" className="size-3.5" /> Seu treino
        </li>
      </ol>
      <Link
        to={`/grupos/${grupoId}/revisar/${pendentes[0]?.id}?depois=registrar&total=${total}`}
        replace
        className="btn-primary mt-6 w-full"
      >
        Começar avaliação
      </Link>
    </div>
  );
}

export function RegistrarAtividadePage() {
  const { id: grupoId = "" } = useParams();
  const grupo = useGrupo(grupoId);
  const fila = useFilaAvaliacao(grupoId);
  const resumo = useResumoGrupo(grupoId);
  const desafioId = resumo.data?.desafio?.id;
  const desafio = useDesafio(desafioId ?? "", desafioId !== undefined);
  const progresso = useProgresso(desafioId ?? "", desafioId !== undefined);
  const criar = useCriarAtividade(grupoId);
  const usuarioId = useSessao().data?.id;
  // A câmera aberta fica no histórico, como a galeria: o Voltar do celular a fecha.
  const camera = useGaleria("camera-registro");
  const seletor = useRef<HTMLInputElement>(null);
  const [anexos, setAnexos] = useState<Anexo[]>([]);
  const [preparando, setPreparando] = useState(false);
  const envio = useRef<{ assinatura: string; chave: string } | null>(null);
  // O servidor aceita atividades das últimas 48 horas (ADR 0052).
  const [inicioMinimo] = useState(() =>
    toLocalInputValue(new Date(Date.now() - maximoNoPassadoMs)),
  );
  const [erroAnexo, setErroAnexo] = useState<string | null>(null);
  const [enviando, setEnviando] = useState<string | null>(null);
  // Vídeo com mais de 1 minuto: a promessa espera a pessoa escolher o trecho (ADR 0058).
  const [recorte, setRecorte] = useState<{
    arquivo: File;
    duracao: number;
    resolver: (trecho: TrechoVideo | null) => void;
  } | null>(null);
  const [progressoVideo, setProgressoVideo] = useState<number | null>(null);
  const conversao = useRef<AbortController | null>(null);
  const [registrada, setRegistrada] = useState<Registrada | null>(null);
  const [rascunhoRestaurado, setRascunhoRestaurado] = useState(false);
  const rascunhoLido = useRef(false);

  const {
    register,
    handleSubmit,
    control,
    reset,
    setValue,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: valoresPadrao(),
  });
  const valores = useWatch({ control });

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

  // O Android pode encerrar o PWA enquanto a pessoa está na galeria: o rascunho traz tudo de volta (ADR 0058).
  useEffect(() => {
    if (!usuarioId || rascunhoLido.current) return;
    let cancelado = false;
    void lerRascunhoRegistro<Partial<FormData>>(usuarioId, grupoId).then(
      (rascunho) => {
        if (cancelado) return;
        rascunhoLido.current = true;
        if (!rascunho) return;
        reset({ ...valoresPadrao(), ...rascunho.valores });
        setAnexos(rascunho.evidencias.map(criarAnexo));
        envio.current = rascunho.envio;
        setRascunhoRestaurado(true);
      },
    );
    return () => {
      cancelado = true;
    };
  }, [usuarioId, grupoId, reset]);

  useEffect(() => {
    if (!usuarioId || !rascunhoLido.current || registrada) return;
    const temAlgo =
      valores.tipo !== undefined ||
      anexos.length > 0 ||
      (valores.descricao ?? "").trim() !== "";
    const espera = setTimeout(() => {
      if (temAlgo) {
        void salvarRascunhoRegistro(usuarioId, grupoId, {
          valores,
          evidencias: anexos.map(({ preparada }) => preparada),
          envio: envio.current,
        });
      } else {
        void apagarRascunhoRegistro(usuarioId, grupoId);
      }
    }, 300);
    return () => clearTimeout(espera);
  }, [valores, anexos, usuarioId, grupoId, registrada]);

  // As atividades do mesmo dia se somam, em proporção ao mínimo de cada tipo (decisões 0040 e 0045). O dia do
  // campo é o do aparelho, que costuma ser o fuso do grupo.
  const diaEscolhido = startedAt?.slice(0, 10);
  const doDia = progresso.data?.seusSegundosPorDia.find(
    (d) => d.data === diaEscolhido,
  );
  const minutosJaNoDia = Math.floor((doDia?.segundos ?? 0) / 60);
  const falta = desafio.data
    ? calcularFaltaNoDia(desafio.data, doDia, {
        tipo: tipoAtual,
        segundos: (duracaoMinutos || 0) * 60,
      })
    : null;
  const faltaNoDia = falta && !falta.conta ? formatarFaltaNoDia(falta) : null;

  const enviar = async (data: FormData) => {
    const dados: CriarAtividadeData = {
      tipo: data.tipo,
      duracaoSegundos: data.duracaoMinutos * 60,
      startedAt: new Date(data.startedAt).toISOString(),
      descricao: data.descricao,
      tipoComprovacao: anexos.length > 0 ? "Fotografia" : "RegistroManual",
    };
    // Uma nova tentativa com os mesmos dados repete a chave: se a primeira chegou ao servidor e a resposta
    // se perdeu, ele devolve a atividade já criada (ADR 0053). Dados diferentes geram outra chave.
    const assinatura = JSON.stringify(dados);
    if (envio.current?.assinatura !== assinatura) {
      envio.current = { assinatura, chave: crypto.randomUUID() };
    }
    // Se o aplicativo for encerrado no meio do envio, o rascunho repete a mesma chave.
    if (usuarioId) {
      await salvarRascunhoRegistro(usuarioId, grupoId, {
        valores: data,
        evidencias: anexos.map(({ preparada }) => preparada),
        envio: envio.current,
      });
    }
    const atividade = await criar.mutateAsync({
      ...dados,
      chave: envio.current.chave,
    });

    // A atividade já existe: uma imagem que falhe não pode fazer a pessoa registrar de novo.
    let enviadas = 0;
    const motivos = new Set<string>();
    for (const [indice, { preparada }] of anexos.entries()) {
      const rotulo = `Enviando ${indice + 1} de ${anexos.length}…`;
      setEnviando(rotulo);
      try {
        await anexarEvidencia(atividade.id, preparada, (fracao) =>
          setEnviando(`${rotulo} ${Math.round(fracao * 100)}%`),
        );
        enviadas += 1;
      } catch (error) {
        // Na repetição, a foto que já tinha chegado conta como enviada.
        if (
          error instanceof ApiError &&
          error.code === "EVIDENCIA_JA_ANEXADA"
        ) {
          enviadas += 1;
        }
        // O resumo final mostra quantos faltaram; a foto já usada e o vídeo recusado têm motivo próprio.
        if (
          error instanceof ApiError &&
          (error.code === "EVIDENCIA_DUPLICADA" ||
            error.code?.startsWith("VIDEO_") === true)
        ) {
          motivos.add(getErrorMessage(error));
        }
      }
    }
    setEnviando(null);
    if (usuarioId) void apagarRascunhoRegistro(usuarioId, grupoId);

    setRegistrada({
      id: atividade.id,
      faltaNoDia,
      enviadas,
      anexadas: anexos.length,
      motivos: [...motivos],
    });
  };

  // Se a fila não carregar, o formulário aparece e a API decide.
  const pendentes = fila.data ?? [];

  if (grupo.isPending || fila.isPending) return <SplashScreen />;

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

  // Também cobre a atividade que chega com o formulário aberto: o 409 atualiza a fila e o aviso o substitui.
  if (pendentes.length > 0) {
    return (
      <div className="mx-auto max-w-xl">
        <PageHeader
          title="Registrar atividade"
          back={`/grupos/${grupoId}/inicio`}
        />
        <AvisoFila grupoId={grupoId} pendentes={pendentes} />
      </div>
    );
  }

  // Prepara na escolha: a foto que o navegador não processa não entra, e nunca vai o original com EXIF (Q030).
  // `capturadaNoApp`: veio da câmera do aplicativo, no instante informado (ADR 0058).
  const adicionarAnexos = async (
    lista: FileList | File[] | null,
    capturadaNoApp?: Date,
  ) => {
    if (!lista) return;
    const escolhidos = Array.from(lista).slice(
      0,
      maximoDeEvidencias - anexos.length,
    );
    const ehVideo = (arquivo: File) => arquivo.type.startsWith("video/");
    const fotos = escolhidos.filter((arquivo) => !ehVideo(arquivo));
    const videos = escolhidos.filter(ehVideo);
    const marcar = (preparada: EvidenciaPreparada) =>
      capturadaNoApp
        ? marcarCapturaNoApp(preparada, capturadaNoApp)
        : preparada;
    const erros: string[] = [];
    setErroAnexo(null);

    if (fotos.length > 0) {
      setPreparando(true);
      const resultados = await Promise.allSettled(fotos.map(prepararEvidencia));
      setPreparando(false);
      const novos = resultados.flatMap((resultado) =>
        resultado.status === "fulfilled"
          ? [criarAnexo(marcar(resultado.value))]
          : [],
      );
      if (novos.length < fotos.length) {
        erros.push(
          "Não foi possível processar uma das fotos. Tente outra imagem.",
        );
      }
      setAnexos((atuais) => [...atuais, ...novos]);
    }

    // Um vídeo por vez: a conversão usa o codificador do aparelho.
    for (const video of videos) {
      const erro = await adicionarVideo(video, marcar);
      if (erro) erros.push(erro);
    }
    setErroAnexo(erros.length > 0 ? erros.join(" ") : null);
  };

  /** Prepara um vídeo; devolve o motivo da recusa, ou `null` quando entrou ou a pessoa desistiu. */
  const adicionarVideo = async (
    arquivo: File,
    marcar: (preparada: EvidenciaPreparada) => EvidenciaPreparada,
  ): Promise<string | null> => {
    setPreparando(true);
    try {
      const analise = await analisarVideo(arquivo);
      if (analise.duracao < duracaoMinimaS) {
        return "O vídeo precisa ter pelo menos 2 segundos.";
      }
      let trecho: TrechoVideo = {
        inicio: 0,
        fim: Math.min(analise.duracao, duracaoMaximaS),
      };
      if (analise.duracao > duracaoMaximaS + 0.5) {
        if (!analise.podeConverter) {
          return "Seu navegador não consegue recortar vídeos. Envie um vídeo de até 1 minuto.";
        }
        const escolhido = await new Promise<TrechoVideo | null>((resolver) =>
          setRecorte({ arquivo, duracao: analise.duracao, resolver }),
        );
        setRecorte(null);
        if (!escolhido) return null;
        trecho = escolhido;
      }

      const controle = new AbortController();
      conversao.current = controle;
      setProgressoVideo(0);
      const preparada = await prepararVideo(arquivo, analise, trecho, {
        onProgresso: setProgressoVideo,
        signal: controle.signal,
      });
      setAnexos((atuais) => [...atuais, criarAnexo(marcar(preparada))]);
      return null;
    } catch (error) {
      if (conversao.current?.signal.aborted) return null;
      return error instanceof VideoNaoProcessadoError
        ? error.message
        : "Não foi possível preparar este vídeo. Tente outro.";
    } finally {
      conversao.current = null;
      setProgressoVideo(null);
      setPreparando(false);
    }
  };

  const inicioEscolhido = startedAt ? new Date(startedAt) : null;
  const avisoDaFoto = ({ preparada }: Anexo) => {
    if (!inicioEscolhido || Number.isNaN(inicioEscolhido.getTime()))
      return null;
    const aviso = calcularAvisoDataFoto(
      inicioEscolhido,
      (duracaoMinutos || 0) * 60,
      preparada.sinais,
    );
    const video = preparada.tipo === "Video";
    if (aviso === "FotoForaDoPeriodo" && preparada.sinais.capturadaEm) {
      return `${video ? "Vídeo" : "Foto"} de ${formatarDataHora(preparada.sinais.capturadaEm.toISOString())}, fora do horário do treino. O grupo verá um aviso.`;
    }
    if (aviso === "FotoSemData") {
      return `Sem data ${video ? "no vídeo" : "na foto"}. O grupo verá que não deu para conferir.`;
    }
    return null;
  };

  const descartarRascunho = () => {
    anexos.forEach(({ url }) => URL.revokeObjectURL(url));
    setAnexos([]);
    reset(valoresPadrao());
    envio.current = null;
    setErroAnexo(null);
    setRascunhoRestaurado(false);
    if (usuarioId) void apagarRascunhoRegistro(usuarioId, grupoId);
  };

  const removerAnexo = ({ url }: Anexo) => {
    URL.revokeObjectURL(url);
    setAnexos(anexos.filter((anexo) => anexo.url !== url));
  };

  const alterarDuracao = (delta: number) =>
    setValue(
      "duracaoMinutos",
      Math.min(1440, Math.max(passoEmMinutos, duracaoMinutos + delta)),
      { shouldValidate: true },
    );

  return (
    <div className="mx-auto max-w-xl animate-slide-up">
      <PageHeader
        title="Registrar atividade"
        back={`/grupos/${grupoId}/inicio`}
      />

      {rascunhoRestaurado && (
        <div
          role="status"
          className="mb-5 flex items-center gap-3 rounded-2xl bg-soft p-4 text-sm text-on-soft"
        >
          <Info aria-hidden="true" className="size-4 shrink-0" />
          <p className="flex-1">
            Recuperamos o registro que você não terminou.
          </p>
          <button
            type="button"
            className="btn-ghost h-11 shrink-0 px-3"
            onClick={descartarRascunho}
          >
            Descartar
          </button>
        </div>
      )}

      {desafio.data && (
        <div className="mb-5 flex gap-3 rounded-2xl bg-soft p-4 text-sm text-on-soft">
          <Info aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <p>
            Para contar na meta do grupo, some no dia pelo menos{" "}
            <b>{descreverMeta(desafio.data)}</b>. Vale dividir em mais de um
            treino
            {desafio.data.metasPorTipo.length > 0 &&
              ", e tipos diferentes somam em proporção ao mínimo de cada um"}
            .
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
          <div className="min-w-0 flex-1 px-2 text-center">
            <p className="font-score text-3xl">
              {duracaoMinutos}
              <span className="text-base text-muted"> min</span>
            </p>
            {falta && (
              <p
                className={`text-xs font-medium text-balance ${falta.conta ? "text-on-success-soft" : "text-on-warning-soft"}`}
              >
                {falta.conta
                  ? "Conta para a meta"
                  : `${faltaNoDia} para contar`}
              </p>
            )}
            {falta && minutosJaNoDia > 0 && (
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
            min={inicioMinimo}
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
          accept="image/jpeg,image/png,image/webp,video/*"
          multiple
          className="hidden"
          onChange={(event) => {
            const arquivos = event.target.files;
            void adicionarAnexos(arquivos).finally(() => {
              event.target.value = "";
            });
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
                  alt={`Prévia do comprovante ${indice + 1}${anexo.preparada.tipo === "Video" ? ", vídeo" : ""}`}
                  className="aspect-square w-full object-cover"
                />
                {anexo.preparada.tipo === "Video" && (
                  <span className="absolute bottom-1.5 left-1.5 flex items-center gap-1 rounded-full bg-lightbox/60 px-2 py-0.5 text-xs font-semibold text-on-lightbox">
                    <Play aria-hidden="true" className="size-3 fill-current" />
                    {formatarDuracaoVideo(anexo.preparada.duracaoMs ?? 0)}
                  </span>
                )}
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
        {anexos.map(avisoDaFoto).some(Boolean) && (
          <ul className="mb-2 space-y-1" aria-live="polite">
            {anexos.map((anexo, indice) => {
              const aviso = avisoDaFoto(anexo);
              return (
                aviso && (
                  <li
                    key={anexo.url}
                    className="flex gap-1.5 text-xs text-muted"
                  >
                    <Info
                      aria-hidden="true"
                      className="mt-0.5 size-3.5 shrink-0"
                    />
                    <span>
                      Comprovante {indice + 1}: {aviso}
                    </span>
                  </li>
                )
              );
            })}
          </ul>
        )}
        {erroAnexo && (
          <p role="alert" className="input-error mb-2">
            {erroAnexo}
          </p>
        )}
        {anexos.length < maximoDeEvidencias && (
          <div className="rounded-2xl border-2 border-dashed border-border-strong bg-surface p-3">
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                disabled={preparando}
                className="flex flex-col items-center gap-1.5 rounded-xl bg-soft py-5 text-sm font-medium text-on-soft tap disabled:opacity-60"
                onClick={() => camera.abrir(0)}
              >
                <Camera aria-hidden="true" className="size-7" />
                Câmera
              </button>
              <button
                type="button"
                disabled={preparando}
                className="flex flex-col items-center gap-1.5 rounded-xl bg-soft py-5 text-sm font-medium text-on-soft tap disabled:opacity-60"
                onClick={() => seletor.current?.click()}
              >
                <Images aria-hidden="true" className="size-7" />
                Galeria
              </button>
            </div>
            {progressoVideo !== null ? (
              <div className="mt-3" role="status">
                <div className="flex items-center justify-between gap-3 text-xs">
                  <span>
                    Preparando o vídeo… {Math.round(progressoVideo * 100)}%
                  </span>
                  <button
                    type="button"
                    className="btn-ghost h-11 px-3"
                    onClick={() => conversao.current?.abort()}
                  >
                    Cancelar
                  </button>
                </div>
                <div className="h-1.5 overflow-hidden rounded-full bg-soft">
                  <div
                    className="h-full rounded-full bg-progress transition-[width]"
                    style={{ width: `${Math.round(progressoVideo * 100)}%` }}
                  />
                </div>
              </div>
            ) : (
              <p className="mt-2 text-center text-xs text-muted">
                {preparando
                  ? "Preparando…"
                  : `Selfie, vídeo de até 1 minuto ou print do relógio (até ${maximoDeEvidencias})`}
              </p>
            )}
          </div>
        )}
        <CameraEvidencia
          aberta={camera.aberta}
          onFechar={camera.fechar}
          onFoto={(arquivo) => {
            const instante = new Date();
            camera.fechar();
            void adicionarAnexos([arquivo], instante);
          }}
          onVideo={(arquivo, iniciadoEm) => {
            camera.fechar();
            void adicionarAnexos([arquivo], iniciadoEm);
          }}
        />
        {recorte && (
          <RecorteVideo
            arquivo={recorte.arquivo}
            duracao={recorte.duracao}
            onEscolher={recorte.resolver}
            onCancelar={() => recorte.resolver(null)}
          />
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
