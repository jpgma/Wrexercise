import {
  Camera,
  CheckCircle2,
  Images,
  Minus,
  Play,
  Plus,
  Sun,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, Navigate, useNavigate, useParams } from "react-router";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { SplashScreen } from "@/components/SplashScreen";
import {
  anexarEvidencia,
  CameraEvidencia,
  formatarCronometro,
  formatarDuracao,
  GaleriaEvidencias,
  getRotaTreino,
  marcarCapturaNoApp,
  prepararMidia,
  tipoAtividadeIcones,
  tipoAtividadeLabels,
  treinoAtualKey,
  LocalTreino,
  useAtividade,
  useCancelarTreino,
  useGaleria,
  useSegundosDesde,
  useTerminarTreino,
  useTreinoAtual,
  VideoNaoProcessadoError,
  type Atividade,
  type LocalCaptura,
  type Torcida,
  type TreinoAtual,
} from "@/features/atividades";
import { useSessao } from "@/features/autenticacao";
import {
  calcularFaltaNoDia,
  formatarFaltaNoDia,
  useDesafio,
  useProgresso,
} from "@/features/desafios";
import {
  AvatarTreino,
  AvisoCartaNoTreino,
  useCartaEsperando,
  useCartasQueFalham,
} from "@/features/jogo";
import {
  GerarImagemPercurso,
  PercursoAoVivo,
  useGravacaoPercurso,
  type GravacaoPercurso,
} from "@/features/percurso";
import { useResumoGrupo } from "@/features/progresso";
import { getErrorMessage } from "@/services/api";
import { useQueryClient } from "@tanstack/react-query";

/** Os marcos do cronômetro, em minutos: a cada um, uma comemoração (ADR 0078). */
const marcos = [15, 30, 45, 60, 90, 120, 180];
const duracaoComemoracaoMs = 4000;
const maximoDeEvidencias = 10;
/** Depois disso, quem termina confirma ou ajusta o tempo: pode ter esquecido o treino aberto. */
const confirmarAcimaDeS = 4 * 60 * 60;

const textoMarco = (minutos: number) =>
  minutos >= 60
    ? `${minutos / 60}h de treino! Que fôlego 🔥`
    : `${minutos} min! Segue firme 💪`;

/** O último marco que o tempo já passou, ou 0. */
const getMarco = (segundos: number) =>
  [...marcos].reverse().find((m) => segundos >= m * 60) ?? 0;

/** A data de hoje no aparelho, no formato da API (AAAA-MM-DD). */
const getHoje = () => {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
};

const chaveTorcida = (t: Torcida) => `${t.usuarioId}:${t.criadaEm}`;

/** Mantém a tela acesa enquanto a pessoa quiser, onde o navegador permite. */
function useTelaAcesa(ligada: boolean) {
  useEffect(() => {
    if (!ligada || !("wakeLock" in navigator)) return;
    let trava: WakeLockSentinel | null = null;
    let ativo = true;
    const pedir = async () => {
      try {
        trava = await navigator.wakeLock.request("screen");
      } catch {
        // Bateria fraca ou aba em segundo plano: segue sem travar a tela.
      }
    };
    const aoVoltar = () => {
      if (ativo && document.visibilityState === "visible") void pedir();
    };
    void pedir();
    document.addEventListener("visibilitychange", aoVoltar);
    return () => {
      ativo = false;
      document.removeEventListener("visibilitychange", aoVoltar);
      void trava?.release();
    };
  }, [ligada]);
}

/** As Torcidas que chegaram com a tela aberta: cada uma aparece sobre o Avatar por alguns segundos. */
function useTorcidasNovas(torcidas: Torcida[] | undefined) {
  const vistas = useRef<Set<string> | null>(null);
  const [novas, setNovas] = useState<Torcida[]>([]);

  useEffect(() => {
    if (!torcidas) return;
    // As que já existiam ao abrir a tela não aparecem de novo.
    if (vistas.current === null) {
      vistas.current = new Set(torcidas.map(chaveTorcida));
      return;
    }
    const chegaram = torcidas.filter(
      (t) => !vistas.current!.has(chaveTorcida(t)),
    );
    if (chegaram.length === 0) return;
    chegaram.forEach((t) => vistas.current!.add(chaveTorcida(t)));
    setNovas((atuais) => [...atuais, ...chegaram]);
    const espera = window.setTimeout(
      () => setNovas((atuais) => atuais.filter((t) => !chegaram.includes(t))),
      duracaoComemoracaoMs,
    );
    return () => window.clearTimeout(espera);
  }, [torcidas]);

  return novas;
}

