-- Metas por tipo (docs/decisoes/0043-metas-por-tipo.md): alternativas à duração mínima do desafio.
-- Um dia conta quando a soma do dia alcança duracao_minima_segundos do desafio OU quando a soma das
-- atividades dos tipos de uma meta alcança o mínimo dela. Como o resto da meta, não muda depois de criada.
CREATE TABLE metas_tipo_desafio (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    desafio_id uuid NOT NULL REFERENCES desafios (id),
    -- A posição no formulário: desempata qual meta "fez o dia contar" quando várias são cumpridas.
    ordem smallint NOT NULL CHECK (ordem >= 0),
    nome text,
    tipos text[] NOT NULL CHECK (cardinality(tipos) > 0),
    duracao_minima_segundos integer NOT NULL CHECK (duracao_minima_segundos > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (desafio_id, ordem)
);
