import { CalendarCheck, Dumbbell, Flame, Swords, X } from "lucide-react";
import { type ReactNode, useEffect, useRef } from "react";
import { Link } from "react-router";
import { useVitrine } from "@/components/folhaAvatar";
import { formatarDuracao } from "@/features/atividades";
import { usePerfilMembro } from "@/features/usuarios";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { primeiroNome } from "./textosCartas";
import type { MembroCena } from "./useCena";
import { getRelacaoRival, useRivais } from "./useRivais";

const textoRival = {
  escolhido: "Seu Rival da semana",
  escolheu: "Escolheu você como Rival",
} as const;

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
 * O painel do Foco, sobre a parte de baixo da praça: nome, Vitrine, como a pessoa vai no desafio e
 * o Rival. As cartas entre as duas ficam na Mesa, logo abaixo. Some durante o arremesso e a escolha
 * do Rival.
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
  const rivais = useRivais(grupoId);
  const relacao = getRelacaoRival(rivais.data, usuarioId, alvoId);
  const rival = relacao ? textoRival[relacao] : null;
  const titulo = useRef<HTMLHeadingElement>(null);
  const nome = primeiroNome(membro.nomeExibicao);

  // Quem usa teclado ou leitor de tela vai direto para o painel.
  useEffect(() => {
    titulo.current?.focus({ preventScroll: true });
  }, [alvoId]);

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