export function TreinoPage() {
  const { id: grupoId = "" } = useParams();
  const treino = useTreinoAtual(true);
  const [terminada, setTerminada] = useState<Atividade | null>(null);

  if (terminada) {
    return <TreinoTerminado atividade={terminada} grupoId={grupoId} />;
  }
  if (treino.isPending) return <SplashScreen />;
  if (treino.isError) {
    return (
      <div className="mx-auto max-w-xl">
        <p role="alert" className="form-error">
          {getErrorMessage(treino.error)}
        </p>
      </div>
    );
  }
  if (!treino.data) return <SemTreino grupoId={grupoId} />;
  // O treino é de outro grupo: o cronômetro mora na rota dele.
  if (treino.data.grupoId !== grupoId) {
    return <Navigate to={getRotaTreino(treino.data)} replace />;
  }
  return <Cronometro treino={treino.data} onTerminado={setTerminada} />;
}

function SemTreino({ grupoId }: { grupoId: string }) {
  return (
    <div className="mx-auto flex max-w-md flex-col items-center pt-16 text-center">
      <h1 className="text-3xl uppercase">Nenhum treino ao vivo</h1>
      <p className="mt-2 text-muted">
        Quando você começar um treino, o cronômetro aparece aqui.
      </p>
      <Link
        to={`/grupos/${grupoId}/atividades/nova`}
        className="btn-primary mt-8 w-full"
      >
        Começar treino
      </Link>
      <Link to={`/grupos/${grupoId}/inicio`} className="btn-ghost mt-2 w-full">
        Voltar ao início
      </Link>
    </div>
  );
}

