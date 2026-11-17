-- Fusão (decisão 0070): duas Provocações numa Jogada só. A segunda carta fica aqui; nulo fora da fusão.
-- A duração-alvo e o tipo a evitar continuam nas colunas de sempre, já somados para as duas cartas.
ALTER TABLE jogadas ADD COLUMN carta_fusao_id uuid REFERENCES cartas (id);
ALTER TABLE jogadas ADD CONSTRAINT jogadas_fusao_outra_carta_check CHECK (carta_fusao_id <> carta_id);
-- A MUDANÇA DE PLANOS não vale numa fusão.
ALTER TABLE jogadas ADD CONSTRAINT jogadas_fusao_sem_mudanca_check
    CHECK (carta_fusao_id IS NULL OR codigo_objetivo IS NULL);

CREATE INDEX jogadas_carta_fusao_id ON jogadas (carta_fusao_id);
