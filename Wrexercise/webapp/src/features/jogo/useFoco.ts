import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import {
  animacoes,
  type Direcao,
  getDirecao,
  getDuracao,
  getGiro,
  type NomeAnimacao,
  quadroImpacto,
  quadroSoltura,
  quadroSusto,
} from "./animacoes";
import type { CodigoCarta } from "./useCartas";
import type { Atuacao, Ponto } from "./useEncenacoes";

/** Para onde a câmera leva a praça: deslocamento e ampliação, em px da praça (360×400). */
export type Camera = { x: number; y: number; zoom: number; ms: number };

export type FaseFoco = "aberto" | "arremessando" | "rivalizando";

/** A carta do Foco: voa de quem joga até o alvo ou cai, quando o envio falha. */
export type CartaNoFoco = {
  codigo: CodigoCarta;
  efeito: "voo" | "sai";
  de: Ponto;
  para: Ponto;
};

export type EfeitoFoco =
  { tipo: "impacto"; texto: string; ponto: Ponto } | { tipo: "vs" };

const parametro = "foco";
const semCamera: Camera = { x: 0, y: 0, zoom: 1, ms: 0 };
const CameraMs = 450;
const PassoGiroMs = 70;
const VooMs = 600;
const ZoomFoco = 2.2;
const ZoomAcao = 2;
/** Onde fica o peito de quem está em Foco: no alto da praça, acima do painel. */
const MiraFoco: Ponto = { x: 180, y: 130 };
/** Onde fica a ação (arremesso, Rival): no meio da praça, sem o painel. */
const MiraAcao: Ponto = { x: 180, y: 200 };
/** O passo dos rivais em direção ao centro, em fração da distância (o mesmo das encenações). */
const PassoRival = 0.3;
const PassoRivalMs = 500;

const pose = (
  animacao: NomeAnimacao,
  direcao: Direcao,
  extra: Partial<Atuacao> = {},
): Atuacao => ({ animacao, direcao, dx: 0, dy: 0, passoMs: 0, ...extra });

/**
 * O Foco num membro da Cena (docs/gamificacao.md §3.5): a câmera se aproxima, o avatar gira até
 * olhar para quem vê a tela e, a partir dele, quem vê joga uma carta ou escolhe o Rival, com o
 * próprio avatar entrando em cena. O alvo pode ser o próprio avatar, que só gira e não tem ação.
 * O alvo fica na URL (`?foco=`): o botão voltar fecha o Foco.
 *
 * Quem dorme não gira: ao abrir o Foco, toma um susto (`acordar`) e volta a dormir. Atingido por uma
 * carta, toma o mesmo susto no lugar de `receber`. Para andar ou arremessar, levanta antes
 * (`levantar`) e volta a dormir (`adormecer`) no fim.
 */
