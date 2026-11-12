# Seed de testes

**É o seed para testar o aplicativo inteiro.** Ele cria no banco local do `run.bat` um grupo com 4 contas e 20 semanas de histórico verificado. Assim, Energia, conquistas, Enfeites, vitrines, avatares, Classificacao, Progresso e Feed já têm dados. A Loja, as Cartas, as Jogadas e os Rivais começam vazios: use a Energia das contas para testá-los.

Só para testes locais. As senhas abaixo não são segredo.

```powershell
.\run.bat           # a pilha precisa estar no ar, com as migrações aplicadas
.\seed.bat          # apaga o seed anterior, se houver, e cria tudo de novo
.\seed.bat --reset  # apaga as Jogadas do grupo do seed
```

## Contas

Grupo **Seed de testes** (fuso `America/Sao_Paulo`). As senhas ficam aqui de propósito: valem só para estas contas de teste no banco local.

| E-mail | Senha | Papel | Perfil |
|---|---|---|---|
| `ana@seed.test` | `wrexercise` | Proprietario | Madrugadora; corrida, musculação e ioga. |
| `bruno@seed.test` | `wrexercise` | Administrador | Treina à noite; musculação, calistenia e esporte. |
| `carla@seed.test` | `wrexercise` | Membro | Ciclismo, natação e ioga. |
| `diego@seed.test` | `wrexercise` | Membro | Falhou uma semana do desafio encerrado: sem `DesafioCompleto` e só `Sequencia4`. Vitrine e Enfeites vistos vazios, para testar os Enfeites "novos". |

Cada conta tem cerca de 2.200 a 2.700 de Energia e entre 12 e 15 conquistas. As conquistas mudam de uma conta para outra, então cada conta tem Enfeites liberados e bloqueados diferentes. Cada conta tem uma folha de avatar tingida diferente.

## O que o seed cria

- **Desafios:** "Projeto verão", encerrado, com 14 semanas, 3 treinos de 30 min. "Rumo aos 100", ativo, com 12 semanas, 4 dias de 40 min e a meta por tipo "Pedal ou corrida" (25 min).
- **Atividades:** no máximo uma por dia e pessoa, todas `Verificada` e `RegistroManual`, de 20 semanas atrás até ontem. Segunda, terça, quinta e sábado são obrigatórios e cumprem as metas dos dois desafios. Nos outros dias, a atividade é sorteada. Parte das atividades tem "deu moral" dos colegas.
- **Avatares:** `scripts/seed/avatares/5eed…000N.png`, copiados para `%LOCALAPPDATA%\Wrexercise\local\avatares`.

O sorteio é fixo (vem de `md5`), então o seed é reproduzível. As datas são relativas a hoje: rodar de novo mantém o desafio ativo e as sequências em dia.

## Recriar e `--reset`

- `seed.bat` apaga as contas do seed (pelo id ou pelo e-mail), os grupos que elas criaram e tudo o que aponta para eles. Isso inclui Cartas, Jogadas, Rivais, Notificacoes e o que você criou nesses grupos durante os testes. Outras contas e grupos não mudam.
- `seed.bat --reset` volta o grupo ao estado semeado, que não tem Jogadas: apaga todas as Jogadas do grupo, em andamento ou não. As Cartas usadas voltam para a Mão, e a Energia ganha com Jogadas concluídas some.

## Arquivos

| Arquivo | Papel |
|---|---|
| `seed.bat` (raiz) | Copia os avatares e roda o SQL no contêiner `wrexercise-local-db-1`. |
| `scripts/seed/seed.sql` | Apaga e recria o seed, numa transação. |
| `scripts/seed/reset-jogadas.sql` | O `--reset`. |
| `scripts/seed/avatares/` | Folhas tingidas, geradas por `python pixel-art/seed-avatares/build.py`. Não edite os PNG à mão. |

Ao mudar o esquema, as regras de Energia ou as conquistas, confira se o seed ainda roda e ainda libera o que este arquivo descreve.
