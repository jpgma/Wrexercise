import { getAvisoCarta } from "./objetivoJogada";
import { useJogadas } from "./useCartas";

/**
 * No formulário de treino: diz se o que a pessoa está preenchendo cumpre cada carta que ela aceitou.
 * Usa as Jogadas do grupo, as mesmas da Mesa.
 */
export function AvisoCartaNoTreino({
  grupoId,
  tipo,
  startedAt,
  duracaoSegundos,
}: {
  grupoId: string;
  tipo: string | undefined;
  /** O valor do campo de data e hora, no horário do aparelho. */
  startedAt: string | undefined;
  duracaoSegundos: number;
}) {
  const jogadas = useJogadas(grupoId);
  const treino = {
    tipo,
    inicio: new Date(startedAt ?? ""),
    duracaoSegundos,
  };
  const avisos = (jogadas.data ?? [])
    .filter((j) => j.recebida && j.situacao === "Aceita")
    .map((j) => ({ id: j.id, aviso: getAvisoCarta(j, treino) }))
    .filter((a) => a.aviso !== null);
  if (avisos.length === 0) return null;

  return (
    <ul role="status" className="mt-3 space-y-1">
      {avisos.map(({ id, aviso }) => (
        <li
          key={id}
          className={`chip w-full justify-start whitespace-normal ${aviso?.cumpre ? "chip-game" : "chip-warning"}`}
        >
          {aviso?.texto}
        </li>
      ))}
    </ul>
  );
}
