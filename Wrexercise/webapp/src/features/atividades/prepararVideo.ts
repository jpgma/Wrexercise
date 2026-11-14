import {
  calcularDhash,
  calcularSha256,
  redimensionar,
  type EvidenciaPreparada,
} from "./comprimirImagem";
import { lerMetadadosVideo } from "./lerMetadadosVideo";

/**
 * Vídeo como evidência (ADR 0058). O servidor, um celular, não converte nada: o navegador converte para MP4
 * H.264/AAC em 720p com WebCodecs (biblioteca `mediabunny`, carregada só aqui), gera a miniatura e os dHash
 * dos quadros, e lê a data e o local do original antes de a conversão apagá-los.
 */

export const duracaoMinimaS = 2;
export const duracaoMaximaS = 60;
/** O mesmo limite do servidor para o vídeo enviado sem conversão. */
const maxBytesOriginal = 25 * 1024 * 1024;
const maxLadoOriginal = 1920;

const ladoMaior = 1280;
const quadrosPorSegundo = 30;
const bitrateVideo = 1_200_000;
const bitrateAudio = 64_000;
const ladoQuadros = 480;
const maxLadoMiniatura = 240;
const qualidadeMiniatura = 0.6;

/** Original de até este tamanho é hasheado inteiro; acima, só o começo, o fim e o tamanho. */
const maxBytesHashInteiro = 64 * 1024 * 1024;
const bytesAmostraHash = 8 * 1024 * 1024;

export class VideoNaoProcessadoError extends Error {}

export type AnaliseVideo = {
  duracao: number;
  /** O navegador decodifica o original e codifica H.264 (e AAC, se houver áudio). */
  podeConverter: boolean;
  /** O original pode ir sem conversão: MP4/MOV, H.264, AAC ou sem áudio, até 1920 px e 25 MB. */
  originalAceito: boolean;
};

export type TrechoVideo = { inicio: number; fim: number };

const carregar = () => import("mediabunny");

async function abrir(arquivo: Blob) {
  const mb = await carregar();
  const input = new mb.Input({
    formats: mb.ALL_FORMATS,
    source: new mb.BlobSource(arquivo),
  });
  return { mb, input };
}

export async function analisarVideo(arquivo: File): Promise<AnaliseVideo> {
  let aberto: Awaited<ReturnType<typeof abrir>>;
  try {
    aberto = await abrir(arquivo);
  } catch {
    throw new VideoNaoProcessadoError(
      "Não foi possível abrir este vídeo. Tente outro.",
    );
  }
  const { mb, input } = aberto;
  try {
    const video = await input.getPrimaryVideoTrack();
    if (!video) {
      throw new VideoNaoProcessadoError(
        "Este arquivo não tem imagem de vídeo.",
      );
    }
    const audio = await input.getPrimaryAudioTrack();
    const duracao = await input.computeDuration();

    const largura = video.displayWidth;
    const altura = video.displayHeight;
    const escala = Math.min(1, ladoMaior / Math.max(largura, altura));
    const podeConverter =
      (await video.canDecode()) &&
      (await mb.canEncodeVideo("avc", {
        width: par(largura * escala),
        height: par(altura * escala),
        quality: new mb.Quality({ bitrate: bitrateVideo }),
      })) &&
      (!audio ||
        ((await audio.canDecode()) &&
          (await mb.canEncodeAudio("aac", {
            numberOfChannels: 1,
            sampleRate: audio.sampleRate,
          }))));

    const tipo = await input.getMimeType();
    const originalAceito =
      /^video\/(mp4|quicktime)/.test(tipo) &&
      video.codec === "avc" &&
      (!audio || audio.codec === "aac") &&
      Math.max(largura, altura) <= maxLadoOriginal &&
      arquivo.size <= maxBytesOriginal &&
      duracao <= duracaoMaximaS + 0.5;

    return { duracao, podeConverter, originalAceito };
  } catch (error) {
    if (error instanceof VideoNaoProcessadoError) throw error;
    throw new VideoNaoProcessadoError(
      "Não foi possível ler este vídeo. Tente outro.",
    );
  } finally {
    input.dispose();
  }
}

function par(valor: number) {
  return Math.max(2, Math.round(valor / 2) * 2);
}

