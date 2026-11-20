import type { LocalCaptura } from "./CameraEvidencia";

function pedirPosicao(opcoes: PositionOptions) {
  return new Promise<LocalCaptura | null>((resolver) => {
    navigator.geolocation.getCurrentPosition(
      ({ coords }) =>
        resolver({ latitude: coords.latitude, longitude: coords.longitude }),
      () => resolver(null),
      opcoes,
    );
  });
}

/**
 * A posição do aparelho para o local do treino ao vivo (ADR 0080). Tenta o GPS por até `esperaMs` e, sem
 * ele, a posição aproximada. Sem permissão, sem suporte ou sem sinal: `null`, e o treino começa sem local.
 */
export async function obterLocalAtual(
  esperaMs = 6000,
): Promise<LocalCaptura | null> {
  if (!("geolocation" in navigator)) return null;
  const precisa = await pedirPosicao({
    enableHighAccuracy: true,
    maximumAge: 60_000,
    timeout: esperaMs,
  });
  return (
    precisa ??
    pedirPosicao({
      enableHighAccuracy: false,
      maximumAge: 5 * 60_000,
      timeout: 2000,
    })
  );
}
