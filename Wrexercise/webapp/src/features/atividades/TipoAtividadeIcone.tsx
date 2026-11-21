import {
  Activity,
  BicepsFlexed,
  Bike,
  Dumbbell,
  Flower2,
  Footprints,
  HandFist,
  Trophy,
  Volleyball,
  Waves,
  type LucideIcon,
} from "lucide-react";
import { BolaFutebol, PessoaCorrendo } from "./iconesProprios";
import type { TipoAtividade } from "./types";

export const tipoAtividadeIcones: Record<TipoAtividade, LucideIcon> = {
  Musculacao: Dumbbell,
  Corrida: PessoaCorrendo,
  Caminhada: Footprints,
  Ciclismo: Bike,
  Natacao: Waves,
  Futebol: BolaFutebol,
  Volei: Volleyball,
  Luta: HandFist,
  Yoga: Flower2,
  Calistenia: BicepsFlexed,
  Esporte: Trophy,
  Outro: Activity,
};

export function TipoAtividadeIcone({
  tipo,
  className = "size-10",
}: {
  tipo: TipoAtividade;
  className?: string;
}) {
  const Icon = tipoAtividadeIcones[tipo];
  return (
    <div
      aria-hidden="true"
      className={`grid shrink-0 place-items-center rounded-xl bg-soft text-on-soft ${className}`}
    >
      <Icon className="size-5" />
    </div>
  );
}
