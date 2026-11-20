import {
  getAvisoCarta,
  getAvisoCartaAoComecar,
  type AvisoCarta,
} from "./objetivoJogada";
import { primeiroNome } from "./textosCartas";
import { useJogadas } from "./useCartas";

/** O que o formulário de treino está preenchendo. */
type TreinoDoFormulario = {
  tipo: string | undefined;
  /** O valor do campo de data e hora, no horário do aparelho. */
  startedAt: string | undefined;
  duracaoSegundos: number;
  /** Começar um treino ao vivo: a duração ainda não existe (decisão 0078). */
  aoComecar?: boolean;
};

type AvisoJogada = { id: string; remetente: string; aviso: AvisoCarta };

/** O aviso de cada carta aceita sobre o treino do formulário. Usa as Jogadas do grupo, as mesmas da Mesa. */
function useAvisosCartas(
  grupoId: string,
  treino: TreinoDoFormulario,
  ativo: boolean,
): AvisoJogada[] {
  const jogadas = useJogadas(grupoId, ativo);
  const previsto = {
    tipo: treino.tipo,
    inicio: new Date(treino.startedAt ?? ""),
    duracaoSegundos: treino.duracaoSegundos,
  };
  return (jogadas.data ?? [])
    .filter((j) => j.recebida && j.situacao === "Aceita")
    .flatMap((j) => {
      const aviso = treino.aoComecar
        ? getAvisoCartaAoComecar(j, previsto)
        : getAvisoCarta(j, previsto);
      return aviso
        ? [{ id: j.id, remetente: primeiroNome(j.remetenteNome), aviso }]
        : [];
    });
}

/**
 * Os primeiros nomes de quem enviou as cartas que falham se este treino for postado: só o próximo
 * treino depois do aceite vale (decisão 0075). O formulário pede confirmação antes de postar.
 */
export function useCartasQueFalham(
  grupoId: string,
  treino: TreinoDoFormulario,
  ativo: boolean,
): string[] {
  return useAvisosCartas(grupoId, treino, ativo)
    .filter((a) => a.aviso.falha)
    .map((a) => a.remetente);
}

/** No formulário de treino: diz se o que a pessoa está preenchendo cumpre cada carta que ela aceitou. */
export function AvisoCartaNoTreino({
  grupoId,
  ...treino
}: TreinoDoFormulario & { grupoId: string }) {
  const avisos = useAvisosCartas(grupoId, treino, true);
  if (avisos.length === 0) return null;

  return (
    <ul role="status" className="mt-3 space-y-1">
      {avisos.map(({ id, aviso }) => (
        <li
          key={id}
          className={`chip w-full justify-start whitespace-normal ${aviso.cumpre ? "chip-game" : "chip-warning"}`}
        >
          {aviso.texto}
        </li>
      ))}
    </ul>
  );
}
