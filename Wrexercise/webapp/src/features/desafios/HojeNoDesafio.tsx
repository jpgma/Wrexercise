import { ProgressRing } from "@/components/ProgressRing";
import {
  calcularParteDoDia,
  formatarCaminhosDoDia,
  listarCaminhosDoDia,
  type MetaPorTipo,
} from "./metas";

/**
 * Quanto do dia de hoje já está preenchido, pela regra proporcional (decisão 0045), contando as
 * atividades pendentes, e os caminhos para fechar o resto, do mais curto ao mais longo.
 */
export function HojeNoDesafio({
  desafio,
  segundosPorTipo,
}: {
  desafio: { duracaoMinimaSegundos: number; metasPorTipo: MetaPorTipo[] };
  segundosPorTipo: { tipo: string; segundos: number }[];
}) {
  const parte = calcularParteDoDia(desafio, segundosPorTipo);
  const conta = parte >= 1;
  // Arredonda para baixo: 99,6% não aparece como 100% antes de o dia contar.
  const porcentagem = Math.min(100, Math.floor(parte * 100));

  return (
    <div className="mt-4 flex items-center gap-3 rounded-xl border border-border p-3">
      <ProgressRing
        label="Seu progresso hoje"
        valor={porcentagem}
        max={100}
        size={56}
        stroke={6}
      >
        <span className="font-score text-sm leading-none">{porcentagem}%</span>
      </ProgressRing>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold">
          {conta ? "Hoje já conta" : "Hoje"}
        </p>
        <p className="text-sm text-balance text-muted">
          {conta
            ? "Seu treino de hoje fecha o dia na meta."
            : formatarCaminhosDoDia(listarCaminhosDoDia(desafio, parte))}
        </p>
      </div>
    </div>
  );
}
