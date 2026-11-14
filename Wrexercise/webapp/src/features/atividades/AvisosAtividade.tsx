import {
  CalendarX,
  Clock,
  Copy,
  ImageOff,
  type LucideIcon,
  Pause,
} from "lucide-react";
import { Link } from "react-router";
import type { Aviso, TipoAviso } from "./types";

type Estilo = {
  curto: string;
  longo: string;
  Icon: LucideIcon;
  /** "Sem data" e "Vídeo parado" só informam: ficam neutros, sem o tom de alerta dos outros. */
  classe: string;
};

const estilos: Record<TipoAviso, Estilo> = {
  FotoForaDoPeriodo: {
    curto: "Foto de outro horário",
    longo: "A data de uma foto ou vídeo não bate com o horário do treino.",
    Icon: CalendarX,
    classe: "chip-warning",
  },
  FotoParecida: {
    curto: "Foto parecida",
    longo: "Uma foto ou vídeo parece com o de outra atividade.",
    Icon: Copy,
    classe: "chip-warning",
  },
  RegistroTardio: {
    curto: "Registrada depois",
    longo: "Registrada mais de 12 horas depois do fim do treino.",
    Icon: Clock,
    classe: "chip-warning",
  },
  FotoSemData: {
    curto: "Foto sem data",
    longo: "Não deu para conferir quando uma foto ou vídeo foi feito.",
    Icon: ImageOff,
    classe: "",
  },
  VideoParado: {
    curto: "Vídeo parado",
    longo: "A imagem de um vídeo quase não muda do começo ao fim.",
    Icon: Pause,
    classe: "",
  },
};

const ordem: TipoAviso[] = [
  "FotoForaDoPeriodo",
  "FotoParecida",
  "RegistroTardio",
  "FotoSemData",
  "VideoParado",
];

/** Selos curtos para os cartões do feed e da fila de avaliação. */
export function AvisosAtividadeResumo({ avisos }: { avisos: TipoAviso[] }) {
  if (avisos.length === 0) return null;
  return (
    <ul aria-label="Avisos" className="flex flex-wrap gap-1.5">
      {ordem
        .filter((tipo) => avisos.includes(tipo))
        .map((tipo) => {
          const { curto, Icon, classe } = estilos[tipo];
          return (
            <li key={tipo} className={`chip ${classe}`}>
              <Icon aria-hidden="true" className="size-3" />
              {curto}
            </li>
          );
        })}
    </ul>
  );
}

/** Lista completa na página da atividade, com o link para a foto parecida quando quem vê pode abri-la. */
export function AvisosAtividade({ avisos }: { avisos: Aviso[] }) {
  if (avisos.length === 0) return null;
  const vistos = new Set<string>();
  const unicos = avisos.filter((aviso) => {
    const chave = `${aviso.tipo}:${aviso.atividadeRelacionadaId ?? ""}`;
    if (vistos.has(chave)) return false;
    vistos.add(chave);
    return true;
  });

  return (
    <section aria-label="Avisos" className="space-y-2">
      <ul className="space-y-2">
        {unicos.map((aviso) => {
          const { longo, Icon } = estilos[aviso.tipo];
          return (
            <li
              key={`${aviso.tipo}:${aviso.atividadeRelacionadaId ?? ""}`}
              className="flex gap-2 rounded-xl bg-soft p-3 text-sm text-on-soft"
            >
              <Icon aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
              <span>
                {longo}{" "}
                {aviso.atividadeRelacionadaId && (
                  <Link
                    to={`/atividades/${aviso.atividadeRelacionadaId}`}
                    className="font-semibold text-link underline"
                  >
                    Ver a outra
                  </Link>
                )}
              </span>
            </li>
          );
        })}
      </ul>
      <p className="text-xs text-muted">
        Avisos automáticos, que podem errar. Com um deles, a atividade só é
        verificada se o grupo avaliar.
      </p>
    </section>
  );
}
