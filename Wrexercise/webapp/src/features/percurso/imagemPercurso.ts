import { formatarDuracao, tipoAtividadeLabels } from "@/features/atividades";
import {
  formatarDistancia,
  formatarMedia,
  formatarVelocidade,
  usaVelocidade,
} from "./calculo";
import { enquadrar, paraJanela, projetar, tamanhoTile } from "./geometria";
import { textoRecorde } from "./ResumoPercurso";
import type { Percurso } from "./usePercurso";

/**
 * A imagem do Percurso (ADR 0084), desenhada no navegador ao terminar o treino e anexada como evidência com
 * o selo "Percurso". Usa o percurso aparado, como o grupo o vê. Dois estilos, conforme o modo da tela
 * inicial: `App` (o cartão da identidade, com o mapa) e `Jogo` (moldura e trajeto em pixel, com o Avatar
 * comemorando no fim). As cores e as fontes vêm dos tokens do tema.
 */

export type OpcoesImagem = {
  modo: "App" | "Jogo";
  /** A folha do Avatar, só no modo Jogo. */
  folhaAvatar: string | null;
  inicio: Date;
};

const largura = 1080;
const altura = 1350;
const margem = 60;
const mapa = { x: margem, y: 190, w: largura - 2 * margem, h: 640 };
/** A janela do mapa em px do zoom: metade do quadro, desenhada em 2×. */
const escalaMapa = 2;

/** A célula da folha do Avatar e o quadro de comemorar olhando para quem vê (linha 25, `sul`). */
const celulaAvatar = 48;
const quadroComemorar = { coluna: 4, linha: 25 };

function token(nome: string, reserva: string) {
  const valor = getComputedStyle(document.documentElement)
    .getPropertyValue(nome)
    .trim();
  return valor || reserva;
}

function carregar(src: string) {
  return new Promise<HTMLImageElement | null>((resolve) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
  });
}

function retanguloArredondado(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  r: number,
) {
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, r);
}

/** Um retângulo com os cantos em degrau, como as molduras em pixel do Jogo. */
function retanguloPixel(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  passo: number,
) {
  ctx.beginPath();
  ctx.moveTo(x + 2 * passo, y);
  ctx.lineTo(x + w - 2 * passo, y);
  ctx.lineTo(x + w - 2 * passo, y + passo);
  ctx.lineTo(x + w - passo, y + passo);
  ctx.lineTo(x + w - passo, y + 2 * passo);
  ctx.lineTo(x + w, y + 2 * passo);
  ctx.lineTo(x + w, y + h - 2 * passo);
  ctx.lineTo(x + w - passo, y + h - 2 * passo);
  ctx.lineTo(x + w - passo, y + h - passo);
  ctx.lineTo(x + w - 2 * passo, y + h - passo);
  ctx.lineTo(x + w - 2 * passo, y + h);
  ctx.lineTo(x + 2 * passo, y + h);
  ctx.lineTo(x + 2 * passo, y + h - passo);
  ctx.lineTo(x + passo, y + h - passo);
  ctx.lineTo(x + passo, y + h - 2 * passo);
  ctx.lineTo(x, y + h - 2 * passo);
  ctx.lineTo(x, y + 2 * passo);
  ctx.lineTo(x + passo, y + 2 * passo);
  ctx.lineTo(x + passo, y + passo);
  ctx.lineTo(x + 2 * passo, y + passo);
  ctx.closePath();
}

type Cores = Record<
  | "fundo"
  | "texto"
  | "suave"
  | "contorno"
  | "destaque"
  | "jogo"
  | "recompensa"
  | "sobreRecompensa"
  | "progresso"
  | "fundoMudo",
  string
>;