function Cronometro({
  treino,
  onTerminado,
}: {
  treino: TreinoAtual;
  onTerminado: (atividade: Atividade) => void;
}) {
  const { grupoId } = treino;
  const navigate = useNavigate();
  const sessao = useSessao().data;
  const segundos = useSegundosDesde(treino.startedAt);
  const Icone = tipoAtividadeIcones[treino.tipo];
  const cartaEsperando = useCartaEsperando();

  // Marcos: só os que o tempo passar com a tela aberta comemoram.
  const ultimoMarco = useRef(getMarco(segundos));
  const [marco, setMarco] = useState<number | null>(null);
  const marcoAtual = getMarco(segundos);
  useEffect(() => {
    if (marcoAtual <= ultimoMarco.current) return;
    ultimoMarco.current = marcoAtual;
    setMarco(marcoAtual);
    const espera = window.setTimeout(
      () => setMarco(null),
      duracaoComemoracaoMs,
    );
    return () => window.clearTimeout(espera);
  }, [marcoAtual]);

  const torcidasNovas = useTorcidasNovas(treino.torcidas);
  const comemorando = marco !== null || torcidasNovas.length > 0;

  // O Percurso (ADR 0084): o GPS de um PWA só grava com a tela acesa e o app à frente.
  const gravacao = useGravacaoPercurso({
    atividadeId: treino.atividadeId,
    tipo: treino.tipo,
    percurso: treino.percurso,
    app: treino.app,
  });
  const gravando =
    treino.percurso !== null &&
    gravacao.estado !== "parado" &&
    gravacao.estado !== "noApp";

  const [telaAcesa, setTelaAcesa] = useState(false);
  useTelaAcesa(telaAcesa || gravando);
  const podeTravarTela = "wakeLock" in navigator;

  // A meta do dia com o tempo que já passou: quanto falta para o treino contar.
  const resumo = useResumoGrupo(grupoId);
  const desafioId = resumo.data?.desafio?.id;
  const desafio = useDesafio(desafioId ?? "", desafioId !== undefined);
  const progresso = useProgresso(desafioId ?? "", desafioId !== undefined);
  const doDia = progresso.data?.seusSegundosPorDia.find(
    (d) => d.data === getHoje(),
  );
  const falta = desafio.data
    ? calcularFaltaNoDia(desafio.data, doDia, {
        tipo: treino.tipo,
        segundos,
      })
    : null;

  const [terminando, setTerminando] = useState(false);
  const [cancelando, setCancelando] = useState(false);
  const cancelar = useCancelarTreino(treino.atividadeId);

  return (
    <div className="mx-auto max-w-xl animate-fade-in">
      <div className="flex items-center justify-between gap-2">
        <p className="chip chip-danger">
          <span
            aria-hidden="true"
            className="size-2 rounded-full bg-danger motion-safe:animate-pulse"
          />
          AO VIVO
        </p>
        <p className="truncate text-sm text-muted">{treino.grupoNome}</p>
      </div>

      {treino.app && treino.appEndedAt && (
        <p
          role="status"
          className="chip chip-success mt-3 w-full whitespace-normal"
        >
          {treino.app}: treino concluído
          {treino.appDuracaoSegundos
            ? ` · ${formatarDuracao(treino.appDuracaoSegundos)} ativos`
            : ""}
          . Põe as fotos e termine aqui.
        </p>
      )}

      <section
        aria-labelledby="treino-titulo"
        className="card-sticker relative mt-4 flex flex-col items-center overflow-hidden px-4 pt-6 pb-5 text-center"
      >
        <h1
          id="treino-titulo"
          className="flex items-center gap-2 text-2xl uppercase"
        >
          <Icone aria-hidden="true" className="size-6" />
          {tipoAtividadeLabels[treino.tipo]}
        </h1>
        <p
          role="timer"
          aria-label={`Tempo de treino: ${formatarDuracao(Math.max(60, segundos))}`}
          className="mt-2 font-score text-7xl leading-none tabular-nums"
        >
          {formatarCronometro(segundos)}
        </p>

        <div className="relative mt-4 grid h-36 place-items-end">
          {sessao && (
            <AvatarTreino
              usuarioId={sessao.id}
              tipo={treino.tipo}
              comemorando={comemorando}
            />
          )}
          {torcidasNovas.map((t, i) => (
            <span
              key={chaveTorcida(t)}
              aria-hidden="true"
              className="absolute animate-pop rounded-full border-2 border-outline bg-surface px-2 py-0.5 text-xs font-semibold whitespace-nowrap shadow-sticker"
              style={{ top: i * 28, left: i % 2 ? "70%" : "10%" }}
            >
              📣 {t.nomeExibicao.split(" ")[0]}
            </span>
          ))}
        </div>

        {/* Os marcos e as Torcidas são anunciados uma vez; o cronômetro, não. */}
        <p aria-live="polite" className="mt-2 min-h-7 font-semibold">
          {marco !== null
            ? textoMarco(marco)
            : torcidasNovas.length > 0
              ? `${torcidasNovas.map((t) => t.nomeExibicao.split(" ")[0]).join(" e ")} ${torcidasNovas.length === 1 ? "está" : "estão"} torcendo por você!`
              : ""}
        </p>

        {falta && (
          <p
            className={`chip mt-1 whitespace-normal ${falta.conta ? "chip-success" : ""}`}
          >
            {falta.conta
              ? "Já conta para a meta do dia ✅"
              : `${formatarFaltaNoDia(falta)} para contar`}
          </p>
        )}
        {treino.torcidas.length > 0 && (
          <p className="mt-2 text-xs text-muted">
            📣 {treino.torcidas.length}{" "}
            {treino.torcidas.length === 1 ? "torcida" : "torcidas"} neste treino
          </p>
        )}
      </section>

      {cartaEsperando && (
        <p
          role="status"
          className="chip chip-game mt-3 w-full whitespace-normal"
        >
          🃏 Uma carta chegou! Ela espera você terminar o treino.
        </p>
      )}

      {treino.percurso ? (
        <PercursoAoVivo
          atividadeId={treino.atividadeId}
          tipo={treino.tipo}
          segundos={segundos}
          gravacao={gravacao}
        />
      ) : (
        <LocalDoTreinoAoVivo treino={treino} />
      )}
      <EvidenciasAoVivo treino={treino} />

      {podeTravarTela && (
        <label className="mt-4 flex min-h-11 cursor-pointer items-center gap-3 rounded-xl bg-soft px-3 py-2 text-sm text-on-soft">
          <input
            type="checkbox"
            className="size-5 shrink-0 accent-progress"
            checked={telaAcesa || gravando}
            disabled={gravando}
            onChange={(e) => setTelaAcesa(e.target.checked)}
          />
          <span className="flex-1">
            Manter a tela acesa
            {gravando && (
              <span className="block text-xs">
                Ligada para gravar o percurso
              </span>
            )}
          </span>
          <Sun aria-hidden="true" className="size-5 shrink-0" />
        </label>
      )}

      {terminando ? (
        <TerminarTreino
          treino={treino}
          segundos={segundos}
          gravacao={gravacao}
          onCancelar={() => setTerminando(false)}
          onTerminado={onTerminado}
        />
      ) : (
        <div className="mt-6 space-y-2">
          <button
            type="button"
            className="btn-primary h-14 w-full text-lg"
            onClick={() => setTerminando(true)}
          >
            Terminar treino
          </button>
          <button
            type="button"
            className="btn-ghost w-full text-danger"
            onClick={() => setCancelando(true)}
          >
            Cancelar treino
          </button>
        </div>
      )}

      <ConfirmDialog
        open={cancelando}
        title="Cancelar o treino?"
        message="O treino e as fotos dele somem. Ninguém perde nada, e você pode começar de novo."
        confirmLabel="Cancelar treino"
        pending={cancelar.isPending}
        error={cancelar.isError ? getErrorMessage(cancelar.error) : undefined}
        onConfirm={() =>
          cancelar.mutate(undefined, {
            onSuccess: () => {
              gravacao.descartar();
              void navigate(`/grupos/${grupoId}/inicio`);
            },
          })
        }
        onCancel={() => setCancelando(false)}
      />
    </div>
  );
}

