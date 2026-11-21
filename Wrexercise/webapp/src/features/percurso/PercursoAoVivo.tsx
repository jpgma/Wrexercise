import { MapPin, Moon, Smartphone } from "lucide-react";
import { useState } from "react";
import type { TipoAtividade } from "@/features/atividades";
import { formatarDistancia, formatarMedia, usaVelocidade } from "./calculo";
import { MapaPercurso } from "./MapaPercurso";
import { entrarTelaCheia, ModoBolso } from "./ModoBolso";
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
 * estado do GPS. A tela fica acesa enquanto grava; o "Modo bolso" deixa a tela preta,
 * em tela cheia, só com os números em branco.
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
          onClick={() => {
            // A tela cheia precisa ser pedida no próprio toque.
            entrarTelaCheia();
            setBolso(true);
          }}
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
          estado={gravacao.estado}
          onSair={() => setBolso(false)}
        />
      )}
    </section>
  );
}
