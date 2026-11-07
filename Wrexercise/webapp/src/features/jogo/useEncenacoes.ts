import { useCallback, useEffect, useRef, useState } from "react";
import { getDuracao, type NomeAnimacao } from "./animacoes";
import type { Jogada } from "./useCartas";

export type Ponto = { x: number; y: number };

/** O que a encenação muda num avatar: a animação, o deslocamento e para onde ele olha. */
export type Atuacao = {
  animacao: NomeAnimacao;
  dx: number;
  dy: number;
  /** `true` olha para a esquerda. Sem valor, vale o lado da elipse. */
  espelhar?: boolean;
  /** Duração do deslocamento até `dx`/`dy`, em ms. */
  passoMs: number;
};

/** A carta em cena: voando entre dois pontos, saindo pelo lado ou caindo no lugar. */
export type CartaEmCena = {
  jogada: Jogada;
  efeito: "voo" | "sai" | "aceita";
  de: Ponto;
  para: Ponto;
};

/** No máximo 3 encenações por visita (docs/gamificacao.md §3.3). */
const MaxPorVisita = 3;
const PassoMs = 900;
const VooMs = 600;

type Etapa = { em: number; executar: () => void };

/**
 * Toca, uma de cada vez, as encenações das Jogadas que quem vê a cena ainda não viu:
 * a chegada de uma carta (para o destinatário) e a resposta (para o remetente).
 * No fim, ou ao pular, marca todas como vistas.
 */
export function useEncenacoes({
  jogadas,
  posicoes,
  centro,
  reduzido,
  onVistas,
}: {
  jogadas: Jogada[] | undefined;
  posicoes: Map<string, Ponto>;
  centro: Ponto;
  reduzido: boolean;
  onVistas: (ids: string[]) => void;
}) {
  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(new Map());
  const [carta, setCarta] = useState<CartaEmCena | null>(null);
  const [ativa, setAtiva] = useState(false);
  const temporizadores = useRef<number[]>([]);
  const pendentes = useRef<string[]>([]);
  const avisar = useRef(onVistas);

  useEffect(() => {
    avisar.current = onVistas;
  }, [onVistas]);

  const encerrar = useCallback(() => {
    temporizadores.current.forEach((t) => window.clearTimeout(t));
    temporizadores.current = [];
    setAtuacoes(new Map());
    setCarta(null);
    setAtiva(false);
    if (pendentes.current.length > 0) {
      avisar.current(pendentes.current);
      pendentes.current = [];
    }
  }, []);

  const chave = (jogadas ?? [])
    .filter((j) => j.nova)
    .map((j) => j.id)
    .join(",");

  useEffect(() => {
    if (!chave) return;
    const fila = (jogadas ?? []).filter((j) => j.nova);
    pendentes.current = fila.map((j) => j.id);

    // Sem movimento, ou com a página escondida, as cartas aparecem direto no estado final.
    const avisarJa = () => {
      avisar.current(pendentes.current);
      pendentes.current = [];
    };
    if (reduzido || document.visibilityState !== "visible") {
      avisarJa();
      return;
    }

    const etapas: Etapa[] = [];
    let tempo = 0;
    const atuar = (usuarioId: string, atuacao: Atuacao | null) =>
      setAtuacoes((atual) => {
        const proxima = new Map(atual);
        if (atuacao) proxima.set(usuarioId, atuacao);
        else proxima.delete(usuarioId);
        return proxima;
      });

    for (const jogada of [...fila]
      .sort((a, b) => a.enviadaEm.localeCompare(b.enviadaEm))
      .slice(0, MaxPorVisita)) {
      const remetente = posicoes.get(jogada.remetenteId);
      const destinatario = posicoes.get(jogada.destinatarioId);
      if (!remetente || !destinatario) continue;
      tempo = jogada.recebida
        ? encenarChegada(etapas, tempo, jogada, remetente, destinatario)
        : encenarResposta(etapas, tempo, jogada, destinatario);
      tempo += 400;
    }

    function encenarChegada(
      lista: Etapa[],
      inicio: number,
      jogada: Jogada,
      de: Ponto,
      para: Ponto,
    ): number {
      const id = jogada.remetenteId;
      const ida = { dx: centro.x - de.x, dy: centro.y - de.y };
      const olharPara = (alvo: Ponto) =>
        Math.abs(alvo.x - centro.x) < 2 ? undefined : alvo.x < centro.x;
      let t = inicio;
      lista.push({
        em: t,
        executar: () =>
          atuar(id, {
            animacao: "andar",
            ...ida,
            espelhar: centro.x < de.x,
            passoMs: PassoMs,
          }),
      });
      t += PassoMs;
      lista.push({
        em: t,
        executar: () =>
          atuar(id, {
            animacao: "arremessar",
            ...ida,
            espelhar: olharPara(para),
            passoMs: 0,
          }),
      });
      // A carta sai da mão no terceiro quadro do arremesso.
      t += 2 * 80;
      lista.push({
        em: t,
        executar: () => setCarta({ jogada, efeito: "voo", de: centro, para }),
      });
      t += VooMs;
      lista.push({
        em: t,
        executar: () => {
          setCarta(null);
          atuar(jogada.destinatarioId, {
            animacao: "receber",
            dx: 0,
            dy: 0,
            passoMs: 0,
          });
        },
      });
      t += getDuracao("receber");
      lista.push({
        em: t,
        executar: () => {
          atuar(jogada.destinatarioId, null);
          atuar(id, {
            animacao: "andar",
            dx: 0,
            dy: 0,
            espelhar: de.x < centro.x,
            passoMs: PassoMs,
          });
        },
      });
      t += PassoMs;
      lista.push({ em: t, executar: () => atuar(id, null) });
      return t;
    }

    function encenarResposta(
      lista: Etapa[],
      inicio: number,
      jogada: Jogada,
      destinatario: Ponto,
    ): number {
      const aceitou = jogada.situacao !== "Recusada";
      const animacao: NomeAnimacao = aceitou ? "aceitar" : "recusar";
      let t = inicio;
      lista.push({
        em: t,
        executar: () => {
          atuar(jogada.destinatarioId, {
            animacao,
            dx: 0,
            dy: 0,
            passoMs: 0,
          });
          setCarta({
            jogada,
            efeito: aceitou ? "aceita" : "sai",
            de: destinatario,
            para: destinatario,
          });
        },
      });
      t += Math.max(getDuracao(animacao), 900);
      lista.push({
        em: t,
        executar: () => {
          atuar(jogada.destinatarioId, null);
          setCarta(null);
        },
      });
      return t;
    }

    if (etapas.length === 0) {
      avisarJa();
      return;
    }

    etapas.unshift({ em: 0, executar: () => setAtiva(true) });
    temporizadores.current = etapas.map(({ em, executar }) =>
      window.setTimeout(executar, em),
    );
    temporizadores.current.push(window.setTimeout(encerrar, tempo));

    // Sair da tela no meio da encenação a cancela sem marcar nada: ela toca de novo na próxima visita.
    const ativos = temporizadores;
    return () => {
      ativos.current.forEach((t) => window.clearTimeout(t));
      ativos.current = [];
      pendentes.current = [];
      setAtuacoes(new Map());
      setCarta(null);
      setAtiva(false);
    };
    // Recomeça só quando muda o conjunto de Jogadas novas; o fim é tratado por `encerrar`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chave]);

  return { atuacoes, carta, ativa, pular: encerrar };
}
