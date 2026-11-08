# 0037 — Avatar importado, Enfeites e vitrine

**Status:** DECIDIDA. Complementa `0024-cartas-e-avatar-importado.md`. Detalhes em `../gamificacao.md` §4.3, §9 e §10 (G6).

## Contexto

A G6 deixa cada pessoa importar a própria folha de sprites e libera Enfeites a partir das conquistas. Ao implementá-la, alguns pontos de `gamificacao.md` não cabiam como estavam:

- O mapa de avatares era por grupo (`GET /api/grupos/{id}/avatares`), mas o componente `Avatar` aparece fora de qualquer grupo (Perfil, notificações, convite).
- A vitrine foi descrita como "lista de códigos de conquista", mas a planta e a medalha evoluem: guardar a conquista faria a vitrine perder o lugar a cada nível novo.
- Os Enfeites passam a ser visíveis aos colegas, e hoje as conquistas aparecem só no próprio Perfil (`0018-perfil-e-pagina-do-membro.md`).
- O limite de 20 atividades por tipo parecia alto para tipos raros, como `Natacao`.
- Um arquivo enviado por qualquer pessoa precisa de proteção contra imagens impróprias e contra formatos maliciosos.

## Alternativas consideradas

- **Mapa de avatares:** por grupo (como estava) ou um mapa só para o aplicativo. O por grupo obriga cada tela a conhecer um grupo e repete a busca.
- **Vitrine:** guardar códigos de conquista, ou códigos de Enfeite (a família).
- **Vitrine visível:** só para o dono, ou para os colegas no modo Jogo, com opção de esvaziar.
- **Limite por tipo:** 20 (como proposto), 10 (igual aos hábitos).
- **Armazenamento:** bytes no PostgreSQL ou arquivo em disco.

## Decisão

1. **Um mapa global:** `GET /api/jogo/avatares` devolve, para quem consulta e para cada membro ativo dos seus grupos, a folha importada (se houver) e a vitrine. O frontend busca uma vez e todos os `Avatar` usam o resultado.
2. **A vitrine guarda o código do Enfeite**, não o da conquista. A evolução (planta e medalha) mantém o lugar. Os Enfeites liberados que a pessoa ainda não viu (`enfeites_vistos`) entram sozinhos nas vagas; um Enfeite tirado e já visto não volta. Códigos que não valem mais são ignorados na leitura.
3. **A vitrine é visível aos colegas, só para quem está no modo Jogo.** Quem está no modo App não expõe nada, e qualquer pessoa pode esvaziar a vitrine. As conquistas continuam visíveis só no próprio Perfil.
4. **Limite de 10 atividades por tipo**, igual ao dos hábitos. As conquistas e os limites ficam em constantes.
5. **Arquivo em disco** (`/dados/avatares`), com nome aleatório gerado pelo servidor. O servidor valida de novo o PNG (256×384, RGBA de 8 bits, até 64 KB, sem APNG), e o navegador o regrava antes de enviar. A folha só é entregue ao dono e a quem divide um grupo ativo com ele (404 para os demais), com `nosniff` e cache privado imutável.
6. **Redefinição:** o `Proprietario` ou um `Administrador` do grupo apaga a folha importada de um membro. Não há aviso ao membro nesta fase: ele exigiria um tipo novo de notificação.
7. **Regra compartilhada de desafio concluído:** passa para `Features/Progresso/Contracts/DesafiosDaPessoa.cs`, usada pela Energia e pelas conquistas.

## Impacto

- Migração `018-create-avatares.sql`: tabela `avatares` (`arquivo`, `vitrine`, `enfeites_vistos`).
- `DataSourceFactory` habilita arrays no Npgsql.
- Novo volume `avatares` no `compose.yaml` e no Termux, incluído em `backup.sh` (`avatares-AAAAMMDD-HHMMSS.tar.gz`). Backups antigos, sem esse arquivo, ainda restauram.
- 12 conquistas novas e 19 imagens em `pixel-art/enfeites/` (CC0).
- O mapa calcula a vitrine por pessoa, como as conquistas (sem tabela de conquistas). Com os grupos pequenos do aplicativo, o custo é aceitável; o cache do TanStack guarda o mapa por 5 minutos.
- `gamificacao.md` e `funcionalidades.md` (§3.3.5, §3.3.6, §10.3) foram atualizados; `Vitrine` entrou no glossário.
