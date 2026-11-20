import { useFolhaAvatar } from "@/components/folhaAvatar";
import type { NomeAnimacao } from "./animacoes";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMovimentoReduzido } from "./useMovimentoReduzido";

/** A animação de quem treina ao vivo, pelo tipo do treino (ADR 0078). Igual à da praça. */
export function getAnimacaoDoTreino(tipo: string): NomeAnimacao {
  if (tipo === "Corrida") return "correr";
  if (tipo === "Caminhada") return "andar";
  return "treinando";
}

/**
 * O Avatar de quem treina, no cronômetro do treino ao vivo: treina enquanto o tempo corre e comemora a
 * cada marco. Com movimento reduzido, fica num quadro só.
 */
export function AvatarTreino({
  usuarioId,
  tipo,
  comemorando,
  escala = 3,
}: {
  usuarioId: string;
  tipo: string;
  comemorando: boolean;
  escala?: number;
}) {
  const folha = useFolhaAvatar(usuarioId);
  const reduzido = useMovimentoReduzido();
  const animacao: NomeAnimacao = comemorando
    ? "comemorando"
    : getAnimacaoDoTreino(tipo);
  return (
    <SpriteAvatar
      key={animacao}
      folha={folha}
      animacao={animacao}
      direcao="sul"
      escala={escala}
      estatico={reduzido}
    />
  );
}
