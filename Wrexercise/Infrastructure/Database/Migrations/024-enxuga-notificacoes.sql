-- ADR 0048: "seu treino foi verificado" e o aviso de Jogada concluída para quem cumpriu deixam de existir.
-- Apaga as linhas antigas desses tipos para a lista não mostrar avisos que o sistema não gera mais.
DELETE FROM notificacoes WHERE tipo = 'SituacaoAlterada' AND situacao = 'Verificada';
DELETE FROM notificacoes WHERE tipo = 'JogadaConcluida' AND ator_id IS NULL;
