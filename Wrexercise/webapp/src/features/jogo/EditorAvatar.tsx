import { useEffect, useRef, useState } from "react";
import { Avatar } from "@/components/Avatar";
import { folhaPadrao } from "@/components/folhaAvatar";
import { useSessao } from "@/features/autenticacao";
import { getErrorMessage } from "@/services/api";
import { direcoes, type NomeAnimacao } from "./animacoes";
import { folhaImportada, prepararFolha } from "./folhaPng";
import { gerarGuiaFolha } from "./guiaFolha";
import { SpriteAvatar } from "./SpriteAvatar";
import { useMovimentoReduzido } from "./useMovimentoReduzido";
import { useEnviarFolha, useMeuAvatar, useRestaurarFolha } from "./useAvatares";

type Escolhida = { blob: Blob; url: string };

/** As animações com arte própria na folha; as outras usam `parado` (ADR 0039, Andamento). */
const animacoesDaPrevia: { nome: NomeAnimacao; rotulo: string }[] = [
  { nome: "parado", rotulo: "Parado" },
  { nome: "andar", rotulo: "Andar" },
  { nome: "treinando", rotulo: "Correr" },
  { nome: "comemorando", rotulo: "Comemorar" },
  { nome: "arremessar", rotulo: "Arremessar" },
  { nome: "receber", rotulo: "Receber" },
];

/** Baixa um arquivo gerado no navegador. */
function baixar(blob: Blob, nome: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = nome;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** As 8 direções de uma folha, na mesma animação. As que tocam uma vez recomeçam ao terminar. */
function PreviaDirecoes({
  folha,
  animacao,
  rotulo,
}: {
  folha: string;
  animacao: NomeAnimacao;
  rotulo: string;
}) {
  const reduzido = useMovimentoReduzido();
  const [volta, setVolta] = useState(0);
  // Em 2× cabem só 2 por linha num celular; em 1×, as 8 direções ocupam 2 linhas.
  const [escala] = useState(() =>
    window.matchMedia("(min-width: 640px)").matches ? 2 : 1,
  );
  return (
    <ul className="flex flex-wrap justify-center" aria-label={rotulo}>
      {direcoes.map((direcao, indice) => (
        <li key={direcao}>
          <SpriteAvatar
            key={`${animacao}-${volta}`}
            folha={folha}
            animacao={animacao}
            direcao={direcao}
            escala={escala}
            estatico={reduzido}
            onFim={indice === 0 ? () => setVolta((v) => v + 1) : undefined}
          />
        </li>
      ))}
    </ul>
  );
}

/**
 * A folha de avatar: exportar a atual, editar fora do aplicativo e importar de volta. O aplicativo
 * só confere o tamanho (docs/avatar-formato.md §6); o desenho é livre.
 */
export function EditorAvatar() {
  const { data: usuario } = useSessao();
  const { data: meuAvatar } = useMeuAvatar();
  const enviar = useEnviarFolha();
  const restaurar = useRestaurarFolha();
  const entrada = useRef<HTMLInputElement>(null);
  const [escolhida, setEscolhida] = useState<Escolhida | null>(null);
  const [animacao, setAnimacao] = useState<NomeAnimacao>("parado");
  const [erro, setErro] = useState<string | null>(null);

  // A prévia usa um endereço temporário, que precisa ser liberado.
  useEffect(
    () => () => {
      if (escolhida) URL.revokeObjectURL(escolhida.url);
    },
    [escolhida],
  );

  if (!usuario) return null;

  const importada = meuAvatar?.folha ?? null;
  const atual = importada ?? folhaPadrao();

  const limparMensagens = () => {
    enviar.reset();
    restaurar.reset();
    setErro(null);
  };

  const escolher = async (arquivo: File | undefined) => {
    limparMensagens();
    if (!arquivo) return;
    try {
      const blob = await prepararFolha(arquivo);
      setEscolhida({ blob, url: URL.createObjectURL(blob) });
    } catch (e) {
      setEscolhida(null);
      setErro(e instanceof Error ? e.message : "Imagem inválida.");
    }
  };

  const exportarGuia = async () => {
    limparMensagens();
    try {
      baixar(await gerarGuiaFolha(), "guia-avatar.png");
    } catch (e) {
      setErro(
        e instanceof Error ? e.message : "Não foi possível gerar o guia.",
      );
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
            {importada
              ? "Você usa uma folha importada."
              : "Você usa o avatar padrão."}{" "}
            Seu avatar aparece para quem divide um grupo com você.
          </p>
        </div>
      </div>

      <div className="card space-y-4 p-5">
        <div className="space-y-1">
          <p className="text-sm font-semibold">Sua folha de sprites</p>
          <p className="text-xs text-muted">
            Exporte a folha, edite no seu editor de pixel art e importe de
            volta. Ela precisa ter {folhaImportada.largura}×
            {folhaImportada.altura} px e até 1000 KB. O desenho é livre. O guia
            mostra a grade, o nome de cada linha e o ponto entre os pés.
          </p>
        </div>

        <label className="block space-y-1">
          <span className="input-label">Animação da prévia</span>
          <select
            className="input-field"
            value={animacao}
            onChange={(evento) =>
              setAnimacao(evento.target.value as NomeAnimacao)
            }
          >
            {animacoesDaPrevia.map(({ nome, rotulo }) => (
              <option key={nome} value={nome}>
                {rotulo}
              </option>
            ))}
          </select>
        </label>

        {!escolhida && (
          <PreviaDirecoes
            folha={atual}
            animacao={animacao}
            rotulo="Folha atual"
          />
        )}

        <div className="grid grid-cols-2 gap-3">
          <a
            className="btn-secondary justify-center"
            href={atual}
            download="folha-avatar.png"
            onClick={limparMensagens}
          >
            Exportar folha
          </a>
          <button
            type="button"
            className="btn-secondary justify-center"
            onClick={() => void exportarGuia()}
          >
            Exportar guia
          </button>
        </div>

        <input
          ref={entrada}
          type="file"
          accept="image/png"
          className="sr-only"
          tabIndex={-1}
          aria-label="Arquivo da folha de sprites"
          onChange={(evento) => {
            void escolher(evento.target.files?.[0]);
            evento.target.value = "";
          }}
        />
        <button
          type="button"
          className="btn-primary w-full justify-center"
          disabled={enviar.isPending}
          onClick={() => entrada.current?.click()}
        >
          Importar folha
        </button>

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
          <p className="text-sm font-semibold">Prévia da folha nova</p>
          <PreviaDirecoes
            folha={escolhida.url}
            animacao={animacao}
            rotulo="Prévia da folha nova"
          />
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

      {importada && (
        <button
          type="button"
          className="btn-secondary w-full justify-center"
          disabled={restaurar.isPending}
          onClick={() => {
            limparMensagens();
            restaurar.mutate();
          }}
        >
          {restaurar.isPending ? "Voltando…" : "Voltar ao avatar padrão"}
        </button>
      )}
    </div>
  );
}
