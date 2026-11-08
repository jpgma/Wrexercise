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

/**
 * Uma encenação do Rival da semana: a provocação (os dois avançam um passo e fazem `provocar`) ou o
 * resultado (o vencedor comemora).
 */
export type RivalEmCena = {
  id: string;
  escolhedorId: string;
  rivalId: string;
  efeito: "provocacao" | "resultado";
  /** No resultado, quem comemora; `null` quando não houve vencedor. */
  vencedorId: string | null;
};

/** No máximo 3 encenações por visita (docs/gamificacao.md §3.3). */
const MaxPorVisita = 3;
const PassoMs = 900;
const VooMs = 600;
const EnfeiteMs = 1800;
/** O passo dos rivais em direção ao centro, em fração da distância. */
const PassoRival = 0.3;
const PassoRivalMs = 500;

type Etapa = { em: number; executar: () => void };

/**
 * Toca, uma de cada vez, as encenações das Jogadas e do Rival que quem vê a cena ainda não viu:
 * a chegada de uma carta (para o destinatário) e a resposta (para o remetente). Depois vem a do
 * Enfeite novo de quem vê: o avatar comemora e o Enfeite brilha. No fim, ou ao pular, marca tudo como visto.
 */
export function useEncenacoes({
  jogadas,
  posicoes,
  centro,
  reduzido,
  onVistas,
  usuarioId,
  enfeitesNovos,
  onEnfeitesVistos,
  rivais,
  onRivaisVistos,
}: {
  jogadas: Jogada[] | undefined;
  posicoes: Map<string, Ponto>;
  centro: Ponto;
  reduzido: boolean;
  onVistas: (ids: string[]) => void;
  usuarioId: string;
  /** Códigos dos Enfeites que quem vê ainda não viu. */
  enfeitesNovos: string[];
  onEnfeitesVistos: () => void;
  rivais: RivalEmCena[];
  onRivaisVistos: (ids: string[]) => void;
}) {
  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(new Map());
  const [carta, setCarta] = useState<CartaEmCena | null>(null);
  const [ativa, setAtiva] = useState(false);
  const temporizadores = useRef<number[]>([]);
  const pendentes = useRef<string[]>([]);
  const [brilho, setBrilho] = useState(false);
  const pendenteEnfeites = useRef(false);
  const avisar = useRef(onVistas);
  const avisarEnfeites = useRef(onEnfeitesVistos);
  const pendentesRivais = useRef<string[]>([]);
  const avisarRivais = useRef(onRivaisVistos);

  useEffect(() => {
    avisar.current = onVistas;
    avisarEnfeites.current = onEnfeitesVistos;
    avisarRivais.current = onRivaisVistos;
  }, [onVistas, onEnfeitesVistos, onRivaisVistos]);

  const encerrar = useCallback(() => {
    temporizadores.current.forEach((t) => window.clearTimeout(t));
    temporizadores.current = [];
    setAtuacoes(new Map());
    setCarta(null);
    setAtiva(false);
    setBrilho(false);
    if (pendentes.current.length > 0) {
      avisar.current(pendentes.current);
      pendentes.current = [];
    }
    if (pendenteEnfeites.current) {
      pendenteEnfeites.current = false;
      avisarEnfeites.current();
    }
    if (pendentesRivais.current.length > 0) {
      avisarRivais.current(pendentesRivais.current);
      pendentesRivais.current = [];
    }
  }, []);

  const chave = `${(jogadas ?? [])
    .filter((j) => j.nova)
    .map((j) => j.id)
    .join(",")}|${enfeitesNovos.join(",")}|${rivais
    .map((r) => `${r.id}:${r.efeito}`)
    .join(",")}`;

  useEffect(() => {
    if (chave === "||") return;
    const fila = (jogadas ?? []).filter((j) => j.nova);
    pendentes.current = fila.map((j) => j.id);
    pendenteEnfeites.current = enfeitesNovos.length > 0;
    pendentesRivais.current = rivais.map((r) => r.id);

    // Sem movimento, ou com a página escondida, tudo aparece direto no estado final.
    const avisarJa = () => {
      if (pendentes.current.length > 0) avisar.current(pendentes.current);
      pendentes.current = [];
      if (pendenteEnfeites.current) avisarEnfeites.current();
      pendenteEnfeites.current = false;
      if (pendentesRivais.current.length > 0)
        avisarRivais.current(pendentesRivais.current);
      pendentesRivais.current = [];
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

    const meuPonto = posicoes.get(usuarioId);
    const comEnfeite = enfeitesNovos.length > 0 && meuPonto !== undefined;
    let vagas = comEnfeite ? MaxPorVisita - 1 : MaxPorVisita;
    for (const jogada of [...fila]
      .sort((a, b) => a.enviadaEm.localeCompare(b.enviadaEm))
      .slice(0, vagas)) {
      const remetente = posicoes.get(jogada.remetenteId);
      const destinatario = posicoes.get(jogada.destinatarioId);
      if (!remetente || !destinatario) continue;
      tempo = jogada.recebida
        ? encenarChegada(etapas, tempo, jogada, remetente, destinatario)
        : encenarResposta(etapas, tempo, jogada, destinatario);
      tempo += 400;
      vagas--;
    }

    for (const rival of rivais) {
      if (vagas <= 0) break;
      const escolhedor = posicoes.get(rival.escolhedorId);
      const outro = posicoes.get(rival.rivalId);
      if (!escolhedor || !outro) continue;
      if (rival.efeito === "provocacao") {
        tempo = encenarProvocacao(etapas, tempo, rival, escolhedor, outro);
      } else if (rival.vencedorId) {
        tempo = encenarVitoria(etapas, tempo, rival.vencedorId);
      } else {
        continue;
      }
      tempo += 400;
      vagas--;
    }

    if (comEnfeite) {
      const inicio = tempo;
      etapas.push({
        em: inicio,
        executar: () => {
          setBrilho(true);
          atuar(usuarioId, {
            animacao: "comemorando",
            dx: 0,
            dy: 0,
            passoMs: 0,
          });
        },
      });
      tempo = inicio + Math.max(getDuracao("comemorando"), EnfeiteMs);
      etapas.push({
        em: tempo,
        executar: () => {
          setBrilho(false);
          atuar(usuarioId, null);
        },
      });
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

    /** Os dois avançam um passo, se encaram e fazem `provocar`; depois voltam ao lugar. */
    function encenarProvocacao(
      lista: Etapa[],
      inicio: number,
      rival: RivalEmCena,
      a: Ponto,
      b: Ponto,
    ): number {
      const pares: [string, Ponto, Ponto][] = [
        [rival.escolhedorId, a, b],
        [rival.rivalId, b, a],
      ];
      const passo = (de: Ponto) => ({
        dx: Math.round((centro.x - de.x) * PassoRival),
        dy: Math.round((centro.y - de.y) * PassoRival),
      });
      let t = inicio;
      lista.push({
        em: t,
        executar: () =>
          pares.forEach(([id, de]) =>
            atuar(id, {
              animacao: "andar",
              ...passo(de),
              espelhar: centro.x < de.x,
              passoMs: PassoRivalMs,
            }),
          ),
      });
      t += PassoRivalMs;
      lista.push({
        em: t,
        executar: () =>
          pares.forEach(([id, de, alvo]) =>
            atuar(id, {
              animacao: "provocar",
              ...passo(de),
              espelhar: alvo.x < de.x,
              passoMs: 0,
            }),
          ),
      });
      t += Math.max(getDuracao("provocar"), 700);
      lista.push({
        em: t,
        executar: () =>
          pares.forEach(([id, de]) =>
            atuar(id, {
              animacao: "andar",
              dx: 0,
              dy: 0,
              espelhar: de.x < centro.x,
              passoMs: PassoRivalMs,
            }),
          ),
      });
      t += PassoRivalMs;
      lista.push({
        em: t,
        executar: () => pares.forEach(([id]) => atuar(id, null)),
      });
      return t;
    }

    function encenarVitoria(
      lista: Etapa[],
      inicio: number,
      vencedorId: string,
    ): number {
      let t = inicio;
      lista.push({
        em: t,
        executar: () =>
          atuar(vencedorId, {
            animacao: "comemorando",
            dx: 0,
            dy: 0,
            passoMs: 0,
          }),
      });
      t += Math.max(getDuracao("comemorando"), 1200);
      lista.push({ em: t, executar: () => atuar(vencedorId, null) });
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
      pendenteEnfeites.current = false;
      pendentesRivais.current = [];
      setAtuacoes(new Map());
      setCarta(null);
      setAtiva(false);
      setBrilho(false);
    };
    // Recomeça só quando muda o conjunto de Jogadas novas; o fim é tratado por `encerrar`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chave]);

  return { atuacoes, carta, ativa, brilho, pular: encerrar };
}
