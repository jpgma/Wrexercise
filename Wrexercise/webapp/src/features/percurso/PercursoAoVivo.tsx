import { MapPin, Moon, Smartphone } from "lucide-react";
import { useState } from "react";
import { formatarCronometro, type TipoAtividade } from "@/features/atividades";
import { formatarDistancia, formatarMedia, usaVelocidade } from "./calculo";
import { MapaPercurso } from "./MapaPercurso";
import type { GravacaoPercurso } from "./useGravacaoPercurso";
import { usePercurso } from "./usePercurso";

const textoEstado: Record<GravacaoPercurso["estado"], string | null> = {
  gravando: null,
  parado: "O percurso está sendo gravado em outro aparelho.",
  semSinal: "Procurando o sinal do GPS…",
  semPermissao:
    "Sem permissão de localização. Permita nas configurações do navegador para gravar o percurso.",
  indisponivel:
    "Este aparelho não tem GPS disponível. O treino continua sem percurso.",
  cheio:
    "O percurso chegou ao limite. O treino continua, mas o GPS parou de gravar.",
};

/**
 * O Percurso no cronômetro do treino ao vivo (ADR 0084): distância e ritmo até agora, o mapa com o trajeto e o
 * estado do GPS. A tela fica acesa enquanto grava; o "Modo bolso" escurece tudo e mostra só os números.
 */
export function PercursoAoVivo({
  atividadeId,
  tipo,
  segundos,
  gravacao,
}: {
  atividadeId: string;
  tipo: TipoAtividade;
  segundos: number;
  gravacao: GravacaoPercurso;
}) {
  const percurso = usePercurso(atividadeId, true);
  const [bolso, setBolso] = useState(false);
  const velocidade = segundos > 0 ? gravacao.distancia / segundos : null;
  const aviso = textoEstado[gravacao.estado];
  const pendentes = gravacao.pendentes.map(
    (p) => [p.latitude, p.longitude] as [number, number],
  );

  return (
    <section aria-labelledby="percurso-titulo" className="card mt-4 p-3">
      <div className="flex items-center justify-between gap-2">
        <h2
          id="percurso-titulo"
          className="flex items-center gap-1.5 text-sm font-semibold"
        >
          <MapPin aria-hidden="true" className="size-4" />
          Percurso
        </h2>
        {gravacao.estado === "gravando" && (
          <p className="chip chip-success">
            <span
              aria-hidden="true"
              className="size-2 rounded-full bg-success motion-safe:animate-pulse"
            />
            Gravando
          </p>
        )}
      </div>

      <dl className="mt-2 grid grid-cols-2 gap-2 text-center">
        <div className="rounded-xl bg-surface-muted py-2">
          <dt className="text-xs text-muted">Distância</dt>
          <dd className="font-score text-3xl tabular-nums">
            {formatarDistancia(gravacao.distancia)}
          </dd>
        </div>
        <div className="rounded-xl bg-surface-muted py-2">
          <dt className="text-xs text-muted">
            {usaVelocidade(tipo) ? "Velocidade média" : "Ritmo médio"}
          </dt>
          <dd className="font-score text-3xl tabular-nums">
            {formatarMedia(tipo, velocidade)}
          </dd>
        </div>
      </dl>

      {aviso && (
        <p role="status" className="mt-2 text-sm text-muted">
          {aviso}
        </p>
      )}
      {gravacao.estado === "parado" && (
        <button
          type="button"
          className="btn-ghost mt-1 w-full"
          onClick={gravacao.gravarAqui}
        >
          <Smartphone aria-hidden="true" className="size-4" />
          Gravar neste aparelho
        </button>
      )}

      {percurso.data && (
        <MapaPercurso
          percurso={percurso.data}
          extra={[pendentes]}
          rotulo={`Mapa do percurso até agora: ${formatarDistancia(gravacao.distancia)}`}
          className="mt-3"
        />
      )}
      <p className="mt-2 text-xs text-muted">
        O grupo vê o trajeto ao vivo, sem os primeiros e os últimos 200 m. Com a
        tela apagada, o GPS pausa e o trecho fica tracejado.
      </p>

      {gravacao.estado !== "parado" && (
        <button
          type="button"
          className="btn-ghost mt-2 w-full"
          onClick={() => setBolso(true)}
        >
          <Moon aria-hidden="true" className="size-4" />
          Modo bolso
        </button>
      )}

      {bolso && (
        <ModoBolso
          distancia={gravacao.distancia}
          segundos={segundos}
          media={formatarMedia(tipo, velocidade)}
          onSair={() => setBolso(false)}
        />
      )}
    </section>
  );
}

/**
 * A tela escura para levar o celular no bolso ou no suporte: só os números, com pouca luz. A tela continua
 * acesa (o GPS precisa dela). Sai com dois toques, para não sair sem querer.
 */
function ModoBolso({
  distancia,
  segundos,
  media,
  onSair,
}: {
  distancia: number;
  segundos: number;
  media: string;
  onSair: () => void;
}) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Modo bolso"
      className="fixed inset-0 z-50 flex flex-col items-center justify-center gap-6 bg-lightbox text-on-lightbox"
      onDoubleClick={onSair}
    >
      <p className="font-score text-7xl tabular-nums opacity-80">
        {formatarDistancia(distancia)}
      </p>
      <p className="font-score text-5xl tabular-nums opacity-70">
        {formatarCronometro(segundos)}
      </p>
      <p className="text-xl tabular-nums opacity-60">{media}</p>
      <button
        type="button"
        autoFocus
        className="mt-10 min-h-11 rounded-full px-6 text-sm opacity-60"
        onDoubleClick={onSair}
        // Pelo teclado (sem toque), Enter ou Espaço saem de uma vez.
        onClick={(e) => {
          if (e.detail === 0) onSair();
        }}
        onKeyDown={(e) => {
          if (e.key === "Escape") onSair();
        }}
      >
        Toque duas vezes para sair
      </button>
    </div>
  );
}
