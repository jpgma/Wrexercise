import { direcoes, folha as formato, linhasFolha } from "./animacoes";

/** Uma linha da folha no guia: o nome que aparece e quantos quadros ela usa. */
type LinhaGuia = { rotulo: string; quadros: number };

function getLinhasGuia(): LinhaGuia[] {
  const resultado: LinhaGuia[] = [
    { rotulo: "rotacao", quadros: direcoes.length },
  ];
  const porLinha = Object.entries(linhasFolha).sort(
    ([, a], [, b]) => a.linha - b.linha,
  );
  for (const [nome, { quadros }] of porLinha) {
    for (const direcao of direcoes) {
      resultado.push({ rotulo: `${nome} · ${direcao}`, quadros });
    }
  }
  return resultado;
}

/**
 * Desenha o guia de edição da folha, no mesmo tamanho dela, para usar como camada no editor de pixel
 * art: a grade, o nome de cada linha, as células sem uso sombreadas e a âncora (o ponto entre os pés)
 * em cada célula usada. É gerado aqui a partir do formato, então nunca fica desatualizado.
 */
export async function gerarGuiaFolha(): Promise<Blob> {
  const { celula, colunas, linhas, ancora } = formato;
  const canvas = document.createElement("canvas");
  canvas.width = colunas * celula;
  canvas.height = linhas * celula;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("Não foi possível gerar o guia.");

  getLinhasGuia().forEach(({ rotulo, quadros }, linha) => {
    const y = linha * celula;
    ctx.fillStyle = "rgba(0, 0, 0, 0.35)";
    ctx.fillRect(quadros * celula, y, (colunas - quadros) * celula, celula);

    ctx.fillStyle = "rgb(255, 64, 160)";
    for (let coluna = 0; coluna < quadros; coluna++) {
      ctx.fillRect(coluna * celula + ancora.x, y + ancora.y, 1, 1);
    }

    ctx.font = "8px sans-serif";
    ctx.textBaseline = "top";
    ctx.fillStyle = "rgb(0, 140, 255)";
    ctx.fillText(rotulo, 2, y + 2);
  });

  ctx.fillStyle = "rgba(0, 140, 255, 0.6)";
  for (let coluna = 1; coluna < colunas; coluna++) {
    ctx.fillRect(coluna * celula, 0, 1, canvas.height);
  }
  for (let linha = 1; linha < linhas; linha++) {
    ctx.fillRect(0, linha * celula, canvas.width, 1);
  }

  const blob = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, "image/png"),
  );
  if (!blob) throw new Error("Não foi possível gerar o guia.");
  return blob;
}
