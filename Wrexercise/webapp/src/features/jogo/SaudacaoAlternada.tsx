import { useEffect, useState } from "react";
import { useMovimentoReduzido } from "./useMovimentoReduzido";

type Quadro = { linha: string; titulo: string };

const intervaloMs = 4000;

/**
 * Cabeçalho do modo `Jogo`: alterna entre a saudação e o progresso da semana.
 * Para o leitor de tela, os dois textos ficam fixos. Tocar troca o quadro e pausa a alternância;
 * com movimento reduzido, não alterna e mostra a saudação com o detalhe do progresso.
 */
export function SaudacaoAlternada({
  saudacao,
  progresso,
}: {
  saudacao: Quadro;
  progresso: Quadro;
}) {
  const reduzido = useMovimentoReduzido();
  const [indice, setIndice] = useState(0);
  const [pausado, setPausado] = useState(false);

  useEffect(() => {
    if (reduzido || pausado) return;
    const timer = window.setInterval(
      () => setIndice((i) => (i + 1) % 2),
      intervaloMs,
    );
    return () => window.clearInterval(timer);
  }, [reduzido, pausado]);

  if (reduzido) {
    return (
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm text-muted">{progresso.linha}</p>
        <h1 className="truncate text-3xl uppercase">{saudacao.titulo}</h1>
        <p className="sr-only">{progresso.titulo}</p>
      </div>
    );
  }

  return (
    <div className="min-w-0 flex-1">
      <h1 className="sr-only">{saudacao.titulo}</h1>
      <p className="sr-only">
        {progresso.titulo} {progresso.linha}
      </p>
      <div
        aria-hidden="true"
        className="h-[3.5rem] cursor-pointer overflow-hidden select-none"
        onClick={() => {
          setPausado(true);
          setIndice((i) => (i + 1) % 2);
        }}
        onMouseEnter={() => setPausado(true)}
        onMouseLeave={() => setPausado(false)}
      >
        <div
          className="transition-transform duration-350 ease-out"
          style={{ transform: `translateY(${-indice * 3.5}rem)` }}
        >
          {[saudacao, progresso].map((quadro, i) => (
            <div key={i} className="h-[3.5rem]">
              <p className="truncate text-sm text-muted first-letter:uppercase">
                {quadro.linha}
              </p>
              <p className="truncate font-display text-3xl leading-9 tracking-[0.01em] uppercase">
                {quadro.titulo}
              </p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