/** O mapa com o trajeto, desenhado num canvas do tamanho da janela (em px do zoom). */
async function desenharMapa(
  percurso: Percurso,
  cores: Cores,
  pixel: boolean,
): Promise<{
  canvas: HTMLCanvasElement;
  fim: { x: number; y: number } | null;
}> {
  const w = mapa.w / escalaMapa;
  const h = mapa.h / escalaMapa;
  const canvas = document.createElement("canvas");
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext("2d")!;
  ctx.fillStyle = cores.fundoMudo;
  ctx.fillRect(0, 0, w, h);

  if (!percurso.bbox || percurso.segmentos.length === 0) {
    return { canvas, fim: null };
  }

  const janela = enquadrar(percurso.bbox, w, h, percurso.bbox, 30);
  const base = `/api/atividades/${percurso.atividadeId}/percurso/mapa/${janela.zoom}`;
  const tiles = await Promise.all(
    janela.tiles.map(async (t) => ({
      ...t,
      img: await carregar(`${base}/${t.x}/${t.y}`),
    })),
  );
  for (const { x, y, img } of tiles) {
    if (img) {
      ctx.drawImage(
        img,
        x * tamanhoTile - janela.origem.x,
        y * tamanhoTile - janela.origem.y,
      );
    }
  }
  // Um véu leve: o trajeto se destaca sobre o mapa.
  ctx.globalAlpha = 0.25;
  ctx.fillStyle = cores.fundo;
  ctx.fillRect(0, 0, w, h);
  ctx.globalAlpha = 1;

  const tracar = (
    trechos: [number, number][][],
    cor: string,
    espessura: number,
  ) => {
    for (const trecho of trechos) {
      const pontos = paraJanela(trecho, janela);
      ctx.beginPath();
      pontos.forEach((p, i) =>
        i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y),
      );
      ctx.strokeStyle = cor;
      ctx.lineWidth = espessura;
      ctx.stroke();
    }
  };

  ctx.lineCap = pixel ? "square" : "round";
  ctx.lineJoin = pixel ? "miter" : "round";
  if (percurso.parceiro) tracar(percurso.parceiro.segmentos, cores.jogo, 4);

  // As lacunas, tracejadas.
  const linhas = percurso.segmentos.map((t) => paraJanela(t, janela));
  ctx.setLineDash([4, 6]);
  ctx.strokeStyle = cores.destaque;
  ctx.lineWidth = 3;
  for (let i = 1; i < linhas.length; i++) {
    const a = linhas[i - 1]?.at(-1);
    const b = linhas[i]?.[0];
    if (!a || !b) continue;
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
  }
  ctx.setLineDash([]);
  tracar(percurso.segmentos, cores.contorno, 9);
  tracar(percurso.segmentos, cores.destaque, 5);

  const ultimo = percurso.segmentos.at(-1)!.at(-1)!;
  const p = projetar(ultimo[0], ultimo[1], janela.zoom);
  return {
    canvas,
    fim: { x: p.x - janela.origem.x, y: p.y - janela.origem.y },
  };
}

function texto(
  ctx: CanvasRenderingContext2D,
  valor: string,
  x: number,
  y: number,
  fonte: string,
  cor: string,
  alinhamento: CanvasTextAlign = "left",
) {
  ctx.font = fonte;
  ctx.fillStyle = cor;
  ctx.textAlign = alinhamento;
  ctx.fillText(valor, x, y);
}

