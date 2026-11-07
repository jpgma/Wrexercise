import { zodResolver } from "@hookform/resolvers/zod";
import {
  Download,
  KeyRound,
  LogOut,
  Shield,
  Smartphone,
  User,
} from "lucide-react";
import { useState, useSyncExternalStore } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { PageHeader, SectionTitle } from "@/components/PageHeader";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import {
  useAlterarModoInicio,
  useAlterarNome,
  useAlterarSenha,
  useSair,
  useSessao,
} from "@/features/autenticacao";
import { getErrorMessage } from "@/services/api";
import {
  getModoInstalacao,
  instalar,
  subscribeInstalacao,
} from "@/services/instalacao";

const nomeSchema = z.object({
  nomeExibicao: z
    .string()
    .trim()
    .min(1, "Digite seu nome")
    .max(80, "Use até 80 caracteres"),
});

const senhaSchema = z
  .object({
    senhaAtual: z.string().min(1, "Digite sua senha atual"),
    novaSenha: z
      .string()
      .min(8, "Use pelo menos 8 caracteres")
      .max(128, "Use até 128 caracteres"),
    confirmacao: z.string(),
  })
  .refine(({ novaSenha, confirmacao }) => novaSenha === confirmacao, {
    message: "As senhas não são iguais",
    path: ["confirmacao"],
  });

type NomeData = z.infer<typeof nomeSchema>;
type SenhaData = z.infer<typeof senhaSchema>;

function NomeForm({ nomeAtual }: { nomeAtual: string }) {
  const alterar = useAlterarNome();
  const { showToast } = useToast();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<NomeData>({
    resolver: zodResolver(nomeSchema),
    defaultValues: { nomeExibicao: nomeAtual },
  });

  return (
    <form
      noValidate
      className="card space-y-3 p-5"
      onSubmit={(event) =>
        void handleSubmit(({ nomeExibicao }) =>
          alterar.mutate(nomeExibicao, {
            onSuccess: () => showToast("Nome atualizado."),
          }),
        )(event)
      }
    >
      <TextField
        label="Seu nome"
        icon={User}
        type="text"
        autoComplete="name"
        error={errors.nomeExibicao?.message}
        {...register("nomeExibicao")}
      />
      {alterar.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(alterar.error)}
        </p>
      )}
      <button
        type="submit"
        className="btn-secondary w-full"
        disabled={alterar.isPending}
      >
        {alterar.isPending ? "Salvando…" : "Salvar nome"}
      </button>
    </form>
  );
}

function SenhaForm() {
  const alterar = useAlterarSenha();
  const { showToast } = useToast();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<SenhaData>({ resolver: zodResolver(senhaSchema) });

  return (
    <form
      noValidate
      className="card space-y-3 p-5"
      onSubmit={(event) =>
        void handleSubmit(({ senhaAtual, novaSenha }) =>
          alterar.mutate(
            { senhaAtual, novaSenha },
            {
              onSuccess: () => {
                reset();
                showToast(
                  "Senha alterada. Os outros aparelhos foram desconectados.",
                );
              },
            },
          ),
        )(event)
      }
    >
      <TextField
        label="Senha atual"
        icon={KeyRound}
        type="password"
        autoComplete="current-password"
        error={errors.senhaAtual?.message}
        {...register("senhaAtual")}
      />
      <TextField
        label="Nova senha"
        icon={KeyRound}
        type="password"
        autoComplete="new-password"
        error={errors.novaSenha?.message}
        {...register("novaSenha")}
      />
      <TextField
        label="Confirme a nova senha"
        icon={KeyRound}
        type="password"
        autoComplete="new-password"
        error={errors.confirmacao?.message}
        {...register("confirmacao")}
      />
      <p className="text-xs text-muted">
        Ao alterar a senha, os outros aparelhos saem da sua conta.
      </p>
      {alterar.isError && (
        <p role="alert" className="form-error">
          {getErrorMessage(alterar.error)}
        </p>
      )}
      <button
        type="submit"
        className="btn-secondary w-full"
        disabled={alterar.isPending}
      >
        {alterar.isPending ? "Alterando…" : "Alterar senha"}
      </button>
    </form>
  );
}

