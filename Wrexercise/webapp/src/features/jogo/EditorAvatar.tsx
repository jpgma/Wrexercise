import { useEffect, useState } from "react";
import { Avatar } from "@/components/Avatar";
import { useSessao } from "@/features/autenticacao";
import { getErrorMessage } from "@/services/api";
import { animacoes, type NomeAnimacao } from "./animacoes";
import { SpriteAvatar } from "./SpriteAvatar";
import { useEnviarFolha, useRestaurarFolha } from "./useAvatares";

/** O formato público da folha (docs/avatar-formato.md). */
const largura = 256;
const altura = 384;
const maxBytes = 64 * 1024;

type Escolhida = { blob: Blob; url: string };

/**
 * Abre o arquivo num canvas e o grava de novo como PNG: o navegador confere o tamanho e remove
 * metadados. O servidor confere tudo outra vez.
 */
async function prepararFolha(arquivo: File): Promise<Blob> {
  const imagem = await createImageBitmap(arquivo).catch(() => {
    throw new Error("Não foi possível abrir essa imagem. Use um arquivo PNG.");
  });
  if (imagem.width !== largura || imagem.height !== altura) {
    imagem.close();
    throw new Error(
      `A folha precisa ter exatamente ${largura}×${altura} px. Esta tem ${imagem.width}×${imagem.height} px.`,
    );
  }

  const canvas = document.createElement("canvas");
  canvas.width = largura;
  canvas.height = altura;
  canvas.getContext("2d")?.drawImage(imagem, 0, 0);
  imagem.close();

  const blob = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, "image/png"),
  );
  if (!blob) throw new Error("Não foi possível preparar a imagem.");
  if (blob.size > maxBytes) {
    throw new Error(
      `A folha ficou com ${Math.ceil(blob.size / 1024)} KB, e o limite é 64 KB. Use menos cores.`,
    );
  }
  return blob;
}

const nomesDasLinhas: Record<NomeAnimacao, string> = {
  parado: "Parado",
  andar: "Andar",
  treinando: "Treinando",
  comemorando: "Comemorando",
  alongar: "Alongar",
  olhar: "Olhar",
  arremessar: "Arremessar",
  receber: "Receber",
  aceitar: "Aceitar",
  recusar: "Recusar",
  provocar: "Provocar",
};

export function EditorAvatar() {
  const { data: usuario } = useSessao();
  const enviar = useEnviarFolha();
  const restaurar = useRestaurarFolha();
  const [escolhida, setEscolhida] = useState<Escolhida | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  // A prévia usa um endereço temporário, que precisa ser liberado.
  useEffect(
    () => () => {
      if (escolhida) URL.revokeObjectURL(escolhida.url);
    },
    [escolhida],
  );

  if (!usuario) return null;

  const escolher = async (arquivo: File | undefined) => {
    enviar.reset();
    restaurar.reset();
    setErro(null);
    if (!arquivo) return;
    try {
      const blob = await prepararFolha(arquivo);
      setEscolhida({ blob, url: URL.createObjectURL(blob) });
    } catch (e) {
      setEscolhida(null);
      setErro(e instanceof Error ? e.message : "Imagem inválida.");
    }
  };

  const salvar = () => {
    if (!escolhida) return;
    enviar.mutate(escolhida.blob, { onSuccess: () => setEscolhida(null) });
  };

  const mutacaoComErro = enviar.isError
    ? enviar.error
    : restaurar.isError
      ? restaurar.error
      : null;

  return (
    <div className="space-y-4">
      <div className="card flex items-center gap-4 p-5">
        <Avatar nome={usuario.nomeExibicao} id={usuario.id} size={72} />
        <div className="min-w-0">
          <p className="font-display text-xl">{usuario.nomeExibicao}</p>
          <p className="text-sm text-muted">
            Seu avatar aparece para quem divide um grupo com você.
          </p>
        </div>
      </div>

      <div className="card space-y-3 p-5">
        <label className="block text-sm font-semibold" htmlFor="folha-avatar">
          Importar sua folha de sprites
        </label>
        <p className="text-xs text-muted">
          PNG com fundo transparente, {largura}×{altura} px (8 colunas × 12
          linhas de 32 px) e até 64 KB.
        </p>
        <input
          id="folha-avatar"
          type="file"
          accept="image/png"
          className="block w-full text-sm"
          onChange={(evento) => void escolher(evento.target.files?.[0])}
        />
        {erro && (
          <p role="alert" className="form-error">
            {erro}
          </p>
        )}
        {mutacaoComErro && (
          <p role="alert" className="form-error">
            {getErrorMessage(mutacaoComErro)}
          </p>
        )}
        {enviar.isSuccess && !escolhida && (
          <p role="status" className="text-sm font-semibold text-progress">
            Avatar atualizado.
          </p>
        )}
      </div>

      {escolhida && (
        <div className="card space-y-4 p-5">
          <p className="text-sm font-semibold">Prévia de todas as linhas</p>
          <ul className="grid grid-cols-3 gap-3 sm:grid-cols-4">
            <li className="flex flex-col items-center gap-1">
              <span
                aria-hidden="true"
                className="avatar rounded-full"
                style={{
                  width: 64,
                  height: 64,
                  backgroundImage: `url(${escolhida.url})`,
                  backgroundSize: `${64 * 8}px ${64 * 12}px`,
                  backgroundPosition: "0 0",
                  imageRendering: "pixelated",
                }}
              />
              <span className="text-2xs text-muted">Retrato</span>
            </li>
            {(Object.keys(animacoes) as NomeAnimacao[]).map((nome) => (
              <li key={nome} className="flex flex-col items-center gap-1">
                <SpriteAvatar
                  folha={escolhida.url}
                  animacao={nome}
                  escala={2}
                />
                <span className="text-2xs text-muted">
                  {nomesDasLinhas[nome]}
                </span>
              </li>
            ))}
          </ul>
          <div className="flex gap-3">
            <button
              type="button"
              className="btn-primary flex-1"
              disabled={enviar.isPending}
              onClick={salvar}
            >
              {enviar.isPending ? "Salvando…" : "Salvar avatar"}
            </button>
            <button
              type="button"
              className="btn-ghost"
              disabled={enviar.isPending}
              onClick={() => setEscolhida(null)}
            >
              Cancelar
            </button>
          </div>
        </div>
      )}

      <button
        type="button"
        className="btn-secondary w-full justify-center"
        disabled={restaurar.isPending}
        onClick={() => restaurar.mutate()}
      >
        {restaurar.isPending ? "Voltando…" : "Voltar ao avatar padrão"}
      </button>
    </div>
  );
}
