import { AtribuicaoMapa, MiniMapa } from "./MiniMapa";

/** Onde o treino ao vivo começou, quando a pessoa escolheu mostrar o local ao grupo (ADR 0080). */
export function LocalTreino({
  atividadeId,
  latitude,
  longitude,
  aoVivo = false,
  className = "",
}: {
  atividadeId: string;
  latitude: number | null;
  longitude: number | null;
  aoVivo?: boolean;
  className?: string;
}) {
  if (latitude === null || longitude === null) return null;
  return (
    <div className={`flex items-center gap-3 ${className}`}>
      <MiniMapa
        base={`/api/atividades/${atividadeId}/mapa`}
        latitude={latitude}
        longitude={longitude}
        rotulo="Ver no mapa onde o treino começou"
        tamanho={72}
      />
      <div className="min-w-0">
        <p className="text-sm font-semibold">Local do treino</p>
        <p className="text-xs text-muted">
          {aoVivo
            ? "Onde você começou. O grupo vê este local."
            : "Onde o treino ao vivo começou"}
        </p>
        <AtribuicaoMapa />
      </div>
    </div>
  );
}
