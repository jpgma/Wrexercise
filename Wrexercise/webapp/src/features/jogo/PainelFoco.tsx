import { CalendarCheck, Dumbbell, Flame, Moon, Swords, X } from "lucide-react";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { useVitrine } from "@/components/folhaAvatar";
import { formatarDuracao } from "@/features/atividades";
import { usePerfilMembro } from "@/features/usuarios";
import { getErrorMessage } from "@/services/api";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { primeiroNome } from "./textosCartas";
import {
  type CodigoCutucada,
  contarCaracteres,
  cutucadaLivre,
  normalizarTextoCutucada,
  TextoMaximoCutucada,
  tiposCutucada,
} from "./textosCutucada";
import { useCutucar } from "./useCutucar";
import type { MembroCena } from "./useCena";
import { getRelacaoRival, useRivais } from "./useRivais";

const textoRival = {
  escolhido: "Seu Rival da semana",
  escolheu: "Escolheu você como Rival",
} as const;

/** "Dormindo há 3 dias": quantos dias inteiros desde o início do sono, que começa em 2. */
function getTextoSono(desde: string): string {
  const dias = Math.max(
    2,
    Math.floor((Date.now() - new Date(desde).getTime()) / 86_400_000),
  );
  return `Dormindo há ${dias} dias`;
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
 * O painel do Foco, sobre a parte de baixo da praça: nome, Vitrine, como a pessoa vai no desafio e
 * o Rival, ou há quantos dias dorme. As cartas entre as duas ficam na Mesa, logo abaixo. Some durante o arremesso e a escolha
 * do Rival. No próprio Foco não há Rival nem cartas: a Vitrine se edita logo abaixo (VitrineFoco).
 */
export function PainelFoco({
  grupoId,
  usuarioId,
  membro,
  oculto,
  onFechar,
  onCutucado,
}: {
  grupoId: string;
  usuarioId: string;
  membro: MembroCena;
  oculto: boolean;
  onFechar: () => void;
  /** Depois da Cutucada: quem dorme leva um susto na praça. */
  onCutucado: () => void;
}) {
  const alvoId = membro.usuarioId;
  const proprio = alvoId === usuarioId;
  const perfil = usePerfilMembro(grupoId, alvoId);
  const vitrine = useVitrine(alvoId);
  const rivais = useRivais(grupoId);
  const relacao = proprio
    ? null
    : getRelacaoRival(rivais.data, usuarioId, alvoId);
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
          {(rival || membro.dormindoDesde) && (
            <div className="mt-1.5 flex flex-wrap gap-1">
              {rival && (
                <p className="chip chip-game">
                  <Swords aria-hidden="true" className="size-3" />
                  {rival}
                </p>
              )}
              {membro.dormindoDesde && (
                <p className="chip">
                  <Moon aria-hidden="true" className="size-3" />
                  {getTextoSono(membro.dormindoDesde)}
                </p>
              )}
            </div>
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

      {!proprio && membro.pose === "dormindo" && (
        <Cutucada
          grupoId={grupoId}
          membro={membro}
          nome={nome}
          onCutucado={onCutucado}
        />
      )}

      {!proprio && !membro.recebeCartas && (
        <p className="mt-2 text-xs text-muted">
          {nome} está no modo App e não recebe cartas.
        </p>
      )}

      <Link
        to={`/grupos/${grupoId}/membros/${alvoId}`}
        className="mt-1 inline-flex min-h-11 items-center text-xs font-semibold text-link"
      >
        {proprio ? "Ver a minha página" : `Ver a página de ${nome}`}
      </Link>
    </section>
  );
}

/**
 * A Cutucada (decisões 0063 e 0064): só para quem dorme, uma vez a cada 24 horas. O botão abre as
 * mensagens prontas; tocar numa delas já envia. "Escrever" abre um campo para um texto de até 80
 * caracteres, que só quem recebe lê.
 */
function Cutucada({
  grupoId,
  membro,
  nome,
  onCutucado,
}: {
  grupoId: string;
  membro: MembroCena;
  nome: string;
  onCutucado: () => void;
}) {
  const [aberta, setAberta] = useState(false);
  const [escrevendo, setEscrevendo] = useState(false);
  const [texto, setTexto] = useState("");
  const cutucar = useCutucar(grupoId);
  const cutucado = membro.cutucadoPorMimEm !== null || cutucar.isSuccess;
  const normalizado = normalizarTextoCutucada(texto);
  const caracteres = contarCaracteres(normalizado);
  const textoValido = caracteres > 0 && caracteres <= TextoMaximoCutucada;

  const enviar = (tipo: CodigoCutucada, livre?: string) =>
    cutucar.mutate(
      { destinatarioId: membro.usuarioId, tipo, texto: livre },
      {
        onSuccess: () => {
          setAberta(false);
          setEscrevendo(false);
          onCutucado();
        },
      },
    );

  if (cutucado) {
    return (
      <p className="mt-2 flex min-h-11 items-center gap-1.5 text-sm text-muted">
        <span aria-hidden="true">👉</span>
        Você já cutucou {nome} hoje.
      </p>
    );
  }

  return (
    <div className="mt-2">
      <button
        type="button"
        aria-expanded={aberta}
        aria-controls="cutucadas-opcoes"
        onClick={() => {
          setAberta((a) => !a);
          setEscrevendo(false);
        }}
        className="btn-secondary min-h-11 w-full justify-center"
      >
        <span aria-hidden="true">👉</span> Cutucar {nome}
      </button>
      {aberta && escrevendo && (
        <form
          id="cutucadas-opcoes"
          className="mt-2"
          onSubmit={(evento) => {
            evento.preventDefault();
            if (textoValido) enviar(cutucadaLivre.codigo, normalizado);
          }}
        >
          <label htmlFor="cutucada-texto" className="input-label">
            Sua mensagem para {nome}
          </label>
          <div className="mt-1 flex gap-1.5">
            <input
              id="cutucada-texto"
              type="text"
              autoFocus
              enterKeyHint="send"
              autoComplete="off"
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              aria-describedby="cutucada-contador"
              className="input-field min-h-11 min-w-0 flex-1"
            />
            <button
              type="submit"
              disabled={!textoValido || cutucar.isPending}
              className="btn-primary min-h-11 shrink-0"
            >
              Enviar
            </button>
          </div>
          <div className="mt-1 flex items-center justify-between text-xs text-muted">
            <button
              type="button"
              onClick={() => setEscrevendo(false)}
              className="min-h-11 font-semibold text-link"
            >
              Voltar às prontas
            </button>
            <span
              id="cutucada-contador"
              className={caracteres > TextoMaximoCutucada ? "text-danger" : ""}
            >
              {caracteres}/{TextoMaximoCutucada}
              <span className="sr-only"> caracteres. Só {nome} lê.</span>
            </span>
          </div>
        </form>
      )}
      {aberta && !escrevendo && (
        <div
          id="cutucadas-opcoes"
          role="group"
          aria-label={`Mensagens para cutucar ${nome}`}
          className="mt-2 grid grid-cols-2 gap-1.5"
        >
          {tiposCutucada.map((tipo) => (
            <button
              key={tipo.codigo}
              type="button"
              disabled={cutucar.isPending}
              onClick={() => enviar(tipo.codigo)}
              className="flex min-h-11 items-center gap-1.5 rounded-lg border-2 border-outline bg-surface px-2 text-left text-xs leading-tight tap disabled:opacity-50"
            >
              <span aria-hidden="true" className="text-lg leading-none">
                {tipo.emoji}
              </span>
              {tipo.texto}
            </button>
          ))}
          <button
            type="button"
            disabled={cutucar.isPending}
            onClick={() => setEscrevendo(true)}
            className="col-span-2 flex min-h-11 items-center justify-center gap-1.5 rounded-lg border-2 border-dashed border-outline px-2 text-xs font-semibold tap disabled:opacity-50"
          >
            <span aria-hidden="true" className="text-lg leading-none">
              ✍️
            </span>
            Escrever a minha
          </button>
        </div>
      )}
      {cutucar.isError && (
        <p role="alert" className="form-error mt-1">
          {getErrorMessage(cutucar.error)}
        </p>
      )}
    </div>
  );
}
