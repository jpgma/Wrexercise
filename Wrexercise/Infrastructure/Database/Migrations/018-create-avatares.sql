-- G6 (docs/gamificacao.md §4.3 e §9.2): o avatar importado e a vitrine de Enfeites.
-- Uma linha por usuário, criada só quando a pessoa muda algo. Sem linha, vale o avatar padrão.
CREATE TABLE avatares (
    usuario_id uuid PRIMARY KEY REFERENCES usuarios (id) ON DELETE CASCADE,
    -- Nome aleatório do PNG em /dados/avatares/. Nulo: avatar padrão.
    arquivo text CHECK (arquivo ~ '^[0-9a-f]{32}\.png$'),
    -- Códigos dos Enfeites, na ordem em que aparecem (até 3). O primeiro fica ao lado do avatar na Cena.
    vitrine text[] NOT NULL DEFAULT '{}' CHECK (cardinality(vitrine) <= 3),
    -- Enfeites que a pessoa já viu: o que está liberado e fora desta lista é "novo".
    enfeites_vistos text[] NOT NULL DEFAULT '{}',
    updated_at timestamptz NOT NULL DEFAULT now()
);