/** Onde o treino começou, se a pessoa escolheu mostrar (ADR 0080): ela vê o que o grupo vê. */
function LocalDoTreinoAoVivo({ treino }: { treino: TreinoAtual }) {
  const detalhe = useAtividade(treino.atividadeId);
  if (!detalhe.data) return null;
  return (
    <LocalTreino
      atividadeId={treino.atividadeId}
      latitude={detalhe.data.latitude}
      longitude={detalhe.data.longitude}
      aoVivo
      className="card mt-4 p-3"
    />
  );
}

/** Fotos e vídeos durante o treino: cada um sobe na hora, até 10 no total (ADR 0078). */
function EvidenciasAoVivo({ treino }: { treino: TreinoAtual }) {
  const queryClient = useQueryClient();
  const camera = useGaleria("camera-treino");
  const seletor = useRef<HTMLInputElement>(null);
  const [enviando, setEnviando] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const vagas = maximoDeEvidencias - treino.totalEvidencias;

  // Durante o treino, a localização não sai do aparelho: quem quiser mostrar, registra depois.
  const enviar = async (
    lista: FileList | File[] | null,
    capturada?: { em: Date; local: LocalCaptura | null },
  ) => {
    if (!lista) return;
    setErro(null);
    const arquivos = Array.from(lista).slice(0, vagas);
    for (const [indice, arquivo] of arquivos.entries()) {
      const rotulo =
        arquivos.length > 1
          ? `Enviando ${indice + 1} de ${arquivos.length}…`
          : "Enviando…";
      setEnviando(rotulo);
      try {
        const preparada = await prepararMidia(arquivo, (f) =>
          setEnviando(`Preparando… ${Math.round(f * 100)}%`),
        );
        const marcada = capturada
          ? marcarCapturaNoApp(preparada, capturada.em, capturada.local)
          : preparada;
        await anexarEvidencia(treino.atividadeId, marcada, false, (f) =>
          setEnviando(`${rotulo} ${Math.round(f * 100)}%`),
        );
      } catch (error) {
        setErro(
          error instanceof VideoNaoProcessadoError
            ? error.message
            : getErrorMessage(error),
        );
      }
    }
    setEnviando(null);
    void queryClient.invalidateQueries({ queryKey: treinoAtualKey });
    // As miniaturas vêm do detalhe da atividade.
    void queryClient.invalidateQueries({
      queryKey: ["atividades", treino.atividadeId],
    });
  };

  return (
    <section aria-labelledby="evidencias-titulo" className="mt-6">
      <h2 id="evidencias-titulo" className="mb-2 text-sm font-semibold">
        Fotos e vídeos{" "}
        <span className="font-normal text-muted">
          ({treino.totalEvidencias} de {maximoDeEvidencias})
        </span>
      </h2>
      <input
        ref={seletor}
        type="file"
        accept="image/jpeg,image/png,image/webp,video/*"
        multiple
        className="hidden"
        onChange={(event) => {
          void enviar(event.target.files).finally(() => {
            event.target.value = "";
          });
        }}
      />
      {treino.totalEvidencias > 0 && <MiniaturasTreino treino={treino} />}
      {vagas > 0 ? (
        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            disabled={enviando !== null}
            className="flex flex-col items-center gap-1.5 rounded-xl bg-soft py-4 text-sm font-medium text-on-soft tap disabled:opacity-60"
            onClick={() => camera.abrir(0)}
          >
            <Camera aria-hidden="true" className="size-6" />
            Câmera
          </button>
          <button
            type="button"
            disabled={enviando !== null}
            className="flex flex-col items-center gap-1.5 rounded-xl bg-soft py-4 text-sm font-medium text-on-soft tap disabled:opacity-60"
            onClick={() => seletor.current?.click()}
          >
            <Images aria-hidden="true" className="size-6" />
            Galeria
          </button>
        </div>
      ) : (
        <p className="text-sm text-muted">
          Você já mandou {maximoDeEvidencias} fotos e vídeos. Agora é treinar!
        </p>
      )}
      <p aria-live="polite" className="mt-2 text-center text-xs text-muted">
        {enviando ?? "Selfie no meio do treino, o equipamento, o relógio…"}
      </p>
      {erro && (
        <p role="alert" className="input-error">
          {erro}
        </p>
      )}
      <CameraEvidencia
        aberta={camera.aberta}
        pedirLocalizacao={false}
        onFechar={camera.fechar}
        onFoto={(arquivo, local) => {
          const em = new Date();
          camera.fechar();
          void enviar([arquivo], { em, local });
        }}
        onVideo={(arquivo, em, local) => {
          camera.fechar();
          void enviar([arquivo], { em, local });
        }}
      />
    </section>
  );
}

