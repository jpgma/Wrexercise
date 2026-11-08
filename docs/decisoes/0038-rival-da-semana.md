# 0038 — Rival da semana

**Status:** DECIDIDA. Complementa `0023-gamificacao.md` e `0033-regras-de-energia-e-jogadas.md`. Detalhes em `../gamificacao.md` §8 e §10 (G7).

## Contexto

A G7 deixa cada pessoa escolher um Rival por semana: vence quem cumprir a meta primeiro, "pelo horário de verificação da sessão que completou a meta". Ao implementá-la, alguns pontos de `gamificacao.md` §8 não fechavam:

- `atividades` não guardava quando a atividade ficou `Verificada`: só `started_at`, `submitted_at` e `created_at`.
- Sem limite de quando escolher, quem já cumpriu a meta podia escolher um colega que ainda não cumpriu e ganhar os 15 EN de graça.
- §8 fala em um Rival "por semana no grupo", e §5.1 diz que estar em mais grupos não multiplica a Energia.
- Não estava dito se o Rival fica sabendo, nem se quem está no modo App pode ser escolhido.
- §1.3 fala de um selo "com o número da posição, como placar", sem dizer qual posição.

## Alternativas consideradas

- **Horário que decide:** uma coluna nova `verified_at`; o `created_at` da sessão que completou a meta (sem migração, mas o resultado só seria final depois do domingo mais a janela de verificação); ou o `started_at` (permite registrar com data passada para vencer).
- **Quando escolher:** só enquanto ninguém dos dois cumpriu a meta, travado depois; até quarta-feira, com troca; ou em qualquer dia, travado.
- **Vários grupos:** um Rival por semana no total; um por grupo, pago uma vez; ou um por grupo, pago em cada um.
- **Visibilidade:** só modo Jogo, com aviso; só modo Jogo, sem aviso; ou qualquer membro, em segredo.

## Decisão

1. **`atividades.verified_at`.** Toda escrita que deixa uma atividade `Verificada` grava `now()` na mesma instrução. Vence quem tem o `verified_at` mais antigo na sessão que completou a meta (a N-ésima atividade verificada que conta para o desafio na semana, com N = sessões por semana). Como `Verificada` é final e quem cumprir depois recebe um horário mais novo, Venceu e Perdeu são finais assim que aparecem, e a Energia continua só crescendo. Empate exato: sem vencedor. Ninguém cumpriu: sem vencedor, depois do domingo mais 2 dias (a janela de verificação de 48 horas).
2. **A escolha só vale antes de alguém dos dois cumprir a meta**, contando as atividades ainda sem veredito. Depois de escolhido, o Rival não muda nem pode ser cancelado.
3. **Um Rival por grupo e por semana, e cada vitória paga 15 EN.** É uma exceção consciente a §5.1: o Rival exige uma escolha e uma disputa em cada grupo, e não sai do mesmo treino registrado duas vezes. O limite real é um por grupo, e os grupos são poucos.
4. **Só quem está no modo Jogo escolhe e é escolhido.** O Rival recebe a `Notificacao` `RivalEscolhido` e vê a provocação na Cena. Só as duas pessoas do par veem o par: os demais membros não veem nada. Quem está no modo App não percebe diferença.
5. **A semana é a do desafio em destaque**, que precisa estar `Ativo`. As duas pessoas precisam participar da semana (entraram até a segunda-feira). Um desafio cancelado depois da escolha deixa o Rival sem efeito.
6. **O selo** é um `badge-*` no painel do Rival, embaixo da praça: `RIVAL` durante a semana e `VENCEU · +15 EN` para quem escolheu e venceu. O placar é o total de vitórias da pessoa no grupo. Não há arte nova nem Conquista nova.
7. **Encenações:** quem escolhe vê a provocação na hora; o Rival vê na primeira visita (`vista_em`); quem escolheu vê o vencedor comemorar quando o resultado é final (`resultado_visto_em`). Contam no limite de 3 por visita.

## Impacto

- Migração `019-create-rivais.sql`: coluna `atividades.verified_at`, com as verificadas antigas preenchidas pela última revisão ou pelo envio, e `CHECK` que exige o horário em toda atividade `Verificada`; tabela `rivais`; `notificacoes.rival_id` e o tipo `RivalEscolhido`.
- `CriarAtividade`, `AnexarEvidencia`, `VerificacaoAtividade` e `RevisarAtividade` gravam `verified_at`.
- Rotas `GET` e `POST /api/grupos/{id}/rivais` e `POST /api/grupos/{id}/rivais/vistos`. A Energia ganha a origem `rivais`.
- O resultado é calculado na leitura, como as Jogadas: sem tarefa agendada.
- `gamificacao.md` e `funcionalidades.md` (§3.3.3, §3.3.7 e §11) foram atualizados.
