import { useCallback, useEffect, useRef, useState } from "react";
import type { TipoAtividade } from "@/features/atividades";
import { ApiError } from "@/services/api";
import {
  apagarRascunho,
  lerRascunho,
  salvarRascunho,
} from "@/services/rascunhos";
import {
  deslocamentoMinimoMetros,
  distanciaLocal,
  haversine,
  intervaloMinimoMs,
  precisaoMaximaMetros,
  type PontoPercurso,
} from "./calculo";
import { enviarPontos } from "./usePercurso";

/**
 * A gravação do Percurso no aparelho (ADR 0084). Os pontos ficam no IndexedDB até o servidor confirmar: se o
 * app recarregar ou a rede cair, nada se perde. Um PWA não recebe o GPS com a tela apagada ou em segundo
 * plano: cada volta à tela começa um trecho novo (`segmento`), e a lacuna não conta distância.
 */

type Buffer = {
  proximoSeq: number;
  segmento: number;
  pendentes: PontoPercurso[];
  /** O último ponto que o servidor já tem: a estimativa local parte dele. */
  ultimoEnviado: PontoPercurso | null;
};

export type EstadoGravacao =
  /** Este aparelho não grava (o treino começou em outro aparelho). */
  | "parado"
  | "gravando"
  /** O GPS ainda não firmou, ou a precisão está ruim. */
  | "semSinal"
  | "semPermissao"
  | "indisponivel"
  /** O percurso chegou ao limite de pontos do servidor. */
  | "cheio";

const chave = (atividadeId: string) => `percurso:${atividadeId}`;
const intervaloEnvioMs = 30_000;
const intervaloSalvarMs = 5000;
const maxPorLote = 500;

/** Os buffers deste aparelho, mesmo sem IndexedDB (navegação privada): só a memória desta aba. */
const emMemoria = new Map<string, Buffer>();

/** Marca este aparelho como o que grava o percurso. Chamado ao começar o treino com "Gravar o percurso". */
export async function iniciarGravacaoPercurso(atividadeId: string) {
  const buffer: Buffer = {
    proximoSeq: 0,
    segmento: 0,
    pendentes: [],
    ultimoEnviado: null,
  };
  emMemoria.set(atividadeId, buffer);
  await salvarRascunho(chave(atividadeId), buffer);
}

/** Esquece o percurso deste aparelho: depois de terminar (e enviar tudo) ou de cancelar o treino. */
export async function descartarGravacaoPercurso(atividadeId: string) {
  emMemoria.delete(atividadeId);
  await apagarRascunho(chave(atividadeId));
}

async function lerBuffer(atividadeId: string): Promise<Buffer | null> {
  return (
    emMemoria.get(atividadeId) ??
    (await lerRascunho<Buffer>(chave(atividadeId)))
  );
}

export type GravacaoPercurso = {
  estado: EstadoGravacao;
  /** A distância do servidor mais a estimativa dos pontos que ainda não subiram, em metros. */
  distancia: number;
  /** Os pontos ainda não enviados, para desenhar a ponta do trajeto ao vivo. */
  pendentes: PontoPercurso[];
  /** Passa a gravar neste aparelho (o treino começou em outro). */
  gravarAqui: () => void;
  /** Para o GPS e envia tudo o que falta. Rejeita se não conseguir enviar. */
  finalizar: () => Promise<void>;
  /** Esquece o percurso neste aparelho: depois de terminar ou cancelar o treino. */
  descartar: () => void;
};