/**
 * O que já foi enviado durante o treino, em miniaturas: tocar abre a galeria em tela cheia. A legenda
 * da galeria usa o tempo decorrido, porque a duração do treino só existe ao terminar.
 */
function MiniaturasTreino({ treino }: { treino: TreinoAtual }) {
  const detalhe = useAtividade(treino.atividadeId);
  const galeria = useGaleria("evidencias-treino");
  const segundos = useSegundosDesde(treino.startedAt, 30_000);
  const evidencias = detalhe.data?.evidencias ?? [];

  return (
    <>
      <ul
        aria-label="Já enviados"
        className="mb-2 flex gap-2 overflow-x-auto pb-1 [scrollbar-width:none]"
      >
        {evidencias.length === 0
          ? Array.from({ length: treino.totalEvidencias }, (_, i) => (
              <li
                key={i}
                aria-hidden="true"
                className="size-16 shrink-0 animate-pulse rounded-xl bg-surface-muted"
              />
            ))
          : evidencias.map((evidencia, i) => (
              <li key={evidencia.id} className="shrink-0">
                <button
                  type="button"
                  onClick={() => galeria.abrir(i)}
                  aria-label={
                    evidencia.tipo === "Video"
                      ? `Assistir ao vídeo ${i + 1}`
                      : `Ver a foto ${i + 1}`
                  }
                  className="relative block size-16 cursor-zoom-in overflow-hidden rounded-xl border-2 border-outline tap"
                >
                  <img
                    src={`${evidencia.url}/miniatura`}
                    alt=""
                    loading="lazy"
                    className="size-full object-cover"
                    // Sem miniatura (uma foto antiga ou um navegador que não a gerou), a imagem inteira.
                    onError={(e) => {
                      if (evidencia.tipo === "Foto")
                        e.currentTarget.src = evidencia.url;
                    }}
                  />
                  {evidencia.tipo === "Video" && (
                    <span className="absolute inset-0 grid place-items-center bg-lightbox/30 text-on-lightbox">
                      <Play
                        aria-hidden="true"
                        className="size-5 fill-current"
                      />
                    </span>
                  )}
                </button>
              </li>
            ))}
      </ul>
      {detalhe.data && (
        <GaleriaEvidencias
          aberta={galeria.aberta}
          indiceInicial={galeria.indiceInicial}
          evidencias={evidencias}
          legenda={{ ...detalhe.data, duracaoSegundos: Math.max(60, segundos) }}
          onFechar={galeria.fechar}
        />
      )}
    </>
  );
}

/**
 * "Terminar treino": a duração parte do tempo decorrido e só diminui (quem esqueceu de terminar
 * ajusta). Passando de 4 horas, a pessoa confirma o tempo antes de enviar.
 */
