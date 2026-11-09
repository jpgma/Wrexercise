import { Link } from "react-router";
import { Avatar } from "@/components/Avatar";
import { Segments } from "@/components/Segments";

/** "Meta batida" só com dias verificados: as pendentes ainda podem ser rejeitadas. */
export function SituacaoParticipante({
  verificadas,
  meta,
}: {
  verificadas: number;
  meta: number;
}) {
  if (verificadas >= meta) {
    return <span className="badge badge-progress">Meta batida</span>;
  }
  if (verificadas === meta - 1) {
    return <span className="chip chip-soft">Quase lá</span>;
  }
  if (verificadas === 0) return <span className="chip">Começando</span>;
  return <span className="chip chip-warning">Em andamento</span>;
}

export function LinhaParticipante({
  usuarioId,
  nomeExibicao,
  verificadas,
  provisorias,
  meta,
  voce,
  contaNestaSemana = true,
  grupoId,
}: {
  usuarioId: string;
  nomeExibicao: string;
  verificadas: number;
  provisorias: number;
  meta: number;
  voce: boolean;
  contaNestaSemana?: boolean;
  /** Com o grupo, a linha leva à página do membro. */
  grupoId?: string;
}) {
  const primeiroNome = nomeExibicao.trim().split(/\s+/)[0];
  const conteudo = (
    <>
      <Avatar nome={nomeExibicao} id={usuarioId} size={38} />
      <div className="min-w-0 flex-1">
        <div className="mb-1.5 flex items-center justify-between gap-2">
          <p className="truncate text-sm font-semibold">
            {primeiroNome}
            {voce && <span className="font-normal text-muted"> (você)</span>}
          </p>
          {contaNestaSemana && (
            <span className="font-score text-base">
              {Math.min(verificadas, meta)}/{meta}
            </span>
          )}
        </div>
        {contaNestaSemana ? (
          <Segments
            label={`Progresso de ${nomeExibicao}`}
            valor={verificadas}
            provisorio={provisorias}
            max={meta}
          />
        ) : (
          <p className="text-2xs text-muted">
            Entrou nesta semana: conta a partir da próxima
          </p>
        )}
        {provisorias > verificadas && (
          <p className="mt-1 text-2xs text-muted">
            +{provisorias - verificadas} aguardando verificação
          </p>
        )}
      </div>
      {contaNestaSemana && (
        <div className="hidden w-24 justify-end sm:flex">
          <SituacaoParticipante verificadas={verificadas} meta={meta} />
        </div>
      )}
    </>
  );

  return grupoId ? (
    <Link
      to={`/grupos/${grupoId}/membros/${usuarioId}`}
      className="flex items-center gap-3 rounded-xl px-2 py-2.5 tap hover:bg-surface-muted"
    >
      {conteudo}
    </Link>
  ) : (
    <div className="flex items-center gap-3 rounded-xl px-2 py-2.5">
      {conteudo}
    </div>
  );
}