export function useGravacaoPercurso({
  atividadeId,
  tipo,
  percurso,
}: {
  atividadeId: string;
  tipo: TipoAtividade;
  percurso: { ultimoSeq: number; distanciaMetros: number } | null;
}): GravacaoPercurso {
  // Até o primeiro ponto chegar, o GPS está procurando o sinal.
  const [estado, setEstado] = useState<EstadoGravacao>("semSinal");
  const [ultimoEnviado, setUltimoEnviado] = useState<PontoPercurso | null>(
    null,
  );
  const [distanciaServidor, setDistanciaServidor] = useState(
    percurso?.distanciaMetros ?? 0,
  );
  const [pendentes, setPendentes] = useState<PontoPercurso[]>([]);
  const [aqui, setAqui] = useState<boolean | null>(null);

  const buffer = useRef<Buffer | null>(null);
  const watch = useRef<number | null>(null);
  const enviando = useRef<Promise<void> | null>(null);
  const salvoEm = useRef(0);
  const encerrado = useRef(false);
  const ultimoSeqServidor = percurso?.ultimoSeq ?? -1;
  const temPercurso = percurso !== null;

  const salvar = useCallback(
    (forcar = false) => {
      const atual = buffer.current;
      if (!atual) return;
      emMemoria.set(atividadeId, atual);
      if (forcar || Date.now() - salvoEm.current > intervaloSalvarMs) {
        salvoEm.current = Date.now();
        void salvarRascunho(chave(atividadeId), atual);
      }
    },
    [atividadeId],
  );

  const pararGps = useCallback(() => {
    if (watch.current !== null) {
      navigator.geolocation.clearWatch(watch.current);
      watch.current = null;
    }
  }, []);

  const enviar = useCallback(async () => {
    const atual = buffer.current;
    if (!atual || atual.pendentes.length === 0) return;
    const lote = atual.pendentes.slice(0, maxPorLote);
    try {
      const resposta = await enviarPontos(atividadeId, lote);
      const ultimo = lote.at(-1) ?? null;
      const ate = ultimo?.seq ?? -1;
      atual.pendentes = atual.pendentes.filter((p) => p.seq > ate);
      atual.ultimoEnviado = ultimo ?? atual.ultimoEnviado;
      setUltimoEnviado(atual.ultimoEnviado);
      setDistanciaServidor(resposta.distanciaMetros);
      setPendentes([...atual.pendentes]);
      salvar(true);
    } catch (error) {
      if (error instanceof ApiError && error.code === "PERCURSO_CHEIO") {
        encerrado.current = true;
        pararGps();
        setEstado("cheio");
        atual.pendentes = [];
        setPendentes([]);
        salvar(true);
        return;
      }
      // Sem rede ou o servidor ocupado: os pontos esperam no aparelho.
      throw error;
    }
  }, [atividadeId, pararGps, salvar]);

  /** Um envio por vez; os pontos que chegam no meio vão no próximo. */
  const enviarUmaVez = useCallback(() => {
    enviando.current ??= enviar().finally(() => {
      enviando.current = null;
    });
    return enviando.current;
  }, [enviar]);

  const aoReceber = useCallback(
    (posicao: GeolocationPosition) => {
      const atual = buffer.current;
      if (!atual || encerrado.current) return;
      const { latitude, longitude, accuracy, altitude } = posicao.coords;
      if (accuracy > precisaoMaximaMetros) {
        setEstado("semSinal");
        return;
      }
      setEstado("gravando");
      const anterior = atual.pendentes.at(-1) ?? atual.ultimoEnviado;
      const em = new Date(posicao.timestamp);
      if (
        anterior &&
        anterior.segmento === atual.segmento &&
        em.getTime() - Date.parse(anterior.em) < intervaloMinimoMs &&
        haversine(anterior.latitude, anterior.longitude, latitude, longitude) <
          deslocamentoMinimoMetros
      ) {
        return;
      }
      atual.pendentes.push({
        seq: atual.proximoSeq++,
        em: em.toISOString(),
        latitude,
        longitude,
        precisao: Math.round(accuracy * 10) / 10,
        altitude: altitude === null ? null : Math.round(altitude * 10) / 10,
        segmento: atual.segmento,
      });
      setPendentes([...atual.pendentes]);
      salvar();
    },
    [salvar],
  );

  const ligarGps = useCallback(() => {
    if (
      watch.current !== null ||
      encerrado.current ||
      !("geolocation" in navigator)
    ) {
      return;
    }
    watch.current = navigator.geolocation.watchPosition(
      aoReceber,
      (erro) => {
        if (erro.code === erro.PERMISSION_DENIED) {
          pararGps();
          setEstado("semPermissao");
        } else {
          setEstado("semSinal");
        }
      },
      { enableHighAccuracy: true, maximumAge: 0, timeout: 20_000 },
    );
  }, [aoReceber, pararGps]);

  // Descobre se este aparelho é o que grava, e continua de onde parou.
  useEffect(() => {
    if (!temPercurso) return;
    let ativo = true;
    void lerBuffer(atividadeId).then((lido) => {
      if (!ativo) return;
      if (!lido) {
        setAqui(false);
        return;
      }
      // Cada reabertura é um trecho novo: a lacuna não conta.
      buffer.current = {
        ...lido,
        proximoSeq: Math.max(lido.proximoSeq, ultimoSeqServidor + 1),
        segmento: lido.segmento + 1,
      };
      setPendentes([...lido.pendentes]);
      setUltimoEnviado(lido.ultimoEnviado);
      salvar(true);
      setAqui(true);
    });
    return () => {
      ativo = false;
    };
    // O último ponto do servidor só importa na primeira leitura.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [atividadeId, temPercurso, salvar]);

  // Grava com a tela à mostra; ao sair, guarda tudo; ao voltar, um trecho novo.
  useEffect(() => {
    if (!aqui) return;
    const aoMudar = () => {
      const atual = buffer.current;
      if (document.visibilityState === "visible") {
        if (atual && watch.current === null) {
          atual.segmento += 1;
          salvar(true);
        }
        ligarGps();
        void enviarUmaVez().catch(() => undefined);
      } else {
        pararGps();
        salvar(true);
      }
    };
    ligarGps();
    document.addEventListener("visibilitychange", aoMudar);
    window.addEventListener("pagehide", aoMudar);
    const envio = window.setInterval(
      () => void enviarUmaVez().catch(() => undefined),
      intervaloEnvioMs,
    );
    return () => {
      document.removeEventListener("visibilitychange", aoMudar);
      window.removeEventListener("pagehide", aoMudar);
      window.clearInterval(envio);
      pararGps();
      salvar(true);
    };
  }, [aqui, ligarGps, pararGps, enviarUmaVez, salvar]);

  const gravarAqui = useCallback(() => {
    buffer.current = {
      proximoSeq: ultimoSeqServidor + 1,
      segmento: 1000 + Math.floor(Math.random() * 1000),
      pendentes: [],
      ultimoEnviado: null,
    };
    salvar(true);
    setAqui(true);
  }, [salvar, ultimoSeqServidor]);

  const descartar = useCallback(() => {
    pararGps();
    encerrado.current = true;
    buffer.current = null;
    void descartarGravacaoPercurso(atividadeId);
  }, [atividadeId, pararGps]);

  const finalizar = useCallback(async () => {
    pararGps();
    encerrado.current = true;
    if (!buffer.current) return;
    // Lote a lote, até não sobrar nada.
    for (let i = 0; i < 50 && buffer.current.pendentes.length > 0; i++) {
      await enviarUmaVez();
    }
  }, [enviarUmaVez, pararGps]);

  const distancia =
    distanciaServidor + distanciaLocal(pendentes, tipo, ultimoEnviado);

  return {
    estado: !aqui
      ? "parado"
      : "geolocation" in navigator
        ? estado
        : "indisponivel",
    distancia,
    pendentes,
    gravarAqui,
    finalizar,
    descartar,
  };
}