function TerminarTreino({
  treino,
  segundos,
  gravacao,
  onCancelar,
  onTerminado,
}: {
  treino: TreinoAtual;
  segundos: number;
  gravacao: GravacaoPercurso;
  onCancelar: () => void;
  onTerminado: (atividade: Atividade) => void;
}) {
  const sessao = useSessao().data;
  const terminar = useTerminarTreino(treino.atividadeId);
  // O tempo fica parado enquanto a pessoa confere: o que ela vê é o que vai.
  const [maximoMin] = useState(() =>
    Math.min(24 * 60, Math.max(1, Math.floor(segundos / 60))),
  );
  // Começado por um app que já acabou (ADR 0081): o tempo ativo dele vem sugerido, sem passar do decorrido.
  const sugestaoMin =
    treino.appEndedAt && treino.appDuracaoSegundos
      ? Math.min(
          maximoMin,
          Math.max(1, Math.round(treino.appDuracaoSegundos / 60)),
        )
      : null;
  const [minutos, setMinutos] = useState(sugestaoMin ?? maximoMin);
  const [descricao, setDescricao] = useState("");
  const longo = maximoMin * 60 > confirmarAcimaDeS;
  const [confirmado, setConfirmado] = useState(false);
  const precisaConfirmar = longo && minutos === maximoMin && !confirmado;
  // O servidor aceita de 1 minuto em diante: antes disso, o botão espera.
  const curto = segundos < 60;
  const [confirmandoCarta, setConfirmandoCarta] = useState(false);
  const titulo = useRef<HTMLHeadingElement>(null);
  useEffect(() => titulo.current?.focus(), []);

  const treinoPrevisto = {
    tipo: treino.tipo,
    startedAt: treino.startedAt,
    duracaoSegundos: minutos * 60,
  };
  const cartasQueFalham = useCartasQueFalham(
    treino.grupoId,
    treinoPrevisto,
    sessao?.modoInicio === "Jogo",
  );

  // Antes de terminar, os pontos que ainda estão no aparelho sobem: o servidor só aceita durante o treino.
  const [erroPercurso, setErroPercurso] = useState<string | null>(null);
  const [descarregando, setDescarregando] = useState(false);
  const enviar = async () => {
    setErroPercurso(null);
    if (treino.percurso && gravacao.estado !== "parado") {
      setDescarregando(true);
      try {
        await gravacao.finalizar();
      } catch (error) {
        setErroPercurso(
          `Não deu para enviar o fim do percurso: ${getErrorMessage(error)} Tente de novo.`,
        );
        return;
      } finally {
        setDescarregando(false);
      }
    }
    terminar.mutate(
      {
        duracaoSegundos: minutos * 60,
        descricao,
        tipoComprovacao:
          treino.totalEvidencias > 0 ? "Fotografia" : "RegistroManual",
      },
      {
        onSuccess: (atividade) => {
          gravacao.descartar();
          onTerminado(atividade);
        },
      },
    );
  };

  const alterar = (delta: number) =>
    setMinutos((m) => Math.min(maximoMin, Math.max(1, m + delta)));

  return (
    <section
      aria-labelledby="terminar-titulo"
      className="card mt-6 animate-slide-up p-4"
    >
      <h2
        id="terminar-titulo"
        ref={titulo}
        tabIndex={-1}
        className="text-xl uppercase outline-none"
      >
        Terminar treino
      </h2>

      <p className="mt-4 mb-2 text-sm font-semibold">Duração</p>
      <div className="flex items-center justify-between rounded-xl bg-surface-muted p-2">
        <button
          type="button"
          aria-label="Diminuir duração"
          className="grid size-12 place-items-center rounded-xl bg-soft text-on-soft tap"
          onClick={() => alterar(-5)}
        >
          <Minus aria-hidden="true" className="size-5" />
        </button>
        <p className="font-score text-3xl tabular-nums">
          {minutos}
          <span className="text-base text-muted"> min</span>
        </p>
        <button
          type="button"
          aria-label="Aumentar duração"
          disabled={minutos >= maximoMin}
          className="grid size-12 place-items-center rounded-xl bg-soft text-on-soft tap disabled:opacity-40"
          onClick={() => alterar(5)}
        >
          <Plus aria-hidden="true" className="size-5" />
        </button>
      </div>
      <p className="mt-1 text-xs text-muted">
        {sugestaoMin !== null
          ? `Sugestão do ${treino.app}: ${sugestaoMin} min (sem as pausas).`
          : "Esqueceu de terminar na hora? Diminua até o tempo que você treinou."}
      </p>

      {longo && minutos === maximoMin && (
        <label className="mt-3 flex min-h-11 cursor-pointer items-center gap-3 rounded-xl bg-warning-soft px-3 py-2 text-sm text-on-warning-soft">
          <input
            type="checkbox"
            className="size-5 shrink-0"
            checked={confirmado}
            onChange={(e) => setConfirmado(e.target.checked)}
          />
          Treinei mesmo {formatarDuracao(maximoMin * 60)} seguidos
        </label>
      )}

      {sessao?.modoInicio === "Jogo" && (
        <AvisoCartaNoTreino grupoId={treino.grupoId} {...treinoPrevisto} />
      )}

      <label className="mt-4 block">
        <span className="mb-2 block text-sm font-semibold">
          Observações <span className="font-normal text-muted">(opcional)</span>
        </span>
        <textarea
          rows={3}
          maxLength={500}
          placeholder="Como foi o treino?"
          className="input-field h-auto resize-none py-3"
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
        />
      </label>

      {curto && (
        <p role="status" className="mt-4 text-sm text-muted">
          O treino precisa ter pelo menos 1 minuto. Já dá pra enviar daqui a{" "}
          {60 - segundos} s.
        </p>
      )}

      {erroPercurso && (
        <p role="alert" className="form-error mt-4">
          {erroPercurso}
        </p>
      )}

      {terminar.isError && (
        <p role="alert" className="form-error mt-4">
          {getErrorMessage(terminar.error)}
        </p>
      )}

      <button
        type="button"
        className="btn-primary mt-4 h-12 w-full"
        disabled={
          terminar.isPending || descarregando || precisaConfirmar || curto
        }
        onClick={() =>
          cartasQueFalham.length > 0 ? setConfirmandoCarta(true) : void enviar()
        }
      >
        {descarregando
          ? "Enviando o percurso…"
          : terminar.isPending
            ? "Enviando…"
            : "Enviar treino"}
      </button>
      <button
        type="button"
        className="btn-ghost mt-2 w-full"
        onClick={onCancelar}
      >
        Continuar treinando
      </button>

      <ConfirmDialog
        open={confirmandoCarta}
        title="Enviar e perder a carta?"
        message={`Esse treino não cumpre a carta de ${cartasQueFalham.join(" e ")}. Só o próximo treino depois do aceite vale: enviando, a carta falha e você fica com FADIGA.`}
        confirmLabel="Enviar mesmo assim"
        onConfirm={() => {
          setConfirmandoCarta(false);
          void enviar();
        }}
        onCancel={() => setConfirmandoCarta(false)}
      />
    </section>
  );
}

