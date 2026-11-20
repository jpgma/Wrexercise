# 0077 — Versões do app e Novidades

**Status:** DECIDIDA. Acrescenta a `0021-pwa-e-service-worker.md` o que a pessoa vê depois de tocar em "Atualizar". O `__VERSAO__` do service worker continua sendo só o hash do pré-cache. O `versao.txt` da implantação (`0046`) passa a ter a versão do app antes da etiqueta da imagem.

## Contexto

- Antes, uma implantação só mostrava "Nova versão disponível · Atualizar". Ninguém sabia o que tinha mudado.
- O mantenedor pediu que o app tenha uma versão, começando em `1.0.0`, e que a cada atualização as pessoas vejam o que mudou em tópicos curtos, claros e descontraídos, em pt-BR.
- Não há CI, tags nem processo de lançamento. A implantação sai de `run.bat` direto da `main`.

## Alternativas consideradas

- **Coluna `versao_vista` em `usuarios`:** mostraria as novidades uma vez por conta, e não por aparelho. Pediria migração, endpoint e testes de integração para um aviso que não é crítico. Descartada: um cookie comum basta, como o passeio do Jogo (`wx_tutorial_jogo`).
- **Subir a versão a cada implantação:** obrigaria a escrever novidades vazias quando nada visível muda. Descartada: a versão só muda quando a pessoa percebe.
- **Versão em `package.json`, numa etiqueta do git ou no `.csproj`:** seriam duas fontes para o mesmo número, ou exigiriam o git no build. Descartada: o `versoes.json` é a fonte única. A etiqueta do git existe, mas só marca o commit implantado; ela sai do `versoes.json`, e nunca o contrário.
- **Criar a etiqueta do git à mão:** fácil de esquecer, e marcaria commits que talvez nunca fossem implantados. Descartada: o `run.bat --ssh` cria a etiqueta depois de implantar.
- **`CHANGELOG.md` na raiz, lido no build:** o texto é para quem usa o app, não para quem mantém o código, e transformar Markdown em dados pediria código a mais. Descartada.
- **Só um selo na tela de Configurações, sem diálogo:** quase ninguém veria. Descartada.

## Decisão

- `Wrexercise/webapp/src/features/novidades/versoes.json` guarda todas as versões, da mais nova para a mais antiga: `versao` (`X.Y.Z`), `data` (`AAAA-MM-DD`) e `novidades` (1 a 6 tópicos, até 80 caracteres, sem ponto final). A primeira entrada é a versão atual.
- Versionamento semântico pelo que a pessoa percebe: patch para correção, minor para funcionalidade ou regra nova, major para mudança grande. Regras de escrita em "Versões e novidades" no `AGENTS.md`.
- Na primeira abertura depois da atualização, com sessão, o diálogo "Novidades" lista as versões que a pessoa ainda não viu neste aparelho (no máximo 5) e fecha com "Bora!". Sem registro no aparelho, ele mostra só a versão atual. O cookie `wx_versao_vista` guarda a última versão vista.
- O diálogo espera a Chegada (`0076`): enquanto há carta sem resposta, ou enquanto a lista de cartas carrega, ele não abre.
- Configurações tem a linha "Novidades", com a versão atual, que abre a página `/perfil/novidades` com o histórico inteiro.
- `VersoesTests`, em `Wrexercise.UnitTests`, verifica o arquivo, porque o frontend não tem executor de testes.
- `run.bat --ssh` lê a versão do `versoes.json` e grava `X.Y.Z wrexercise:<commit>-<arquitetura>` no `versao.txt` do celular. Assim os backups diários dizem qual versão estava no ar.
- Depois de uma implantação bem-sucedida, sem alterações fora de commit, o `run.bat --ssh` cria a etiqueta anotada `vX.Y.Z` no commit implantado e a envia ao `origin`, se ela ainda não existe. A etiqueta marca a primeira implantação de cada versão. Uma implantação seguinte sem versão nova não move a etiqueta. Se criar ou enviar a etiqueta falha, a implantação continua valendo e o `run.bat` só avisa.

## Impacto

- Toda alteração que o participante percebe atualiza o `versoes.json` no mesmo commit.
- Quem usa dois aparelhos vê as novidades uma vez em cada um.
- Sem migração, endpoint ou dependência nova.
- `git tag -l` lista as versões implantadas, e `git log v1.0.0..v1.1.0` mostra o que entrou entre duas delas.
