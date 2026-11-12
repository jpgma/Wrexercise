-- seed.bat --reset: volta o grupo do seed ao estado semeado, que não tem Jogadas.
-- Apaga todas as Jogadas do grupo, em andamento ou não; os avisos delas saem em cascata.
-- As Cartas usadas voltam para a Mão, porque o estado da carta é calculado a partir das Jogadas.
-- A Energia ganha com Jogadas concluídas some junto, como no estado semeado.
-- Um só DELETE: a Jogada original e o DESAFIO DUPLO que aponta para ela saem juntos.
DELETE FROM jogadas WHERE grupo_id = '5eed0000-0000-4000-8000-0000000000a0';
