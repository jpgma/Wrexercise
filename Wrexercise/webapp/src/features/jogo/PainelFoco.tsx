import {
  CalendarCheck,
  Dumbbell,
  Flame,
  Moon,
  Radio,
  Route,
  Swords,
  X,
} from "lucide-react";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { Dialog } from "@/components/Dialog";
import { type Enfeite, useVitrine } from "@/components/folhaAvatar";
import {
  formatarDuracao,
  intervaloTorcidaMs,
  useTorcer,
} from "@/features/atividades";
import { formatarDistancia, PercursoAtividade } from "@/features/percurso";
import { usePerfilMembro } from "@/features/usuarios";
import { getErrorMessage } from "@/services/api";
import { EnfeiteIcone } from "./EnfeiteIcone";
import { primeiroNome } from "./textosCartas";
import { useMeuAvatar } from "./useAvatares";
import {
  type CodigoCutucada,
  contarCaracteres,
  cutucadaLivre,
  normalizarTextoCutucada,
  TextoMaximoCutucada,
  tiposCutucada,
} from "./textosCutucada";
import { useCutucar } from "./useCutucar";
import type { FadigaMembro, MembroCena } from "./useCena";
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

/**
 * Por que o colega está sem gás (decisão 0089): "recusou +20 MIN da Ana". Sem a carta (Jogada de outro
 * grupo), só o motivo.
 */
function getMotivoFadiga(fadiga: FadigaMembro): string | null {
  const daCarta = fadiga.carta
    ? `${fadiga.carta}${fadiga.remetenteNome ? ` de ${primeiroNome(fadiga.remetenteNome)}` : ""}`
    : null;
  switch (fadiga.motivo) {
    case "Recusa":
      return daCarta ? `Recusou ${daCarta}` : "Recusou uma carta";
    case "Falha":
      return daCarta ? `Não cumpriu ${daCarta}` : "Não cumpriu uma carta";
    case "Expiracao":
      return daCarta ? `Deixou ${daCarta} vencer` : "Deixou uma carta vencer";
    default:
      return null;
  }
}

/** "Ao vivo há 25 min": desde quando treina, contado do início. */
function getTextoAoVivo(desde: string): string {
  const segundos = (Date.now() - new Date(desde).getTime()) / 1000;
  return `Ao vivo há ${formatarDuracao(Math.max(60, segundos))}`;
}

/**
 * O Percurso de quem treina ao vivo (ADR 0084): um selo com a distância que abre o mapa, para não tirar
 * altura da praça e da Mão (gamificacao.md §3.5). O mapa se refaz a cada 30 s enquanto está aberto.
 */
