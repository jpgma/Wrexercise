-- G4 (decisão 0036): a MUDANÇA DE PLANOS troca o objetivo da Jogada por um equivalente.
-- Nulo: o objetivo é o da carta jogada.
ALTER TABLE jogadas ADD COLUMN codigo_objetivo text CHECK (codigo_objetivo IN (
    'Mais10Min', 'Mais20Min', 'Variedade', 'FimDeSemana', 'AntesDas8'));

-- No máximo um DESAFIO DUPLO por Jogada.
CREATE UNIQUE INDEX jogadas_origem_id ON jogadas (origem_id);
