import { useFolhaAvatar } from "@/components/folhaAvatar";
import type { NomeAnimacao } from "./animacoes";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMovimentoReduzido } from "./useMovimentoReduzido";

/**
 * A animação de quem treina ao vivo, pelo tipo do treino (ADR 0078, 0082): a do esporte, sozinho e com
 * o objeto genérico. Na praça, quem tem o Enfeite do tipo no chão vai até ele e o usa. "Outro
 * esporte" e `Outro` fazem o `treinando`.
 */
const animacaoDoTipo: Record<string, NomeAnimacao> = {
  Musculacao: "musculacao",
  Corrida: "correr",
  Caminhada: "andar",
  Ciclismo: "ciclismo",
  Natacao: "natacao",
  Futebol: "futebol",
  Volei: "volei",
  Luta: "luta",
  Yoga: "yoga",
  Calistenia: "calistenia",
};

export function getAnimacaoDoTreino(tipo: string): NomeAnimacao {
  return animacaoDoTipo[tipo] ?? "treinando";
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
