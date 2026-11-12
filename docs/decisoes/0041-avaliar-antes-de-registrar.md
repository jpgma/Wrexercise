# 0041 — Avaliar antes de registrar

**Status:** DECIDIDA. Complementa `0012-verificacao-comunitaria.md` e `0015-reacoes-em-revisao.md`. O item 4 e o retorno automático ao registro do item 5 foram substituídos por `0047-fila-antes-do-formulario.md`.

## Contexto

Uma atividade sem reação negativa só fica `Verificada` quando todos os revisores reagem ou quando a janela de 48 horas fecha. Na prática, muita gente registra o próprio treino e não avalia o dos outros, e as atividades passam dois dias em `AguardandoFeedback`. O progresso, a classificação e o Jogo só contam o que é definitivo, então a demora pesa em todo o grupo.

## Alternativas consideradas

- **Só um aviso no frontend**, com opção de pular. Fácil de ignorar, e um cliente qualquer contornaria a regra.
- **Prazo de carência** (bloquear só o que espera há mais de X horas). Deixaria registrar logo depois de um amigo, mas acrescenta uma constante e um caso a explicar.
- **Opção por grupo.** Pede coluna, migração e tela de configuração para uma regra que todos os grupos querem.
- **Recompensa no Jogo** (Energia por avaliação). Incentiva, mas não garante a avaliação, e mexe nas regras de Energia (`0033`). Pode vir depois, separada.

## Decisão

1. **Quem tem atividades na fila de avaliação do grupo não registra uma nova atividade nesse grupo.** A fila é a mesma de `GET /api/grupos/{id}/atividades/para-avaliar`: atividades em `AguardandoFeedback` ou `EmRevisao`, dentro da janela de 48 horas, em que a pessoa é revisora e ainda não reagiu. O filtro SQL fica num lugar só (`ConsultasAtividade.FiltroParaAvaliar`).
2. **O backend aplica a regra.** `POST /api/grupos/{id}/atividades` responde `409 AVALIACOES_PENDENTES`. A verificação roda na mesma transação da criação.
3. **Qualquer reação conta como avaliação**: 💪 `Kudos`, 🤨 `Suspeito` ou ❌ `Rejeitar`. Exigir Kudos empurraria a pessoa a aprovar sem olhar.
4. **Interface:** a tela de registro mostra um aviso acima do formulário, com "Começar avaliação", e o botão de envio vira "Avaliar e enviar". Os dois abrem a fila (`?depois=registrar`) e guardam o formulário, inclusive as imagens, num rascunho em memória (`features/atividades/rascunho.ts`); não há armazenamento no navegador. Ao fim da fila, o aplicativo volta ao formulário preenchido e, se a pessoa tocou em "Avaliar e enviar", envia sozinho. Se a fila não carregar, o formulário funciona normalmente e a API decide.
5. **Avanço automático:** depois de uma reação na tela de avaliação, uma barra enche em 3 segundos e o aplicativo abre a próxima atividade (ou volta ao registro). "Ficar aqui" e o foco no campo de comentário param o avanço, para não atrapalhar quem quer explicar um "Suspeito". A próxima atividade é carregada durante a contagem, para a troca não mostrar tela de carregamento.

## Impacto

- Ninguém fica bloqueado para sempre: a janela de 48 horas limita a fila.
- Quem entrou no grupo depois de uma atividade não é revisor dela e não é bloqueado. Quem está sozinho no grupo nunca é bloqueado.
- Atividades em `EmRevisao` depois da janela continuam esperando a decisão dos administradores (`0015`); esta regra não muda isso.
- Testes de integração que registram atividades de várias pessoas no mesmo grupo passam a avaliar a fila antes (`ApiTestHost.AvaliarFilaAsync`).
