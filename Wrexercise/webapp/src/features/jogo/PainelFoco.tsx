import { CalendarCheck, Dumbbell, Flame, Swords, X } from "lucide-react";
import { type ReactNode, useEffect, useRef } from "react";
import { Link } from "react-router";
import { useVitrine } from "@/components/folhaAvatar";
import { formatarDuracao } from "@/features/atividades";
import { usePerfilMembro } from "@/features/usuarios";
import { IconeCarta } from "./CartaJogo";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { formatarPrazo, getSeloSituacao, primeiroNome } from "./textosCartas";
import { type SituacaoJogada, useJogadas } from "./useCartas";
import type { MembroCena } from "./useCena";
import { useRivais } from "./useRivais";

const ativas: readonly SituacaoJogada[] = [
  "Enviada",
  "Aceita",
  "AguardandoVerificacao",
];

/** A relação de Rival entre quem vê e o alvo nesta semana, se houver. */
function useTextoRival(grupoId: string, usuarioId: string, alvoId: string) {
  const rivais = useRivais(grupoId);
  const dados = rivais.data;
  if (!dados?.semanaInicio) return null;
  const daSemana = dados.rivais.filter(
    (r) => r.semanaInicio === dados.semanaInicio,
  );
  if (
    daSemana.some((r) => r.escolhedorId === usuarioId && r.rivalId === alvoId)
  )
    return "Seu Rival da semana";
  if (
    daSemana.some((r) => r.escolhedorId === alvoId && r.rivalId === usuarioId)
  )
    return "Escolheu você como Rival";
  return null;
}

function Dado({ icone, children }: { icone: ReactNode; children: ReactNode }) {
  return (
    <li className="flex items-center gap-1.5 text-xs text-foreground">
      <span aria-hidden="true" className="text-muted">
        {icone}
      </span>
      {children}
    </li>
  );
}

/**
 * O painel do Foco, sobre a parte de baixo da praça: nome, Vitrine, como a pessoa vai no desafio,
 * as cartas ativas entre as duas e o Rival. Some durante o arremesso e a escolha do Rival.
 */
export function PainelFoco({
  grupoId,
  usuarioId,
  membro,
  oculto,
  onFechar,
}: {
  grupoId: string;
  usuarioId: string;
  membro: MembroCena;
  oculto: boolean;
  onFechar: () => void;
}) {
  const alvoId = membro.usuarioId;
  const perfil = usePerfilMembro(grupoId, alvoId);
  const vitrine = useVitrine(alvoId);
  const jogadas = useJogadas(grupoId);
  const rival = useTextoRival(grupoId, usuarioId, alvoId);
  const titulo = useRef<HTMLHeadingElement>(null);
  const nome = primeiroNome(membro.nomeExibicao);

  // Quem usa teclado ou leitor de tela vai direto para o painel.
  useEffect(() => {
    titulo.current?.focus({ preventScroll: true });
  }, [alvoId]);

  const entreVoces = (jogadas.data ?? [])
    .filter(
      (j) =>
        ativas.includes(j.situacao) &&
        ((j.remetenteId === usuarioId && j.destinatarioId === alvoId) ||
          (j.remetenteId === alvoId && j.destinatarioId === usuarioId)),
    )
    .slice(0, 2);
  const desafio = perfil.data?.desafio;

  return (
    <section
      aria-labelledby="foco-titulo"
      className={`absolute inset-x-2 bottom-2 rounded-xl border-2 border-outline bg-surface/95 p-3 shadow-sticker transition-[opacity,translate] duration-300 ${oculto ? "pointer-events-none translate-y-4 opacity-0" : "animate-slide-up"}`}
      style={{ zIndex: 1003 }}
    >
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <h2
            id="foco-titulo"
            ref={titulo}
            tabIndex={-1}
            className="truncate font-display text-2xl leading-none uppercase outline-none"
          >
            {membro.nomeExibicao}
          </h2>
          {rival && (
            <p className="chip chip-game mt-1.5">
              <Swords aria-hidden="true" className="size-3" />
              {rival}
            </p>
          )}
        </div>
        {vitrine.length > 0 && (
          <ul aria-label="Vitrine" className="flex gap-1">
            {vitrine.map((enfeite) => (
              <li key={enfeite.codigo} title={enfeite.nome}>
                <EnfeiteIcone conquista={enfeite.conquista} tamanho={32} />
                <span className="sr-only">{enfeite.nome}</span>
              </li>
            ))}
          </ul>
        )}
        <button
          type="button"
          onClick={onFechar}
          aria-label="Fechar"
          className="-mt-1 -mr-1 grid size-11 shrink-0 place-items-center rounded-lg tap"
        >
          <X aria-hidden="true" className="size-5" />
        </button>
      </div>

      {/* A altura fica reservada enquanto o perfil carrega: nada pula. */}
      <ul className="mt-2 grid min-h-10 grid-cols-2 gap-x-3 gap-y-1">
        {perfil.data && (
          <>
            {desafio?.emAndamento && (
              <Dado icone={<CalendarCheck className="size-3.5" />}>
                <span>
                  <span className="font-score">
                    {desafio.sessoesVerificadas}/{desafio.sessoesPorSemana}
                  </span>{" "}
                  na semana
                </span>
              </Dado>
            )}
            {desafio && (
              <Dado icone={<Flame className="size-3.5" />}>
                <span>
                  <span className="font-score">{desafio.sequenciaSemanas}</span>{" "}
                  {desafio.sequenciaSemanas === 1
                    ? "semana seguida"
                    : "semanas seguidas"}
                </span>
              </Dado>
            )}
            <Dado icone={<Dumbbell className="size-3.5" />}>
              <span>
                <span className="font-score">{perfil.data.totalTreinos}</span>{" "}
                {perfil.data.totalTreinos === 1 ? "treino" : "treinos"}
                {perfil.data.totalSegundos > 0 &&
                  ` · ${formatarDuracao(perfil.data.totalSegundos)}`}
              </span>
            </Dado>
          </>
        )}
      </ul>

      {entreVoces.length > 0 && (
        <ul aria-label="Cartas entre vocês" className="mt-2 space-y-1">
          {entreVoces.map((j) => {
            const selo = getSeloSituacao(j.situacao, j.recebida);
            return (
              <li key={j.id} className="flex items-center gap-2 text-xs">
                <IconeCarta codigo={j.carta.codigo} />
                <span className="min-w-0 flex-1 truncate">
                  {j.recebida ? `${nome} jogou` : "Você jogou"}{" "}
                  <span className="font-semibold">{j.carta.nome}</span>
                  <span className="text-muted">
                    {" "}
                    · até {formatarPrazo(j.expiraEm)}
                  </span>
                </span>
                <span className={`chip ${selo.classe}`}>{selo.texto}</span>
              </li>
            );
          })}
        </ul>
      )}

      {!membro.recebeCartas && (
        <p className="mt-2 text-xs text-muted">
          {nome} está no modo App e não recebe cartas.
        </p>
      )}

      <Link
        to={`/grupos/${grupoId}/membros/${alvoId}`}
        className="mt-1 inline-flex min-h-11 items-center text-xs font-semibold text-link"
      >
        Ver a página de {nome}
      </Link>
    </section>
  );
}
