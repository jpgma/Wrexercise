import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router";
import { CenaGrupo } from "./CenaGrupo";
import {
  adiarChegada,
  limparChegadaAdiada,
  useChegadaAdiada,
} from "./chegadaAdiada";
import type { DestinoChegada } from "./PainelChegada";
import {
  buscarPendentes,
  type JogadaPendente,
  pendentesKey,
  useJogadasPendentes,
} from "./useCartas";

/** O parâmetro da tela inicial que abre a Chegada de uma carta. */
const parametro = "chegada";

/** O que a Chegada lembra de onde a pessoa estava, para voltar depois da resposta. */
type EstadoChegada = { voltar?: string };

const getRota = (p: JogadaPendente) =>
  `/grupos/${p.grupoId}/inicio?${parametro}=${p.id}`;

/** O formulário de treino, o cronômetro do treino ao vivo, ou o rascunho do Movr que leva até ele. */
const isFormularioDeTreino = (caminho: string) =>
  /^\/grupos\/[^/]+\/atividades\/nova$/.test(caminho) ||
  /^\/grupos\/[^/]+\/treino$/.test(caminho) ||
  caminho.startsWith("/importar/");

/** Se a tela atual é a Chegada: as abas e o menu somem até a resposta. */
export function useEmChegada(): boolean {
  const [params] = useSearchParams();
  return params.has(parametro);
}

/**
 * Se há carta recebida esperando resposta, ou se ainda não dá para saber: avisos que não são
 * urgentes, como as Novidades (decisão 0077), esperam a Chegada acabar. Com erro, nada espera.
 */
export function useCartaEsperando(): boolean {
  const pendentes = useJogadasPendentes();
  if (pendentes.isError) return false;
  return !pendentes.data || pendentes.data.length > 0;
}

/**
 * O portão da Chegada (decisão 0076): com uma carta recebida esperando resposta, qualquer tela leva à
 * Chegada dela, a mais antiga primeiro. Com um treino ao vivo aberto, ou com as cartas deixadas para
 * depois do treino (decisão 0086), ela espera o treino terminar. Ao voltar para o aplicativo com o formulário de treino aberto,
 * a pessoa não é tirada dele: o servidor recusa o registro e o formulário leva à Chegada. Sem dados
 * (erro, sem rede), nada bloqueia.
 */
export function PortaoChegada({ pausado = false }: { pausado?: boolean }) {
  const pendentes = useJogadasPendentes();
  const location = useLocation();
  const navigate = useNavigate();
  const emChegada = useEmChegada();
  const primeira = pendentes.data?.[0];
  const ultimaTela = useRef<string | null>(null);
  const primeiraCarga = useRef(true);
  const adiada = useChegadaAdiada(pendentes.data?.map((p) => p.id));
  const estavaPausado = useRef(pausado);

  // "Responder depois do treino" (decisão 0086) vale até o treino ao vivo acabar ou a pessoa ir responder.
  useEffect(() => {
    if (emChegada || (estavaPausado.current && !pausado)) limparChegadaAdiada();
    estavaPausado.current = pausado;
  }, [emChegada, pausado]);

  useEffect(() => {
    if (!pendentes.data) return;
    const mudouDeTela = ultimaTela.current !== location.key;
    const permitido = primeiraCarga.current || mudouDeTela;
    ultimaTela.current = location.key;
    primeiraCarga.current = false;
    // Durante o treino ao vivo, a carta espera o fim: o cronômetro mostra que ela chegou (decisão 0078).
    if (!primeira || emChegada || pausado || adiada) return;
    if (!permitido && isFormularioDeTreino(location.pathname)) return;
    void navigate(getRota(primeira), {
      replace: true,
      state: {
        voltar: location.pathname + location.search,
      } satisfies EstadoChegada,
    });
  }, [
    pendentes.data,
    primeira,
    emChegada,
    pausado,
    adiada,
    location.key,
    location.pathname,
    location.search,
    navigate,
  ]);

  return null;
}

/**
 * A Chegada de uma carta recebida, na tela inicial do grupo, em qualquer modo: a praça encena o
 * arremesso e a resposta é obrigatória. Depois, a próxima carta pendente ou o lugar de onde a pessoa
 * veio (ou o formulário de treino).
 */
export function ChegadaPagina({
  grupoId,
  usuarioId,
}: {
  grupoId: string;
  usuarioId: string;
}) {
  const [params] = useSearchParams();
  const jogadaId = params.get(parametro) ?? "";
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const voltar = (location.state as EstadoChegada | null)?.voltar;

  const concluir = async (destino: DestinoChegada) => {
    if (destino === "adiada") {
      // Todas as cartas pendentes ficam para depois do treino; uma carta nova traz a Chegada de volta.
      const pendentes =
        queryClient.getQueryData<JogadaPendente[]>(pendentesKey);
      adiarChegada(pendentes?.map((p) => p.id) ?? [jogadaId]);
      void navigate(voltar ?? `/grupos/${grupoId}/inicio`, { replace: true });
      return;
    }
    const destinoFinal =
      destino === "treino" && !(voltar && isFormularioDeTreino(voltar))
        ? `/grupos/${grupoId}/atividades/nova`
        : (voltar ?? `/grupos/${grupoId}/inicio`);
    let proxima: JogadaPendente | undefined;
    try {
      const lista = await queryClient.fetchQuery({
        queryKey: pendentesKey,
        queryFn: ({ signal }) => buscarPendentes(signal),
        staleTime: 0,
      });
      proxima = lista.find((p) => p.id !== jogadaId);
    } catch {
      // Sem a lista, segue para onde a pessoa ia: o portão confere de novo na próxima tela.
    }
    void navigate(proxima ? getRota(proxima) : destinoFinal, {
      replace: true,
      state: proxima
        ? ({ voltar: destinoFinal } satisfies EstadoChegada)
        : undefined,
    });
  };

  return (
    <div className="mt-2">
      <h1 className="sr-only">Carta recebida</h1>
      <CenaGrupo
        key={jogadaId}
        grupoId={grupoId}
        usuarioId={usuarioId}
        chegada={{
          jogadaId,
          onConcluir: (destino) => void concluir(destino),
        }}
      />
    </div>
  );
}
