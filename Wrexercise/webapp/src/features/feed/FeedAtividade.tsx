import {
  Camera,
  Clock,
  MessageCircle,
  Play,
  Route,
  Trophy,
} from "lucide-react";
import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import { useSessao } from "@/features/autenticacao";
import {
  AvisosAtividadeResumo,
  formatarDataRelativa,
  formatarDuracao,
  GaleriaEvidencias,
  SituacaoBadge,
  tipoAtividadeLabels,
  useAtividade,
  useGaleria,
  useTorcer,
  type AtividadeResumo,
} from "@/features/atividades";
import {
  formatarDistancia,
  formatarMedia,
  textoRecorde,
} from "@/features/percurso";
import { tipoReacaoEmojis, tipoReacaoLabels } from "@/features/reacoes";
import { getErrorMessage } from "@/services/api";

function Contagem({
  tipo,
  total,
}: {
  tipo: keyof typeof tipoReacaoEmojis;
  total: number;
}) {
  return (
    <span className="inline-flex items-center gap-1 text-xs text-muted tabular-nums">
      <span aria-hidden="true">{tipoReacaoEmojis[tipo]}</span>
      <span className="sr-only">{tipoReacaoLabels[tipo]}:</span>
      {total}
    </span>
  );
}

/** O treino ao vivo no feed (ADR 0078): quem vê pode torcer, uma vez a cada 10 minutos. */
function TorcerNoFeed({
  atividadeId,
  grupoId,
}: {
  atividadeId: string;
  grupoId: string;
}) {
  const torcer = useTorcer(grupoId);
  return (
    <div className="border-t border-border px-4 py-2">
      {torcer.isSuccess ? (
        <p
          role="status"
          className="flex min-h-11 items-center text-sm text-muted"
        >
          <span aria-hidden="true">📣</span>&nbsp;Torcida enviada!
        </p>
      ) : (
        <button
          type="button"
          className="btn-ghost min-h-11 w-full"
          disabled={torcer.isPending}
          onClick={() => torcer.mutate(atividadeId)}
        >
          <span aria-hidden="true">📣</span> Torcer
        </button>
      )}
      {torcer.isError && (
        <p role="alert" className="input-error">
          {getErrorMessage(torcer.error)}
        </p>
      )}
    </div>
  );
}

