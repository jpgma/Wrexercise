import { Camera, Clock, MessageCircle } from "lucide-react";
import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import {
  formatarDataRelativa,
  formatarDuracao,
  GaleriaEvidencias,
  SituacaoBadge,
  tipoAtividadeLabels,
  useAtividade,
  useGaleria,
  type AtividadeResumo,
} from "@/features/atividades";
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
    comentarios,
  } = atividade;
  const galeria = useGaleria(id);
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
              {formatarDuracao(duracaoSegundos)}
            </p>
            {descricao && (
              <p className="mt-2 line-clamp-2 text-sm break-words">
                {descricao}
              </p>
            )}
            <div className="mt-3 flex items-center justify-between gap-2">
              <SituacaoBadge situacao={situacaoVerificacao} />
              <div className="flex items-center gap-3">
                <Contagem tipo="Kudos" total={kudos} />
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
      {miniaturaUrl && (
        <>
          <button
            type="button"
            onClick={() => galeria.abrir(0)}
            aria-label="Ver comprovantes em tela cheia"
            className="absolute top-4 right-4 size-16 cursor-zoom-in overflow-hidden rounded-xl tap"
          >
            <img
              src={miniaturaUrl}
              alt=""
              loading="lazy"
              className="size-full object-cover"
            />
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