function TrajetoAoVivo({
  atividadeId,
  distanciaMetros,
  nome,
}: {
  atividadeId: string;
  distanciaMetros: number;
  nome: string;
}) {
  const [aberto, setAberto] = useState(false);
  return (
    <>
      <button
        type="button"
        className="chip tap"
        aria-label={`Ver o trajeto de ${nome}: ${formatarDistancia(distanciaMetros)} até agora`}
        onClick={() => setAberto(true)}
      >
        <Route aria-hidden="true" className="size-3" />
        {formatarDistancia(distanciaMetros)}
      </button>
      <Dialog
        open={aberto}
        title={`Trajeto de ${nome}`}
        onClose={() => setAberto(false)}
      >
        {aberto && <PercursoAtividade atividadeId={atividadeId} aoVivo />}
      </Dialog>
    </>
  );
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
 * Os Enfeites da Vitrine de quem está em Foco, num botão só: tocar abre o nome de cada um e, para
 * os que quem vê ainda não tem, o que falta para liberar.
 */
function VitrineDoColega({
  vitrine,
  titulo,
  proprio,
}: {
  vitrine: Enfeite[];
  titulo: string;
  proprio: boolean;
}) {
  const [aberta, setAberta] = useState(false);
  const meu = useMeuAvatar().data;
  const situacao = (enfeite: Enfeite) => {
    if (proprio || !meu) return null;
    if (meu.liberados.some((e) => e.codigo === enfeite.codigo))
      return "Você também tem esse.";
    const falta = meu.bloqueados.find(
      (e) => e.codigo === enfeite.codigo,
    )?.falta;
    return falta ? `Você ainda não tem. ${falta}.` : "Você ainda não tem.";
  };

  return (
    <>
      <button
        type="button"
        aria-haspopup="dialog"
        aria-label={`${titulo}: ${vitrine.map((e) => e.nome).join(", ")}`}
        onClick={() => setAberta(true)}
        className="flex min-h-11 shrink-0 items-center gap-1 rounded-lg tap"
      >
        {vitrine.map((enfeite) => (
          <EnfeiteIcone
            key={enfeite.codigo}
            conquista={enfeite.conquista}
            tamanho={32}
          />
        ))}
      </button>
      <Dialog open={aberta} title={titulo} onClose={() => setAberta(false)}>
        <ul className="space-y-3">
          {vitrine.map((enfeite) => (
            <li key={enfeite.codigo} className="flex items-center gap-3">
              <EnfeiteIcone conquista={enfeite.conquista} tamanho={48} />
              <div className="min-w-0">
                <p className="text-sm font-semibold">{enfeite.nome}</p>
                {situacao(enfeite) && (
                  <p className="text-xs text-muted">{situacao(enfeite)}</p>
                )}
              </div>
            </li>
          ))}
        </ul>
      </Dialog>
    </>
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
          {(rival ||
            membro.dormindoDesde ||
            membro.treinoAoVivo ||
            membro.fadiga) && (
            <div className="mt-1.5 flex flex-wrap gap-1">
              {rival && (
                <p className="chip chip-game">
                  <Swords aria-hidden="true" className="size-3" />
                  {rival}
                </p>
              )}
              {membro.treinoAoVivo && (
                <p className="chip chip-danger">
                  <Radio aria-hidden="true" className="size-3" />
                  {getTextoAoVivo(membro.treinoAoVivo.startedAt)}
                </p>
              )}
              {membro.treinoAoVivo?.distanciaMetros != null && (
                <TrajetoAoVivo
                  atividadeId={membro.treinoAoVivo.atividadeId}
                  distanciaMetros={membro.treinoAoVivo.distanciaMetros}
                  nome={nome}
                />
              )}
              {membro.dormindoDesde && (
                <p className="chip">
                  <Moon aria-hidden="true" className="size-3" />
                  {getTextoSono(membro.dormindoDesde)}
                </p>
              )}
              {membro.fadiga && (
                <p className="chip chip-danger">
                  <img
                    src="/fadiga.png"
                    alt=""
                    width={12}
                    height={12}
                    className="shrink-0 [image-rendering:pixelated]"
                  />
                  Sem gás ·{" "}
                  {membro.fadiga.restantes === 1
                    ? "1 treino pra recarregar"
                    : `${membro.fadiga.restantes} treinos pra recarregar`}
                </p>
              )}
            </div>
          )}
          {membro.fadiga && getMotivoFadiga(membro.fadiga) && (
            <p className="mt-1 text-xs text-muted">
              {getMotivoFadiga(membro.fadiga)}
            </p>
          )}
        </div>
        {vitrine.length > 0 && (
          <VitrineDoColega
            vitrine={vitrine}
            titulo={proprio ? "Sua Vitrine" : `Vitrine de ${nome}`}
            proprio={proprio}
          />
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

      {!proprio && membro.treinoAoVivo && (
        <Torcer
          grupoId={grupoId}
          atividadeId={membro.treinoAoVivo.atividadeId}
          torcidaPorMimEm={membro.treinoAoVivo.torcidaPorMimEm}
          nome={nome}
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
 * A Torcida (ADR 0078): para quem treina ao vivo agora, uma vez a cada 10 minutos. Quem treina vê
 * a Torcida no cronômetro.
 */
function Torcer({
  grupoId,
  atividadeId,
  torcidaPorMimEm,
  nome,
}: {
  grupoId: string;
  atividadeId: string;
  torcidaPorMimEm: string | null;
  nome: string;
}) {
  const torcer = useTorcer(grupoId);
  const [agora] = useState(() => Date.now());
  const recente =
    torcer.isSuccess ||
    (torcidaPorMimEm !== null &&
      agora - new Date(torcidaPorMimEm).getTime() < intervaloTorcidaMs);

  if (recente) {
    return (
      <p
        role="status"
        className="mt-2 flex min-h-11 items-center gap-1.5 text-sm text-muted"
      >
        <span aria-hidden="true">📣</span>
        Você torceu por {nome}. Daqui a pouco dá de novo.
      </p>
    );
  }

  return (
    <div className="mt-2">
      <button
        type="button"
        disabled={torcer.isPending}
        onClick={() => torcer.mutate(atividadeId)}
        className="btn-primary min-h-11 w-full justify-center"
      >
        <span aria-hidden="true">📣</span> Torcer por {nome}
      </button>
      {torcer.isError && (
        <p role="alert" className="input-error">
          {getErrorMessage(torcer.error)}
        </p>
      )}
    </div>
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
