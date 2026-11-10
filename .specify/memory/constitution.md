# Constituição do Wrexercise

Esta constituição não repete as regras do projeto: ela aponta para as fontes que já valem para qualquer trabalho no repositório. Em caso de conflito, as fontes abaixo prevalecem sobre os modelos do Spec Kit.

## Fontes de verdade

- `AGENTS.md`: regras do repositório, idioma, regra de nomes, glossário do domínio, organização, proibições, segurança, dependências e commits.
- `Wrexercise/AGENTS.md` e `Wrexercise/webapp/AGENTS.md`: regras de cada stack.
- `docs/arquitetura.md`: arquitetura, fases de implementação (§18) e itens fora do escopo (§19).
- `docs/funcionalidades.md`: resumo de todas as funcionalidades (inglês ASD-STE100).
- `docs/questoes-abertas.md` e `docs/decisoes/`: questões pendentes e ADRs.
- `docs/gamificacao.md` e `docs/avatar-kit.md`: modo `Jogo` e arte em pixel.

## Princípios

### I. Simplicidade

Simples o suficiente para uma única pessoa entender, implantar e manter. Entre duas alternativas que atendem aos requisitos, escolha a mais simples. Não crie pastas, arquivos ou dependências antes de precisar deles.

### II. Fatias verticais

Monólito modular. Cada funcionalidade é dona de seus endpoints, contratos, SQL, regras e testes, e só cria a fatia na fase correspondente de `docs/arquitetura.md` §18. Uma fatia não acessa detalhes internos de outra.

### III. Decisões registradas

Planos não reabrem decisões de `docs/arquitetura.md` sem justificativa técnica concreta. Um plano que muda a arquitetura ou fecha uma questão aberta inclui um ADR em `docs/decisoes/`. Tudo o que `AGENTS.md` proíbe sem decisão registrada continua proibido nos planos.

### IV. Segurança

Todo endpoint privado verifica autenticação e pertencimento ao grupo no backend. SQL sempre parametrizado. Nada de segredos no repositório nem dados sensíveis em logs.

### V. Verificação

Nenhuma tarefa está concluída sem `scripts/verify.sh` passando (`--full` ao mexer em SQL, migrações, `Program.cs`, `deploy/` ou dependências). Nunca enfraqueça uma verificação para fazê-la passar.

## Uso do Spec Kit neste repositório

- Especificações, planos e tarefas ficam em `specs/NNN-nome/` e são escritos em pt-BR com acentos; nomes no código seguem a regra de nomes e o glossário de `AGENTS.md`.
- Trabalhe direto em `main`. Não instale a extensão `git` do Spec Kit nem crie branches ou worktrees sem pedido explícito do mantenedor.
- Toda funcionalidade implementada a partir de uma especificação atualiza `docs/funcionalidades.md` e a documentação afetada na mesma alteração.

## Governança

Esta constituição muda junto com `AGENTS.md`: se uma regra mudar lá, atualize aqui só o que deixar de apontar para o lugar certo.

**Version**: 1.0.0 | **Ratified**: 2026-11-10 | **Last Amended**: 2026-11-10