async function converter(
  arquivo: File,
  trecho: TrechoVideo,
  onProgresso: (fracao: number) => void,
  signal: AbortSignal | undefined,
): Promise<Blob | null> {
  const { mb, input } = await abrir(arquivo);
  try {
    const temAudio = (await input.getPrimaryAudioTrack()) !== null;
    const target = new mb.BufferTarget();
    const output = new mb.Output({
      format: new mb.Mp4OutputFormat({ fastStart: "in-memory" }),
      target,
    });
    const conversao = await mb.Conversion.init({
      input,
      output,
      tracks: "primary",
      showWarnings: false,
      trim: { start: trecho.inicio, end: trecho.fim },
      // Nada do original vai no arquivo: a localização segue em campo à parte e nunca volta pela API.
      tags: {},
      video: (track) => {
        const paisagem = track.displayWidth >= track.displayHeight;
        const lado = Math.min(
          ladoMaior,
          paisagem ? track.displayWidth : track.displayHeight,
        );
        return {
          codec: "avc",
          ...(paisagem ? { width: par(lado) } : { height: par(lado) }),
          frameRate: quadrosPorSegundo,
          quality: new mb.Quality({ bitrate: bitrateVideo }),
          forceTranscode: true,
        };
      },
      audio: {
        codec: "aac",
        numberOfChannels: 1,
        quality: new mb.Quality({ bitrate: bitrateAudio }),
        forceTranscode: true,
      },
    });

    const usadas = conversao.utilizedTracks;
    if (
      !conversao.isValid ||
      !usadas.some((t) => t.isVideoTrack()) ||
      (temAudio && !usadas.some((t) => t.isAudioTrack()))
    ) {
      return null;
    }

    conversao.onProgress = (fracao) => onProgresso(fracao);
    const cancelar = () => void conversao.cancel();
    signal?.addEventListener("abort", cancelar);
    try {
      await conversao.execute();
    } finally {
      signal?.removeEventListener("abort", cancelar);
    }
    return target.buffer
      ? new Blob([target.buffer], { type: "video/mp4" })
      : null;
  } finally {
    input.dispose();
  }
}

/** Os quadros a 10%, 50% e 90%: o do meio vira a miniatura e o `dhash` (ADR 0058). */
async function extrairQuadros(video: Blob) {
  const { mb, input } = await abrir(video);
  try {
    const track = await input.getPrimaryVideoTrack();
    if (!track) throw new VideoNaoProcessadoError("Vídeo sem imagem.");
    const duracao = await input.computeDuration();
    const paisagem = track.displayWidth >= track.displayHeight;
    const sink = new mb.CanvasSink(
      track,
      paisagem ? { width: ladoQuadros } : { height: ladoQuadros },
    );
    const quadro = async (fracao: number) =>
      (await sink.getCanvas(duracao * fracao))?.canvas ?? null;
    const inicio = await quadro(0.1);
    const meio = await quadro(0.5);
    const fim = await quadro(0.9);
    if (!meio) throw new VideoNaoProcessadoError("Vídeo sem imagem.");

    const dhash = calcularDhash(meio);
    if (!dhash) throw new VideoNaoProcessadoError("Vídeo sem imagem.");
    return {
      duracaoMs: Math.round(duracao * 1000),
      miniatura: await redimensionar(
        meio,
        maxLadoMiniatura,
        qualidadeMiniatura,
      ),
      dhash,
      dhashInicio: inicio ? (calcularDhash(inicio) ?? undefined) : undefined,
      dhashFim: fim ? (calcularDhash(fim) ?? undefined) : undefined,
    };
  } finally {
    input.dispose();
  }
}

/**
 * SHA-256 do original, para reconhecer o mesmo vídeo enviado de novo (ADR 0052). Um vídeo longo da galeria
 * pode ter centenas de MB: aí o hash cobre o começo, o fim e o tamanho, sem carregar o arquivo inteiro.
 */
async function impressaoDoOriginal(arquivo: File) {
  if (arquivo.size <= maxBytesHashInteiro) return calcularSha256(arquivo);
  return calcularSha256(
    new Blob([
      arquivo.slice(0, bytesAmostraHash),
      arquivo.slice(arquivo.size - bytesAmostraHash),
      String(arquivo.size),
    ]),
  );
}

export async function prepararVideo(
  arquivo: File,
  analise: AnaliseVideo,
  trecho: TrechoVideo,
  opcoes: { onProgresso: (fracao: number) => void; signal?: AbortSignal },
): Promise<EvidenciaPreparada> {
  const [metadados, sha256Original] = await Promise.all([
    lerMetadadosVideo(arquivo),
    impressaoDoOriginal(arquivo),
  ]);

  let video: Blob | null = null;
  if (analise.podeConverter) {
    try {
      video = await converter(
        arquivo,
        trecho,
        opcoes.onProgresso,
        opcoes.signal,
      );
    } catch (error) {
      if (opcoes.signal?.aborted) throw error;
      video = null;
    }
  }
  const recortado = trecho.inicio > 0 || trecho.fim < analise.duracao - 0.5;
  if (!video && analise.originalAceito && !recortado) video = arquivo;
  if (!video) {
    throw new VideoNaoProcessadoError(
      "Seu navegador não consegue preparar este vídeo. Envie fotos, ou um vídeo MP4 de até 1 minuto e 25 MB.",
    );
  }

  const quadros = await extrairQuadros(video);
  const nome = arquivo.name.replace(/\.\w+$/, "") + ".mp4";
  return {
    tipo: "Video",
    arquivo: new File([video], nome, { type: "video/mp4" }),
    miniatura: quadros.miniatura,
    duracaoMs: quadros.duracaoMs,
    sinais: {
      ...metadados,
      sha256Original,
      dhash: quadros.dhash,
      dhashInicio: quadros.dhashInicio,
      dhashFim: quadros.dhashFim,
    },
  };
}
