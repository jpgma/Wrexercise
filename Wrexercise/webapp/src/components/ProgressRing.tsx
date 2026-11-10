import type { ReactNode } from "react";

type ProgressRingProps = {
  label: string;
  valor: number;
  /** Definitivas somadas às que ainda aguardam verificação. */
  provisorio?: number;
  max: number;
  size?: number;
  stroke?: number;
  /** Um segmento por unidade de `max`, separados por vãos. */
  segmentado?: boolean;
  children?: ReactNode;
};

/** Um arco por unidade: definitivo sólido, provisório claro, o resto vazio. Contorno de adesivo em cada um. */
function Segmentos({
  circulo,
  circunferencia,
  stroke,
  max,
  valor,
  provisorio,
  completo,
}: {
  circulo: { cx: number; cy: number; r: number; fill: string };
  circunferencia: number;
  stroke: number;
  max: number;
  valor: number;
  provisorio: number;
  completo: boolean;
}) {
  const passo = circunferencia / max;
  // O vão precisa ser maior que o contorno, que avança 1,5px em cada ponta.
  const vao = max > 1 ? Math.max(stroke + 2, passo * 0.08) : 0;
  const arco = passo - vao;
  return Array.from({ length: max }, (_, i) => {
    const cor = completo
      ? "var(--color-progress)"
      : i < valor || i < provisorio
        ? "var(--color-primary)"
        : "var(--color-surface-muted)";
    const opacidade = !completo && i >= valor && i < provisorio ? 0.35 : 1;
    return (
      <g key={i}>
        <circle
          {...circulo}
          stroke="var(--color-outline)"
          strokeWidth={stroke + 3}
          strokeDasharray={`${arco + 3} ${circunferencia - arco - 3}`}
          strokeDashoffset={-(i * passo) + 1.5}
        />
        {/* Fundo opaco sob o provisório, para a opacidade não mostrar o contorno. */}
        <circle
          {...circulo}
          stroke="var(--color-surface-muted)"
          strokeWidth={stroke}
          strokeDasharray={`${arco} ${circunferencia - arco}`}
          strokeDashoffset={-(i * passo)}
        />
        <circle
          {...circulo}
          stroke={cor}
          strokeOpacity={opacidade}
          strokeWidth={stroke}
          strokeDasharray={`${arco} ${circunferencia - arco}`}
          strokeDashoffset={-(i * passo)}
          style={{ transition: "stroke .4s ease" }}
        />
      </g>
    );
  });
}

/** Arco sólido para o definitivo; o provisório aparece em tom mais claro logo depois. Lime ao completar. */
export function ProgressRing({
  label,
  valor,
  provisorio = valor,
  max,
  size = 132,
  stroke = 12,
  segmentado = false,
  children,
}: ProgressRingProps) {
  // Reserva 2px de cada lado para o contorno.
  const raio = (size - stroke - 4) / 2;
  const circunferencia = 2 * Math.PI * raio;
  const fracao = (n: number) => (max > 0 ? Math.min(n / max, 1) : 0);
  const definitivo = fracao(valor);
  const comProvisorio = fracao(provisorio);
  const circulo = { cx: size / 2, cy: size / 2, r: raio, fill: "none" };
  const completo = definitivo >= 1;

  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-valuenow={Math.min(valor, max)}
      className="relative shrink-0"
      style={{ width: size, height: size }}
    >
      {completo && (
        // Explosão de quadrinho atrás do anel, uma vez só ao completar.
        <div
          aria-hidden="true"
          className="absolute -inset-2 animate-burst bg-reward burst"
        />
      )}
      <svg
        width={size}
        height={size}
        className={`relative -rotate-90 ${completo ? "animate-pop" : ""}`}
        aria-hidden="true"
      >
        {segmentado && max > 0 ? (
          <Segmentos
            circulo={circulo}
            circunferencia={circunferencia}
            stroke={stroke}
            max={max}
            valor={valor}
            provisorio={provisorio}
            completo={completo}
          />
        ) : (
          <>
            {/* Contornos do anel: dão o traço de adesivo e separam o coral do fundo claro. */}
            <circle
              cx={size / 2}
              cy={size / 2}
              r={raio}
              fill="none"
              stroke="var(--color-outline)"
              strokeWidth={stroke + 4}
            />
            <circle
              {...circulo}
              stroke="var(--color-surface-muted)"
              strokeWidth={stroke}
            />
            {comProvisorio > definitivo && (
              <circle
                {...circulo}
                stroke="var(--color-primary)"
                strokeOpacity={0.35}
                strokeWidth={stroke}
                strokeLinecap="butt"
                strokeDasharray={circunferencia}
                strokeDashoffset={circunferencia * (1 - comProvisorio)}
              />
            )}
            {definitivo > 0 && (
              <circle
                {...circulo}
                stroke={
                  completo ? "var(--color-progress)" : "var(--color-primary)"
                }
                strokeWidth={stroke}
                strokeLinecap="butt"
                strokeDasharray={circunferencia}
                strokeDashoffset={circunferencia * (1 - definitivo)}
                style={{ transition: "stroke-dashoffset .8s ease" }}
              />
            )}
          </>
        )}
      </svg>
      <div className="absolute inset-0 grid place-items-center text-center">
        {children}
      </div>
    </div>
  );
}
