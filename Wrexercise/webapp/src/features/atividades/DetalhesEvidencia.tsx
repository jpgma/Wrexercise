import {
  CalendarClock,
  Camera,
  FileImage,
  History,
  Route,
  Video,
} from "lucide-react";
import type { LegendaGaleria } from "./GaleriaEvidencias";
import {
  descreverMomentoCaptura,
  formatarDataRelativa,
  origemDataCapturaLabel,
} from "./labels";
import { AtribuicaoMapa, MiniMapa } from "./MiniMapa";
import type { Evidencia } from "./types";

type Momento = Pick<
  LegendaGaleria,
  "startedAt" | "duracaoSegundos" | "situacaoVerificacao"
>;

/** "Foto tirada", "Vídeo gravado", "Print feito" ou "Imagem do percurso". */
function rotuloCaptura(evidencia: Evidencia) {
  if (evidencia.origemDataCaptura === "Percurso")
    return { texto: "Imagem do percurso", Icone: Route };
  if (evidencia.print) return { texto: "Print feito", Icone: FileImage };
  if (evidencia.tipo === "Video")
    return { texto: "Vídeo gravado", Icone: Video };
  return { texto: "Foto tirada", Icone: Camera };
}

/** A hora da captura em relação ao treino e de onde veio essa data, numa linha só. */
export function resumirCaptura(evidencia: Evidencia, momento: Momento) {
  if (!evidencia.capturadaEm || !evidencia.origemDataCaptura) return null;
  return {
    quando: formatarDataRelativa(evidencia.capturadaEm),
    relativo: descreverMomentoCaptura(
      evidencia.capturadaEm,
      momento.startedAt,
      momento.duracaoSegundos,
      momento.situacaoVerificacao === "EmAndamento",
    ),
    origem: origemDataCapturaLabel(evidencia.origemDataCaptura, evidencia.tipo),
  };
}

/**
 * Os detalhes do comprovante, logo abaixo dele (ADR 0080): quando foi feito, em relação ao treino, de onde
 * veio a data e, quando o autor escolheu mostrar (ADR 0061), o mini-mapa do local. Tudo é sinal, não prova.
 */
export function DetalhesEvidencia({
  evidencia,
  indice,
  momento,
}: {
  evidencia: Evidencia;
  indice: number;
  momento: Momento;
}) {
  const captura = resumirCaptura(evidencia, momento);
  const { texto, Icone } = rotuloCaptura(evidencia);
  const temLocal = evidencia.latitude !== null && evidencia.longitude !== null;

  return (
    <div className="flex items-center gap-3 border-b border-border px-4 py-3">
      {temLocal && (
        <MiniMapa
          base={`${evidencia.url}/mapa`}
          latitude={evidencia.latitude!}
          longitude={evidencia.longitude!}
          rotulo={`Ver no mapa o local do comprovante ${indice + 1}`}
        />
      )}
      <div className="min-w-0 flex-1">
        <p className="flex items-center gap-1.5 text-xs text-muted">
          <Icone aria-hidden="true" className="size-3.5" />
          {texto}
        </p>
        {captura ? (
          <>
            <p className="mt-0.5 font-semibold">{captura.quando}</p>
            <p className="text-sm text-muted">{captura.relativo}</p>
          </>
        ) : (
          <p className="mt-0.5 text-sm text-muted">Sem data de captura</p>
        )}
        <p className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1">
          {captura && (
            <span
              className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-2xs font-semibold ${
                evidencia.origemDataCaptura === "Arquivo"
                  ? "bg-warning-soft text-on-warning-soft"
                  : "bg-surface-muted text-foreground"
              }`}
              title={
                evidencia.origemDataCaptura === "Arquivo"
                  ? "O arquivo não trazia a data da captura: esta é a data do arquivo, que muda ao copiar."
                  : undefined
              }
            >
              <CalendarClock aria-hidden="true" className="size-3" />
              {captura.origem}
            </span>
          )}
          {evidencia.adicionadaDepois && (
            <span
              className="inline-flex items-center gap-1 rounded-full bg-surface-muted px-2 py-0.5 text-2xs font-semibold text-foreground"
              title="Mandada depois da avaliação: não contou para ela."
            >
              <History aria-hidden="true" className="size-3" />
              Adicionada depois
            </span>
          )}
          {temLocal && <AtribuicaoMapa />}
        </p>
      </div>
    </div>
  );
}