const opcoesModo = [
  {
    valor: "App",
    titulo: "App",
    descricao: "Sua semana, o desafio e as atividades recentes.",
  },
  {
    valor: "Jogo",
    titulo: "Jogo",
    descricao: "A praça do grupo, com os avatares em cena.",
  },
] as const;

function TelaInicial({ modoAtual }: { modoAtual: "App" | "Jogo" }) {
  const alterar = useAlterarModoInicio();
  const { showToast } = useToast();

  return (
    <>
      <SectionTitle>Tela inicial</SectionTitle>
      <div
        role="radiogroup"
        aria-label="Tela inicial"
        className="card space-y-2 p-3"
      >
        {opcoesModo.map((opcao) => (
          <label
            key={opcao.valor}
            className="flex cursor-pointer items-start gap-3 rounded-lg p-2 has-[:checked]:bg-surface-muted"
          >
            <input
              type="radio"
              name="modo-inicio"
              className="mt-1"
              checked={modoAtual === opcao.valor}
              disabled={alterar.isPending}
              onChange={() =>
                alterar.mutate(opcao.valor, {
                  onSuccess: () =>
                    showToast(`Tela inicial: modo ${opcao.titulo}.`),
                })
              }
            />
            <span>
              <span className="block text-sm font-semibold">
                {opcao.titulo}
              </span>
              <span className="block text-sm text-muted">
                {opcao.descricao}
              </span>
            </span>
          </label>
        ))}
        {alterar.isError && (
          <p role="alert" className="form-error">
            {getErrorMessage(alterar.error)}
          </p>
        )}
      </div>
    </>
  );
}

function Instalacao() {
  const modo = useSyncExternalStore(subscribeInstalacao, getModoInstalacao);

  if (modo === null) return null;

  return (
    <>
      <SectionTitle>Aplicativo</SectionTitle>
      <div className="card space-y-3 p-5">
        <div className="flex gap-3 text-sm text-muted">
          <Smartphone
            aria-hidden="true"
            className="size-5 shrink-0 text-link"
          />
          <p>
            {modo === "botao"
              ? "Instale o Wrexercise para abrir direto da tela inicial, como um app."
              : "No iPhone ou iPad, toque em Compartilhar e depois em Adicionar à Tela de Início."}
          </p>
        </div>
        {modo === "botao" && (
          <button
            type="button"
            className="btn-secondary w-full"
            onClick={() => void instalar()}
          >
            <Download aria-hidden="true" className="size-4" />
            Instalar aplicativo
          </button>
        )}
      </div>
    </>
  );
}

export function ConfiguracoesPage() {
  const { data: usuario } = useSessao();
  const sair = useSair();
  const [confirmandoSair, setConfirmandoSair] = useState(false);

  if (!usuario) return null;

  return (
    <div className="mx-auto max-w-xl">
      <PageHeader title="Configurações" back="/perfil" />

      <NomeForm key={usuario.nomeExibicao} nomeAtual={usuario.nomeExibicao} />

      <TelaInicial modoAtual={usuario.modoInicio} />

      <SectionTitle>Senha</SectionTitle>
      <SenhaForm />

      <Instalacao />

      <SectionTitle>Privacidade</SectionTitle>
      <div className="card flex gap-3 p-4 text-sm text-muted">
        <Shield aria-hidden="true" className="size-5 shrink-0 text-link" />
        <p>
          Seus treinos e comprovantes ficam visíveis apenas para os membros dos
          seus grupos. Ninguém de fora vê nada.
        </p>
      </div>

      <button
        type="button"
        className="mt-6 flex h-12 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold text-danger hover:bg-danger-soft"
        onClick={() => setConfirmandoSair(true)}
      >
        <LogOut aria-hidden="true" className="size-4" />
        Sair da conta
      </button>

      <ConfirmDialog
        open={confirmandoSair}
        title="Sair da conta?"
        message="Você pode entrar de novo quando quiser. Seus treinos continuam salvos."
        confirmLabel="Sair"
        pending={sair.isPending}
        error={sair.isError ? getErrorMessage(sair.error) : undefined}
        onConfirm={() => sair.mutate()}
        onCancel={() => {
          sair.reset();
          setConfirmandoSair(false);
        }}
      />
    </div>
  );
}