export function useFoco({
  posicoes,
  centro,
  alturaPeito,
  usuarioId,
  reduzido,
  pausado,
  dormindo,
}: {
  posicoes: Map<string, Ponto>;
  centro: Ponto;
  /** Altura do peito acima dos pés, em px da praça: é para lá que a câmera mira. */
  alturaPeito: number;
  usuarioId: string;
  reduzido: boolean;
  /** Enquanto a entrada toca, o Foco espera. */
  pausado: boolean;
  /** Quem está com a pose `dormindo`. */
  dormindo: Set<string>;
}) {
  const [params, setParams] = useSearchParams();
  const pedido = params.get(parametro);
  // O alvo pode ser quem vê a tela: o próprio Foco mostra a Vitrine no lugar da Mão.
  const alvoId = !pausado && pedido && posicoes.has(pedido) ? pedido : null;

  const [atuacoes, setAtuacoes] = useState<Map<string, Atuacao>>(new Map());
  const [fase, setFase] = useState<FaseFoco>("aberto");
  const [cameraAcao, setCameraAcao] = useState<Camera | null>(null);
  const [carta, setCarta] = useState<CartaNoFoco | null>(null);
  const [efeito, setEfeito] = useState<EfeitoFoco | null>(null);
  const [tremor, setTremor] = useState(false);
  /** Cada sequência tem um número: abrir outro Foco ou fechar invalida a que estava tocando. */
  const geracao = useRef(0);
  const temporizadores = useRef(new Set<number>());
  /** O Foco foi aberto por um toque (e não por um link): fechar volta no histórico. */
  const empurrado = useRef(false);
  const posicoesRef = useRef(posicoes);
  const anterior = useRef<string | null>(null);
  const dormindoRef = useRef(dormindo);

  useEffect(() => {
    posicoesRef.current = posicoes;
    dormindoRef.current = dormindo;
  }, [posicoes, dormindo]);

  const esperar = useCallback(
    (ms: number) =>
      new Promise<void>((resolver) => {
        const id = window.setTimeout(() => {
          temporizadores.current.delete(id);
          resolver();
        }, ms);
        temporizadores.current.add(id);
      }),
    [],
  );

  const atuar = useCallback(
    (id: string, atuacao: Atuacao | null) =>
      setAtuacoes((atual) => {
        const proxima = new Map(atual);
        if (atuacao) proxima.set(id, atuacao);
        else proxima.delete(id);
        return proxima;
      }),
    [],
  );

  const cancelar = useCallback(() => {
    geracao.current++;
    temporizadores.current.forEach((t) => window.clearTimeout(t));
    temporizadores.current.clear();
    setFase("aberto");
    setCameraAcao(null);
    setCarta(null);
    setEfeito(null);
    setTremor(false);
  }, []);

  // Ao abrir, o alvo gira até `sul`; ao fechar ou trocar, quem saiu gira de volta para o centro.
  useEffect(() => {
    const saiu = anterior.current;
    anterior.current = alvoId;
    if (saiu === alvoId) return;
    cancelar();
    const minha = geracao.current;
    const vivo = () => geracao.current === minha;
    const repouso = (id: string) => {
      const p = posicoesRef.current.get(id);
      return p ? getDirecao(p, centro) : "sul";
    };

    const dorme = (id: string) => dormindoRef.current.has(id);
    const inicial = new Map<string, Atuacao>();
    if (saiu && !dorme(saiu)) inicial.set(saiu, pose("parado", "sul"));
    if (alvoId && !dorme(alvoId))
      inicial.set(alvoId, pose("parado", repouso(alvoId)));
    // Quem dorme toma um susto com o toque e volta a dormir, sem girar.
    if (alvoId && dorme(alvoId) && !reduzido)
      inicial.set(alvoId, pose("acordar", repouso(alvoId)));
    setAtuacoes(inicial);
    if (alvoId && dorme(alvoId) && !reduzido) {
      void esperar(getDuracao("acordar")).then(
        () => vivo() && atuar(alvoId, null),
      );
    }

    const girar = async (id: string, de: Direcao, para: Direcao) => {
      for (const direcao of reduzido ? [] : getGiro(de, para)) {
        atuar(id, pose("parado", direcao));
        await esperar(PassoGiroMs);
        if (!vivo()) return false;
      }
      return true;
    };
    if (saiu && !dorme(saiu)) {
      void girar(saiu, "sul", repouso(saiu)).then(
        (ok) => ok && atuar(saiu, null),
      );
    }
    if (alvoId && !dorme(alvoId)) {
      void girar(alvoId, repouso(alvoId), "sul").then(
        (ok) => ok && atuar(alvoId, pose("parado", "sul")),
      );
    }
    // Só a troca de alvo recomeça o giro.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [alvoId]);

  useEffect(
    () => () => {
      geracao.current++;
      temporizadores.current.forEach((t) => window.clearTimeout(t));
    },
    [],
  );

  const abrir = useCallback(
    (id: string) => {
      const jaAberto = params.has(parametro);
      setParams(
        (atual) => {
          const proximos = new URLSearchParams(atual);
          proximos.set(parametro, id);
          return proximos;
        },
        { replace: jaAberto },
      );
      if (!jaAberto) empurrado.current = true;
    },
    [params, setParams],
  );

  const fechar = useCallback(() => {
    if (empurrado.current) {
      empurrado.current = false;
      window.history.back();
      return;
    }
    setParams(
      (atual) => {
        const proximos = new URLSearchParams(atual);
        proximos.delete(parametro);
        return proximos;
      },
      { replace: true },
    );
  }, [setParams]);

  // O botão voltar fecha o Foco sem passar por `fechar`: o histórico já não tem a entrada.
  useEffect(() => {
    if (!pedido) empurrado.current = false;
  }, [pedido]);

  useEffect(() => {
    if (!alvoId) return;
    const aoTeclar = (e: KeyboardEvent) => {
      if (e.key === "Escape") fechar();
    };
    document.addEventListener("keydown", aoTeclar);
    return () => document.removeEventListener("keydown", aoTeclar);
  }, [alvoId, fechar]);

  /** A câmera com o peito de `p` no ponto `mira` da praça. */
  const mirar = useCallback(
    (p: Ponto, zoom: number, mira: Ponto, ms = CameraMs): Camera => ({
      x: mira.x - p.x * zoom,
      y: mira.y - (p.y - alturaPeito) * zoom,
      zoom,
      ms,
    }),
    [alturaPeito],
  );

  /**
   * Joga a carta no alvo: a câmera vai até quem joga, o avatar gira e arremessa, e a carta voa
   * até o alvo, que a recebe. O envio corre junto; o braço espera no quadro da soltura até o
   * servidor responder. Falhando, a carta cai e o avatar faz `recusar`. Termina no impacto.
   */
  const arremessar = useCallback(
    async (codigo: CodigoCarta, texto: string, envio: Promise<unknown>) => {
      const resultado = envio.then(
        () => true,
        () => false,
      );
      const eu = posicoesRef.current.get(usuarioId);
      const alvo = alvoId ? posicoesRef.current.get(alvoId) : undefined;
      if (!alvoId || !eu || !alvo) {
        await resultado;
        return;
      }
      cancelar();
      const minha = geracao.current;
      const vivo = () => geracao.current === minha;
      const alvoDorme = dormindoRef.current.has(alvoId);
      const euDurmo = dormindoRef.current.has(usuarioId);
      // O susto da abertura do Foco some se a ação começar antes do fim dele.
      if (alvoDorme) atuar(alvoId, null);
      // Quem dorme volta à própria pose, sentado; quem não dorme fica olhando para quem vê.
      const descanso = alvoDorme ? null : pose("parado", "sul");
      const recebe = alvoDorme ? "acordar" : "receber";
      const quadroRecebe = alvoDorme ? quadroSusto : quadroImpacto;
      const repousoEu = getDirecao(eu, centro);
      /** Quem joga e dorme volta a dormir no lugar, sem segurar o fim do arremesso. */
      const voltarADormir = () => {
        atuar(usuarioId, pose("adormecer", repousoEu));
        void esperar(getDuracao("adormecer")).then(
          () => vivo() && atuar(usuarioId, null),
        );
      };

      if (reduzido) {
        if (!(await resultado) || !vivo()) return;
        atuar(alvoId, pose(recebe, "sul", { quadro: quadroRecebe }));
        await esperar(600);
        if (vivo()) atuar(alvoId, descanso);
        return;
      }

      let pronto = false;
      void resultado.then(() => (pronto = true));
      setFase("arremessando");
      setCameraAcao(mirar(eu, ZoomAcao, MiraAcao));
      if (euDurmo) {
        atuar(usuarioId, pose("levantar", repousoEu));
        await esperar(getDuracao("levantar"));
        if (!vivo()) return;
      }
      const rumo = getDirecao(eu, alvo);
      const giro = getGiro(repousoEu, rumo);
      for (const direcao of giro) {
        atuar(usuarioId, pose("parado", direcao));
        await esperar(PassoGiroMs);
        if (!vivo()) return;
      }
      await esperar(Math.max(0, CameraMs - giro.length * PassoGiroMs));
      if (!vivo()) return;

      atuar(usuarioId, pose("arremessar", rumo));
      await esperar(quadroSoltura * animacoes.arremessar.ms);
      if (!vivo()) return;
      if (!pronto) {
        atuar(usuarioId, pose("arremessar", rumo, { quadro: quadroSoltura }));
      }
      const ok = await resultado;
      if (!vivo()) return;

      if (!ok) {
        setCarta({ codigo, efeito: "sai", de: eu, para: eu });
        atuar(usuarioId, pose("recusar", "sul"));
        await esperar(900);
        if (!vivo()) return;
        setCarta(null);
        if (euDurmo) voltarADormir();
        else atuar(usuarioId, null);
        setCameraAcao(null);
        setFase("aberto");
        return;
      }

      // A câmera acompanha a carta até o alvo, que começa o `receber` antes do fim do voo.
      setCarta({ codigo, efeito: "voo", de: eu, para: alvo });
      setCameraAcao(mirar(alvo, ZoomAcao, MiraAcao, VooMs));
      const impactoMs = quadroRecebe * animacoes[recebe].ms;
      await esperar(VooMs - impactoMs);
      if (!vivo()) return;
      atuar(alvoId, pose(recebe, getDirecao(alvo, eu)));
      await esperar(impactoMs);
      if (!vivo()) return;
      setCarta(null);
      setTremor(true);
      setEfeito({ tipo: "impacto", texto, ponto: alvo });
      if ("vibrate" in navigator) navigator.vibrate(30);
      if (euDurmo) voltarADormir();
      else atuar(usuarioId, null);
      await esperar(getDuracao(recebe) - impactoMs + 500);
      if (!vivo()) return;
      setTremor(false);
      setEfeito(null);
      atuar(alvoId, descanso);
      setCameraAcao(null);
      setFase("aberto");
    },
    [alvoId, atuar, cancelar, centro, esperar, mirar, reduzido, usuarioId],
  );

  /**
   * Escolhe o alvo como Rival: os dois dão um passo para o centro, se encaram, o "VS" bate na tela e
   * os dois fazem `provocar`. Depois voltam ao lugar.
   */
  const rivalizar = useCallback(
    async (envio: Promise<unknown>) => {
      const resultado = envio.then(
        () => true,
        () => false,
      );
      const eu = posicoesRef.current.get(usuarioId);
      const alvo = alvoId ? posicoesRef.current.get(alvoId) : undefined;
      if (!alvoId || !eu || !alvo) {
        await resultado;
        return;
      }
      cancelar();
      const minha = geracao.current;
      const vivo = () => geracao.current === minha;

      if (reduzido) {
        if (!(await resultado) || !vivo()) return;
        setEfeito({ tipo: "vs" });
        await esperar(900);
        if (vivo()) setEfeito(null);
        return;
      }

      const pares: [string, Ponto, Ponto][] = [
        [usuarioId, eu, alvo],
        [alvoId, alvo, eu],
      ];
      // Quem dorme, dos dois, se levanta antes do passo e volta a dormir no fim. O susto da
      // abertura do Foco some se a escolha começar antes do fim dele.
      const dormem = pares.filter(([id]) => dormindoRef.current.has(id));
      if (dormindoRef.current.has(alvoId)) atuar(alvoId, null);
      const passo = (de: Ponto) => ({
        dx: Math.round((centro.x - de.x) * PassoRival),
        dy: Math.round((centro.y - de.y) * PassoRival),
      });
      const meio = {
        x: (eu.x + alvo.x) / 2 + (centro.x - (eu.x + alvo.x) / 2) * PassoRival,
        y: (eu.y + alvo.y) / 2 + (centro.y - (eu.y + alvo.y) / 2) * PassoRival,
      };
      const distancia = Math.max(
        Math.abs(eu.x - alvo.x),
        Math.abs(eu.y - alvo.y) * 1.5,
        1,
      );
      const zoom = Math.min(ZoomAcao, Math.max(1.2, 200 / distancia));

      setFase("rivalizando");
      setCameraAcao(mirar(meio, zoom, MiraAcao));
      if (dormem.length > 0) {
        dormem.forEach(([id, de]) =>
          atuar(id, pose("levantar", getDirecao(de, centro))),
        );
        await esperar(getDuracao("levantar"));
        if (!vivo()) return;
      }
      pares.forEach(([id, de]) =>
        atuar(
          id,
          pose("andar", getDirecao(de, centro), {
            ...passo(de),
            passoMs: PassoRivalMs,
          }),
        ),
      );
      await esperar(PassoRivalMs);
      if (!vivo()) return;
      pares.forEach(([id, de, outro]) =>
        atuar(id, pose("parado", getDirecao(de, outro), passo(de))),
      );
      const ok = await resultado;
      if (!vivo()) return;

      if (ok) {
        setEfeito({ tipo: "vs" });
        pares.forEach(([id, de, outro]) =>
          atuar(id, pose("provocar", getDirecao(de, outro), passo(de))),
        );
        await esperar(Math.max(getDuracao("provocar"), 700) + 400);
      } else {
        atuar(usuarioId, pose("recusar", getDirecao(eu, alvo), passo(eu)));
        await esperar(getDuracao("recusar") + 300);
      }
      if (!vivo()) return;
      setEfeito(null);
      pares.forEach(([id, de]) =>
        atuar(
          id,
          pose("andar", getDirecao(centro, de), { passoMs: PassoRivalMs }),
        ),
      );
      await esperar(PassoRivalMs);
      if (!vivo()) return;
      if (dormem.length > 0) {
        dormem.forEach(([id, de]) =>
          atuar(id, pose("adormecer", getDirecao(de, centro))),
        );
        await esperar(getDuracao("adormecer"));
        if (!vivo()) return;
      }
      atuar(usuarioId, null);
      atuar(
        alvoId,
        dormindoRef.current.has(alvoId) ? null : pose("parado", "sul"),
      );
      setCameraAcao(null);
      setFase("aberto");
    },
    [alvoId, atuar, cancelar, centro, esperar, mirar, reduzido, usuarioId],
  );

  const alvo = alvoId ? posicoes.get(alvoId) : undefined;
  const camera: Camera = !alvo
    ? { ...semCamera, ms: CameraMs }
    : (cameraAcao ?? mirar(alvo, ZoomFoco, MiraFoco));

  return {
    alvoId,
    fase,
    camera,
    atuacoes,
    carta,
    efeito,
    tremor,
    abrir,
    fechar,
    arremessar,
    rivalizar,
  };
}
