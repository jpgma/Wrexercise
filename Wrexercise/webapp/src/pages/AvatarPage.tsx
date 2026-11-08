import { PageHeader } from "@/components/PageHeader";
import { EditorAvatar, EditorVitrine } from "@/features/jogo";

export function AvatarPage() {
  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="Avatar"
        subtitle="Importe a sua folha de sprites"
        back="/perfil"
      />
      <div className="space-y-4">
        <EditorAvatar />
        <EditorVitrine />
      </div>
    </div>
  );
}
