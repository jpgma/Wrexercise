type Coluna = {
  chave: string | number;
  rotulo: string;
  valor: number;
  provisorio: number;
  /** Semana anterior à entrada da pessoa no grupo: não conta contra ela. */
  naoParticipou?: boolean;
};

type HistoryChartProps = {
  titulo: string;
  /** Sem meta não há linha: o Perfil cruza grupos com metas diferentes. */
  meta?: number;
  colunas: Coluna[];
};

const alturaEmPx = 112;

/** Barras simples em CSS: uma biblioteca de gráficos pesaria mais que o app inteiro. */
export function HistoryChart({ titulo, meta, colunas }: HistoryChartProps) {
  const topo =
    Math.max(meta ?? 0, ...colunas.map(({ provisorio }) => provisorio)) + 1;

  return (
    <div className="card p-4">
      <p className="text-sm font-semibold">{titulo}</p>
      <p className="text-xs text-muted">
        Dias por semana{meta !== undefined && " · linha = meta"}
      </p>
      <div
        className="relative mt-4 flex items-end gap-2"
        style={{ height: alturaEmPx }}
      >
        {meta !== undefined && (
          <div
            aria-hidden="true"
            className="absolute inset-x-0 border-t border-dashed border-primary/50"
            style={{ bottom: `${(meta / topo) * 100}%` }}
          />
        )}
        {colunas.map(({ chave, rotulo, valor, provisorio, naoParticipou }) => (
          <div
            key={chave}
            role="img"
            aria-label={
              naoParticipou
                ? `${rotulo}: não participou`
                : `${rotulo}: ${valor} verificadas de ${provisorio} registradas`
            }
            className="flex flex-1 flex-col justify-end gap-px"
          >
            {naoParticipou && (
              <span
                aria-hidden="true"
                className="text-center text-xs text-muted"
              >
                –
              </span>
            )}
            {provisorio > valor && (
              <div
                className="w-full rounded-t-md bg-primary/35"
                style={{
                  height: `${((provisorio - valor) / topo) * alturaEmPx}px`,
                }}
              />
            )}
            {valor > 0 && (
              <div
                className={`w-full rounded-md ${meta === undefined || valor >= meta ? "bg-primary" : "bg-muted/40"}`}
                style={{ height: `${(valor / topo) * alturaEmPx}px` }}
              />
            )}
          </div>
        ))}
      </div>
      <div className="mt-1 flex gap-2">
        {colunas.map(({ chave, rotulo }) => (
          <span
            key={chave}
            className="min-w-0 flex-1 truncate text-center text-[10px] text-muted"
          >
            {rotulo}
          </span>
        ))}
      </div>
      {colunas.some(({ naoParticipou }) => naoParticipou) && (
        <p className="mt-2 text-2xs text-muted">– não participou</p>
      )}
    </div>
  );
}