export async function desenharImagemPercurso(
  percurso: Percurso,
  opcoes: OpcoesImagem,
): Promise<{ imagem: Blob; miniatura: Blob | null }> {
  const pixel = opcoes.modo === "Jogo";
  const cores: Cores = {
    fundo: token("--color-surface", "#ffffff"),
    texto: token("--color-foreground", "#111111"),
    suave: token("--color-muted", "#666666"),
    contorno: token("--color-outline", "#111111"),
    destaque: token("--color-primary", "#ff684a"),
    jogo: token("--color-game", "#6b4cff"),
    recompensa: token("--color-reward", "#ffc83d"),
    sobreRecompensa: token("--color-on-reward", "#111111"),
    progresso: token("--color-progress", "#38b764"),
    fundoMudo: token("--color-surface-muted", "#eeeeee"),
  };
  const display = token("--font-display", "Impact, sans-serif");
  const sans = token("--font-sans", "system-ui, sans-serif");
  await Promise.all([
    document.fonts.load(`72px ${display}`),
    document.fonts.load(`600 32px ${sans}`),
  ]).catch(() => undefined);

  const canvas = document.createElement("canvas");
  canvas.width = largura;
  canvas.height = altura;
  const ctx = canvas.getContext("2d")!;
  ctx.fillStyle = cores.fundo;
  ctx.fillRect(0, 0, largura, altura);

  // A moldura: o adesivo do App ou o degrau do Jogo.
  ctx.lineWidth = 14;
  ctx.strokeStyle = pixel ? cores.jogo : cores.contorno;
  if (pixel) retanguloPixel(ctx, 14, 14, largura - 28, altura - 28, 14);
  else retanguloArredondado(ctx, 14, 14, largura - 28, altura - 28, 56);
  ctx.stroke();

  // O título: o tipo e o dia.
  const { resumo, tipo } = percurso;
  texto(
    ctx,
    tipoAtividadeLabels[tipo].toUpperCase(),
    margem,
    140,
    `84px ${display}`,
    cores.texto,
  );
  texto(
    ctx,
    opcoes.inicio.toLocaleDateString("pt-BR", {
      weekday: "short",
      day: "numeric",
      month: "short",
    }) +
      " · " +
      opcoes.inicio.toLocaleTimeString("pt-BR", {
        hour: "2-digit",
        minute: "2-digit",
      }),
    largura - margem,
    132,
    `600 32px ${sans}`,
    cores.suave,
    "right",
  );

  // O mapa: liso no App; no Jogo, reduzido a 1/4 e ampliado sem suavizar, em pixel.
  const { canvas: canvasMapa, fim } = await desenharMapa(
    percurso,
    cores,
    pixel,
  );
  ctx.save();
  if (pixel) retanguloPixel(ctx, mapa.x, mapa.y, mapa.w, mapa.h, 12);
  else retanguloArredondado(ctx, mapa.x, mapa.y, mapa.w, mapa.h, 32);
  ctx.clip();
  if (pixel) {
    const reduzido = document.createElement("canvas");
    reduzido.width = canvasMapa.width / 4;
    reduzido.height = canvasMapa.height / 4;
    const r = reduzido.getContext("2d")!;
    r.imageSmoothingEnabled = false;
    r.drawImage(canvasMapa, 0, 0, reduzido.width, reduzido.height);
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(reduzido, mapa.x, mapa.y, mapa.w, mapa.h);
  } else {
    ctx.drawImage(canvasMapa, mapa.x, mapa.y, mapa.w, mapa.h);
  }
  if (!fim) {
    texto(
      ctx,
      "Percurso curto demais para o mapa",
      largura / 2,
      mapa.y + mapa.h / 2,
      `600 34px ${sans}`,
      cores.suave,
      "center",
    );
  }
  ctx.restore();
  ctx.lineWidth = 6;
  ctx.strokeStyle = cores.contorno;
  if (pixel) retanguloPixel(ctx, mapa.x, mapa.y, mapa.w, mapa.h, 12);
  else retanguloArredondado(ctx, mapa.x, mapa.y, mapa.w, mapa.h, 32);
  ctx.stroke();
  texto(
    ctx,
    "Mapa © OpenStreetMap",
    mapa.x + mapa.w - 16,
    mapa.y + mapa.h - 14,
    `500 20px ${sans}`,
    cores.suave,
    "right",
  );

  // No Jogo, o Avatar comemora no fim do trajeto.
  if (pixel && fim && opcoes.folhaAvatar) {
    const folha = await carregar(opcoes.folhaAvatar);
    if (folha) {
      const lado = celulaAvatar * 4;
      const ax = Math.min(
        mapa.x + mapa.w - lado,
        Math.max(mapa.x, mapa.x + fim.x * escalaMapa - lado / 2),
      );
      const ay = Math.min(
        mapa.y + mapa.h - lado,
        Math.max(mapa.y, mapa.y + fim.y * escalaMapa - lado * 0.9),
      );
      ctx.imageSmoothingEnabled = false;
      ctx.drawImage(
        folha,
        quadroComemorar.coluna * celulaAvatar,
        quadroComemorar.linha * celulaAvatar,
        celulaAvatar,
        celulaAvatar,
        ax,
        ay,
        lado,
        lado,
      );
    }
  }

  // O adesivo do recorde, inclinado sobre o canto do mapa.
  if (resumo.recorde) {
    const rotulo = textoRecorde(
      resumo.recorde,
      usaVelocidade(tipo),
    ).toUpperCase();
    ctx.save();
    ctx.translate(largura - margem - 230, mapa.y + 10);
    ctx.rotate(-0.08);
    ctx.font = `40px ${display}`;
    const w = Math.max(360, ctx.measureText(rotulo).width + 56);
    if (pixel) retanguloPixel(ctx, -w / 2, -36, w, 80, 8);
    else retanguloArredondado(ctx, -w / 2, -36, w, 80, 40);
    ctx.fillStyle = cores.recompensa;
    ctx.fill();
    ctx.lineWidth = 6;
    ctx.strokeStyle = cores.contorno;
    ctx.stroke();
    texto(
      ctx,
      rotulo,
      0,
      18,
      `40px ${display}`,
      cores.sobreRecompensa,
      "center",
    );
    ctx.restore();
  }

  // Os números: a distância grande e, ao lado, tempo, média, máxima e subida.
  const yNumeros = mapa.y + mapa.h + 60;
  texto(
    ctx,
    "DISTÂNCIA",
    margem,
    yNumeros + 10,
    `600 26px ${sans}`,
    cores.suave,
  );
  texto(
    ctx,
    formatarDistancia(resumo.distanciaMetros),
    margem,
    yNumeros + 140,
    `130px ${display}`,
    cores.texto,
  );
  const dados: [string, string][] = [
    ["TEMPO", formatarDuracao(Math.max(60, resumo.duracaoSegundos))],
    [
      usaVelocidade(tipo) ? "VELOC. MÉDIA" : "RITMO MÉDIO",
      formatarMedia(tipo, resumo.velocidadeMedia),
    ],
    ["MÁXIMA", formatarVelocidade(resumo.velocidadeMaxima)],
    [
      "SUBIDA",
      resumo.ganhoElevacaoMetros === null
        ? "—"
        : `~${resumo.ganhoElevacaoMetros} m`,
    ],
  ];
  dados.forEach(([rotulo, valor], i) => {
    const x = 560 + (i % 2) * 240;
    const y = yNumeros + 10 + Math.floor(i / 2) * 96;
    texto(ctx, rotulo, x, y, `600 22px ${sans}`, cores.suave);
    texto(ctx, valor, x, y + 52, `44px ${display}`, cores.texto);
  });

  // As parciais em barras: a mais rápida em destaque.
  const parciais = resumo.parciais.slice(0, 20);
  if (parciais.length > 1) {
    const y0 = yNumeros + 200;
    const alturaBarras = 90;
    const velocidades = parciais.map((p) =>
      p.segundos > 0 ? p.metros / p.segundos : 0,
    );
    const melhor = Math.max(...velocidades);
    const passo = (largura - 2 * margem) / parciais.length;
    texto(
      ctx,
      usaVelocidade(tipo) ? "PARCIAIS DE 5 KM" : "PARCIAIS POR KM",
      margem,
      y0,
      `600 22px ${sans}`,
      cores.suave,
    );
    velocidades.forEach((v, i) => {
      const h = melhor > 0 ? Math.max(10, (v / melhor) * alturaBarras) : 10;
      const x = margem + i * passo + passo * 0.15;
      const w = passo * 0.7;
      ctx.fillStyle = v === melhor ? cores.recompensa : cores.progresso;
      if (pixel) ctx.fillRect(x, y0 + 16 + alturaBarras - h, w, h);
      else {
        retanguloArredondado(
          ctx,
          x,
          y0 + 16 + alturaBarras - h,
          w,
          h,
          Math.min(10, w / 2),
        );
        ctx.fill();
      }
    });
  }

  // O rodapé: as Torcidas, quem treinou junto e a marca.
  const extras = [
    percurso.torcidas > 0
      ? `📣 ${percurso.torcidas} ${percurso.torcidas === 1 ? "torcida" : "torcidas"}`
      : null,
    percurso.parceiro
      ? `Treinaram juntos: ${percurso.parceiro.nomeExibicao.split(" ")[0]}`
      : null,
  ].filter(Boolean);
  if (extras.length > 0) {
    texto(
      ctx,
      extras.join("  ·  "),
      margem,
      altura - 56,
      `600 28px ${sans}`,
      cores.texto,
    );
  }
  texto(
    ctx,
    "WREXERCISE",
    largura - margem,
    altura - 56,
    `32px ${display}`,
    cores.destaque,
    "right",
  );

  const imagem = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, "image/png"),
  );
  if (!imagem) throw new Error("Não foi possível gerar a imagem do percurso.");
  // A foto tem limite de 5 MB: um mapa muito detalhado em PNG pode passar, e o JPEG resolve.
  const final =
    imagem.size > 4.5 * 1024 * 1024
      ? ((await new Promise<Blob | null>((r) =>
          canvas.toBlob(r, "image/jpeg", 0.9),
        )) ?? imagem)
      : imagem;

  const mini = document.createElement("canvas");
  mini.width = 320;
  mini.height = 400;
  mini.getContext("2d")!.drawImage(canvas, 0, 0, mini.width, mini.height);
  const miniatura = await new Promise<Blob | null>((resolve) =>
    mini.toBlob(resolve, "image/jpeg", 0.75),
  );

  return { imagem: final, miniatura };
}
