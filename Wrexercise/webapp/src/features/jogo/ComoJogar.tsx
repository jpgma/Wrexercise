import { CircleHelp, Zap } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Dialog } from "@/components/Dialog";
import { IconeCarta } from "./CartaJogo";

// Ter visto o passeio fica num cookie comum, como o aviso da FADIGA (`useEnergia.ts`): não é
// credencial, e o ESLint bloqueia `localStorage`. Vale por aparelho.
const cookiePasseio = "wx_tutorial_jogo";

function passeioVisto(): boolean {
  return document.cookie
    .split(";")
    .some((parte) => parte.trim().startsWith(`${cookiePasseio}=`));
}

function marcarPasseioVisto() {
  const seguro = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${cookiePasseio}=1; Max-Age=${400 * 24 * 60 * 60}; Path=/; SameSite=Lax${seguro}`;
}

const passos: { titulo: string; texto: string; icone: ReactNode }[] = [
  {
    titulo: "Treinou, ganhou Energia",
    texto:
      "Cada treino verificado rende EN. É com ela que você compra cartas na Loja.",
    icone: <Zap aria-hidden="true" className="size-10 text-game" />,
  },
  {
    titulo: "Provoque um colega",
    texto:
      "Toque num colega na praça, escolha uma carta da sua Mão e arremesse. A carta dá um objetivo pra essa pessoa.",
    icone: <IconeCarta codigo="Variedade" />,
  },
  {
    titulo: "Cumpriu, os dois ganham",
    texto:
      "Quem cumpre leva o valor da carta, e quem jogou recebe o preço de volta. Recusar ou deixar vencer dá FADIGA, e quem jogou perde a carta.",
    icone: <IconeCarta codigo="Aposta" />,
  },
];

const resumo = [
  "Treinos verificados rendem Energia (EN).",
  "Com EN você compra cartas na Loja.",
  "Provocação: você joga num colega e dá um objetivo para o próximo treino dele, com prazo.",
  "Resposta: você usa ao aceitar uma Provocação.",
  "Cumpriu: os dois ganham EN. Recusou, não cumpriu no próximo treino ou venceu o prazo: FADIGA, e quem jogou perde a carta.",
];

/**
 * A ajuda do modo Jogo: o botão "?" abre o resumo das regras, e o passeio de 3 passos abre sozinho
 * na primeira vez neste aparelho. O passeio pode ser pulado e revisto pelo resumo.
 */
export function ComoJogar() {
  // `null`: fechado; um número: o passo do passeio; "resumo": as regras em 5 linhas.
  const [aberto, setAberto] = useState<number | "resumo" | null>(() =>
    passeioVisto() ? null : 0,
  );
  const fechar = () => {
    marcarPasseioVisto();
    setAberto(null);
  };
  const passo = typeof aberto === "number" ? passos[aberto] : undefined;
  const ultimo = aberto === passos.length - 1;

  return (
    <>
      <button
        type="button"
        aria-label="Como jogar"
        aria-haspopup="dialog"
        className="-mx-1 grid size-11 shrink-0 place-items-center rounded-lg text-muted tap"
        onClick={() => setAberto("resumo")}
      >
        <CircleHelp aria-hidden="true" className="size-5" />
      </button>
      <Dialog
        open={aberto !== null}
        title={passo ? passo.titulo : "Como jogar"}
        onClose={fechar}
      >
        {passo && typeof aberto === "number" ? (
          <>
            <div className="grid h-16 place-items-center">{passo.icone}</div>
            <p className="mt-3 text-sm">{passo.texto}</p>
            <p className="mt-4 text-center text-xs text-muted">
              {aberto + 1} de {passos.length}
            </p>
            <div className="mt-3 flex gap-2">
              {!ultimo && (
                <button
                  type="button"
                  className="btn-ghost flex-1"
                  onClick={fechar}
                >
                  Pular
                </button>
              )}
              <button
                type="button"
                className="btn-primary flex-1"
                onClick={() => (ultimo ? fechar() : setAberto(aberto + 1))}
              >
                {ultimo ? "Bora jogar" : "Próximo"}
              </button>
            </div>
          </>
        ) : (
          <>
            <ul className="list-disc space-y-2 pl-5 text-sm">
              {resumo.map((linha) => (
                <li key={linha}>{linha}</li>
              ))}
            </ul>
            <button
              type="button"
              className="btn-ghost mt-4 w-full"
              onClick={() => setAberto(0)}
            >
              Ver o passeio de novo
            </button>
          </>
        )}
      </Dialog>
    </>
  );
}
