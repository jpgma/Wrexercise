# 0072 — Comemoração de conquista e o que falta para cada Enfeite

**Status:** DECIDIDA. Complementa `0018-perfil-e-pagina-do-membro.md` (conquistas calculadas na leitura) e `0037-avatar-importado-e-enfeites.md` (Enfeites e `enfeites_vistos`).

## Contexto

- As conquistas aparecem só numa seção do Perfil, e o que falta para cada Enfeite só aparecia em `/perfil/avatar`, como "Libera com {conquista}". Quem joga não via o que precisava fazer.
- Ganhar uma conquista não avisava ninguém. No modo Jogo, só um Enfeite **novo** tinha encenação; uma evolução (o kettlebell de bronze virar de prata) passava em branco. No modo App não havia aviso nenhum.
- As conquistas não ficam no banco (`0018`), e não há data de quando cada uma foi ganha. O que existe é `avatares.enfeites_vistos`, com os códigos dos Enfeites (a família) que a pessoa já viu.
- O pedido do mantenedor: ver com facilidade o que falta para cada Enfeite e dar mais destaque às conquistas, com pouco texto.

## Alternativas consideradas

- **Onde guardar o que já foi comemorado:**
  - **Reusar `enfeites_vistos`, guardando também o nível** (escolhida): sem migração, um mecanismo só, vale em todos os aparelhos.
  - **Cookie no aparelho:** não toca o servidor, mas cria um segundo mecanismo ao lado de `Novos` e repete a comemoração em cada aparelho.
  - **Tabela de conquistas ganhas, com data:** contraria a `0018` e pede migração e preenchimento retroativo.
- **Texto do que falta:** montado no frontend a partir de `atual` e `alvo`, ou vindo do servidor. O frontend não sabe a unidade de cada conquista ("treinos no fim de semana", "dias com treino"); o servidor já tem a regra.
- **Painel de conquistas na tela do Jogo:** proposto e não adotado agora. A tela inicial já é apertada em 390×844.

## Decisão

1. **O que falta, por extenso.** Cada conquista bloqueada devolve `falta`: "Falta 1 treino no fim de semana", "Faltam 3 dias com treino". O texto sai de `ConquistasUsuario`, com a unidade de cada conquista no singular e no plural, e segue para o Perfil e para os Enfeites bloqueados (`GET /api/jogo/avatar`).
2. **Enfeite bloqueado tocável.** Em `/perfil/avatar` e no próprio Foco, o Enfeite bloqueado é um botão que abre o que falta, o progresso e a conquista que o libera. No Foco de um colega, a Vitrine dele é um botão que abre o nome de cada Enfeite e, para os que quem vê ainda não tem, o que falta.
3. **`enfeites_vistos` guarda também o nível.** Ao marcar como visto, o servidor grava o código do Enfeite e o código da conquista do nível atual (`kettlebell` e `Treinos50`). A regra da Vitrine (`Novos`, por família) não muda.
4. **Conquista nova** (`conquistasNovas` em `GET /api/jogo/avatar`): a conquista do nível atual de um Enfeite liberado que não está em `enfeites_vistos`, quando o Enfeite nunca foi visto ou quando já há um nível dele guardado.
5. **Linhas antigas.** Um Enfeite visto antes desta decisão tem só a família guardada. Ele conta como comemorado: quem já joga não recebe de uma vez as comemorações de tudo o que tinha. A primeira evolução desse Enfeite só é comemorada depois que a linha for gravada de novo, com o nível.
6. **Comemoração.** Uma janela "Conquista liberada!", uma vez, nos dois modos. No modo Jogo, ela espera a entrada e as encenações da praça, mostra o Enfeite e leva ao próprio Foco ("Ver na Vitrine" ou "Pôr na Vitrine"). No modo App, mostra só a conquista e leva ao Perfil. Fechar chama `POST /api/jogo/enfeites/vistos`.
7. **Progresso ao postar.** A tela de "registrado" diz de qual Enfeite (ou conquista, no modo App) o treino aproxima a pessoa: "Caixa de som: 9/10 quando o grupo verificar esse treino." É calculado no navegador, só para as conquistas que contam por atividade (tipo, fim de semana, antes das 7h, a partir das 20h).

## Impacto

- **Sem migração.** `enfeites_vistos` continua `text[]`; passa a ter também códigos de conquista.
- **Quem está no modo App e nunca abriu a praça** não tem linha em `avatares`. Na primeira visita depois desta mudança, a janela lista de uma vez as conquistas que a pessoa já tem. Acontece uma vez só.
- **Backend:** `ConquistasUsuario` (unidades e `Falta`), `Contracts/ConquistasDoUsuario` (`ProgressoConquista.Falta`), `Enfeites` (`Vistos`, `ConquistasNovas`), `Vitrines` e `MeuAvatarResponse.ConquistasNovas`.
- **Frontend:** `CelebracaoConquista`, `avancoEnfeite`, `EditorVitrine` (`Bloqueado` vira botão), `PainelFoco` (a Vitrine do colega) e a página de registro.
- **Limites do progresso ao postar:** o mapa de tipo para conquista fica repetido no frontend, e o dia e a hora usam o fuso do aparelho. É só uma orientação; a contagem real é a do servidor. Conquistas por dia (`Treinos`), `Ecletico` e as de sequência ficam de fora.
- **Documentação:** `gamificacao.md` §9.2 e `funcionalidades.md` §3.3.6, §7.1 e §10.3.
