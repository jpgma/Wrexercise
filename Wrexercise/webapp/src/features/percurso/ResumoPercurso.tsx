import { Trophy, Users } from "lucide-react";
import { formatarDuracao, type TipoRecorde } from "@/features/atividades";
import {
  formatarDistancia,
  formatarMedia,
  formatarTempoParcial,
  formatarVelocidade,
  usaVelocidade,
} from "./calculo";
import type { Percurso } from "./usePercurso";

export const textoRecorde = (recorde: TipoRecorde, ciclismo: boolean) =>
  recorde === "Distancia"
    ? "Novo recorde de distância"
    : ciclismo
      ? "Novo recorde de velocidade"
      : "Novo recorde de ritmo";

function Dado({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="rounded-xl bg-surface-muted px-3 py-2">
      <dt className="text-xs text-muted">{rotulo}</dt>
      <dd className="font-score text-xl tabular-nums">{valor}</dd>
    </div>
  );
}

/**
 * Os números do Percurso (ADR 0084): distância, tempo, ritmo (ou velocidade, no Ciclismo), máxima, subida e
 * as parciais. A parcial mais rápida fica destacada. Nada disso conta pontos: é só para ver.
 */
export function ResumoPercurso({ percurso }: { percurso: Percurso }) {
  const { resumo, tipo } = percurso;
  const ciclismo = usaVelocidade(tipo);
  const parciais = resumo.parciais;
  // Velocidade de cada parcial; a última, mais curta, também compara.
  const velocidades = parciais.map((p) =>
    p.segundos > 0 ? p.metros / p.segundos : 0,
  );
  const melhor = Math.max(...velocidades, 0);
  const tamanhoParcial = ciclismo ? 5 : 1;

  return (
    <div className="space-y-3">
      {(resumo.recorde || percurso.parceiro) && (
        <div className="flex flex-wrap gap-2">
          {resumo.recorde && (
            <p className="chip chip-game">
              <Trophy aria-hidden="true" className="size-4" />
              {textoRecorde(resumo.recorde, ciclismo)}
            </p>
          )}
          {percurso.parceiro && (
            <p className="chip">
              <Users aria-hidden="true" className="size-4" />
              Treinaram juntos: {percurso.parceiro.nomeExibicao.split(" ")[0]}
            </p>
          )}
        </div>
      )}

      <dl className="grid grid-cols-2 gap-2">
        <Dado
          rotulo="Distância"
          valor={formatarDistancia(resumo.distanciaMetros)}
        />
        <Dado
          rotulo="Tempo"
          valor={formatarDuracao(Math.max(60, resumo.duracaoSegundos))}
        />
        <Dado
          rotulo={ciclismo ? "Velocidade média" : "Ritmo médio"}
          valor={formatarMedia(tipo, resumo.velocidadeMedia)}
        />
        <Dado
          rotulo="Velocidade máxima"
          valor={formatarVelocidade(resumo.velocidadeMaxima)}
        />
        {resumo.ganhoElevacaoMetros !== null && (
          <Dado
            rotulo="Subida (aprox.)"
            valor={`${resumo.ganhoElevacaoMetros} m`}
          />
        )}
      </dl>

      {parciais.length > 1 && (
        <section aria-labelledby={`parciais-${percurso.atividadeId}`}>
          <h3
            id={`parciais-${percurso.atividadeId}`}
            className="mb-1 text-xs font-semibold text-muted"
          >
            Parciais de {tamanhoParcial} km
          </h3>
          <ol className="space-y-1">
            {parciais.map((p, i) => {
              const velocidade = velocidades[i] ?? 0;
              const destaque = velocidade === melhor && velocidade > 0;
              return (
                <li key={i} className="flex items-center gap-2 text-xs">
                  <span className="w-12 shrink-0 tabular-nums text-muted">
                    {p.metros === tamanhoParcial * 1000
                      ? `${(i + 1) * tamanhoParcial} km`
                      : formatarDistancia(p.metros)}
                  </span>
                  <span
                    aria-hidden="true"
                    className="h-2.5 flex-1 overflow-hidden rounded-full bg-surface-muted"
                  >
                    <span
                      className={`block h-full rounded-full ${destaque ? "bg-reward" : "bg-progress"}`}
                      style={{
                        width: `${melhor > 0 ? Math.max(8, (velocidade / melhor) * 100) : 0}%`,
                      }}
                    />
                  </span>
                  <span className="w-20 shrink-0 text-right font-semibold tabular-nums">
                    {ciclismo
                      ? formatarVelocidade(velocidade)
                      : formatarTempoParcial(
                          p.metros > 0 ? (p.segundos * 1000) / p.metros : 0,
                        ) + " /km"}
                    {destaque && (
                      <span className="sr-only"> (a mais rápida)</span>
                    )}
                  </span>
                </li>
              );
            })}
          </ol>
        </section>
      )}
    </div>
  );
}