export function FeedAtividade({ atividade }: { atividade: AtividadeResumo }) {
  const {
    id,
    usuarioId,
    nomeExibicao,
    tipo,
    duracaoSegundos,
    startedAt,
    situacaoVerificacao,
    totalEvidencias,
    descricao,
    kudos,
    suspeito,
    rejeitar,
    miniaturaUrl,
    miniaturaDuracaoMs,
    comentarios,
    avisos,
    percurso,
  } = atividade;
  const galeria = useGaleria(id);
  const aoVivo = situacaoVerificacao === "EmAndamento";
  const souEu = useSessao().data?.id === usuarioId;
  // As evidências completas só são buscadas quando a galeria é aberta.
  const detalhe = useAtividade(id, galeria.aberta);

  return (
    // A miniatura abre a galeria; o resto do cartão leva à atividade. Um botão
    // não pode ficar dentro do link, então ela é posicionada por cima dele.
    <div className="card relative transition-transform duration-120 has-[>a:active]:scale-[0.98]">
      <Link to={`/atividades/${id}`} className="block rounded-2xl p-4">
        <div className="flex items-start gap-3">
          <Avatar nome={nomeExibicao} id={usuarioId} />
          <div className="min-w-0 flex-1">
            <div className="flex items-center justify-between gap-2">
              <p className="truncate text-sm font-semibold">{nomeExibicao}</p>
              <span className="shrink-0 text-xs text-muted">
                {formatarDataRelativa(startedAt)}
              </span>
            </div>
            <p className="mt-0.5 flex flex-wrap items-center gap-x-1.5 text-sm text-muted">
              {tipoAtividadeLabels[tipo]} ·
              <Clock aria-hidden="true" className="size-3.5" />
              {/* O início já está no canto do cartão; uma contagem de minutos aqui ficaria parada. */}
              {aoVivo ? "treinando agora" : formatarDuracao(duracaoSegundos)}
            </p>
            {percurso && percurso.distanciaMetros > 0 && (
              <p className="mt-1.5 flex flex-wrap items-center gap-1.5 text-xs">
                <span className="chip">
                  <Route aria-hidden="true" className="size-3.5" />
                  {formatarDistancia(percurso.distanciaMetros)}
                  {!aoVivo &&
                    percurso.velocidadeMedia !== null &&
                    ` · ${formatarMedia(tipo, percurso.velocidadeMedia)}`}
                </span>
                {percurso.recorde && (
                  <span className="chip chip-game">
                    <Trophy aria-hidden="true" className="size-3.5" />
                    {textoRecorde(percurso.recorde, tipo === "Ciclismo")}
                  </span>
                )}
              </p>
            )}
            {descricao && (
              <p className="mt-2 line-clamp-2 text-sm break-words">
                {descricao}
              </p>
            )}
            {avisos.length > 0 && (
              <div className="mt-2">
                <AvisosAtividadeResumo avisos={avisos} />
              </div>
            )}
            <div className="mt-3 flex items-center justify-between gap-2">
              <SituacaoBadge situacao={situacaoVerificacao} />
              <div className="flex items-center gap-3">
                {!aoVivo && <Contagem tipo="Kudos" total={kudos} />}
                {suspeito > 0 && <Contagem tipo="Suspeito" total={suspeito} />}
                {rejeitar > 0 && <Contagem tipo="Rejeitar" total={rejeitar} />}
                {comentarios > 0 && (
                  <span className="flex items-center gap-1 text-xs text-muted tabular-nums">
                    <MessageCircle aria-hidden="true" className="size-3.5" />
                    {comentarios}
                    <span className="sr-only">
                      {comentarios === 1 ? "comentário" : "comentários"}
                    </span>
                  </span>
                )}
                {totalEvidencias > (miniaturaUrl ? 1 : 0) && (
                  <span className="flex items-center gap-1 text-xs text-muted">
                    <Camera aria-hidden="true" className="size-3.5" />
                    {totalEvidencias}
                    <span className="sr-only">
                      {totalEvidencias === 1 ? "comprovante" : "comprovantes"}
                    </span>
                  </span>
                )}
              </div>
            </div>
          </div>
          {miniaturaUrl && (
            <div aria-hidden="true" className="size-16 shrink-0" />
          )}
        </div>
      </Link>
      {aoVivo && !souEu && (
        <TorcerNoFeed atividadeId={id} grupoId={atividade.grupoId} />
      )}
      {miniaturaUrl && (
        <>
          <button
            type="button"
            onClick={() => galeria.abrir(0)}
            aria-label={
              miniaturaDuracaoMs === null
                ? "Ver comprovantes em tela cheia"
                : "Assistir ao vídeo em tela cheia"
            }
            className="absolute top-4 right-4 size-16 cursor-zoom-in overflow-hidden rounded-xl tap"
          >
            <img
              src={miniaturaUrl}
              alt=""
              loading="lazy"
              className="size-full object-cover"
            />
            {miniaturaDuracaoMs !== null && (
              <span className="absolute inset-x-0 bottom-0 flex items-center justify-center gap-0.5 bg-lightbox/60 py-0.5 text-[0.6875rem] font-semibold text-on-lightbox tabular-nums">
                <Play aria-hidden="true" className="size-2.5 fill-current" />
                {Math.floor(Math.round(miniaturaDuracaoMs / 1000) / 60)}:
                {String(Math.round(miniaturaDuracaoMs / 1000) % 60).padStart(
                  2,
                  "0",
                )}
              </span>
            )}
          </button>
          <GaleriaEvidencias
            aberta={galeria.aberta}
            indiceInicial={galeria.indiceInicial}
            evidencias={detalhe.data?.evidencias}
            erro={detalhe.isError ? getErrorMessage(detalhe.error) : undefined}
            legenda={atividade}
            onFechar={galeria.fechar}
          />
        </>
      )}
    </div>
  );
}