/** Depois de terminar: o Avatar comemora e a pessoa segue para a atividade ou para o início. */
function TreinoTerminado({
  atividade,
  grupoId,
}: {
  atividade: Atividade;
  grupoId: string;
}) {
  const sessao = useSessao().data;
  return (
    <div className="mx-auto flex max-w-md animate-fade-in flex-col items-center pt-12 text-center">
      <div className="relative grid h-40 place-items-end">
        <div
          aria-hidden="true"
          className="absolute inset-0 animate-burst bg-reward burst"
        />
        {sessao ? (
          <AvatarTreino
            usuarioId={sessao.id}
            tipo={atividade.tipo}
            comemorando
          />
        ) : (
          <CheckCircle2 aria-hidden="true" className="size-16" />
        )}
      </div>
      <h1 className="mt-6 text-4xl uppercase">Treino ao vivo, feito!</h1>
      <p className="mt-2 text-muted">
        {formatarDuracao(atividade.duracaoSegundos)} de{" "}
        {tipoAtividadeLabels[atividade.tipo].toLowerCase()}.{" "}
        {atividade.situacaoVerificacao === "Verificada"
          ? "Já está verificado."
          : "Agora o grupo confere."}
      </p>
      <p className="chip chip-game mt-4 whitespace-normal">
        Treino ao vivo rende bônus de Energia quando verificado ⚡
      </p>
      {atividade.percurso && (
        <GerarImagemPercurso
          atividadeId={atividade.id}
          inicio={atividade.startedAt}
          totalEvidencias={atividade.evidencias.length}
        />
      )}
      <div className="mt-8 w-full space-y-2">
        <Link
          to={`/atividades/${atividade.id}`}
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
