-- ADR 0068: o proprietário ou um administrador corrige a duração de uma atividade de outra pessoa.
-- Cada correção fica registrada, com o valor de antes e o de depois, e todo o grupo a vê na atividade.
CREATE TABLE correcoes_duracao (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    atividade_id uuid NOT NULL REFERENCES atividades (id) ON DELETE CASCADE,
    responsavel_id uuid NOT NULL REFERENCES usuarios (id),
    duracao_anterior_segundos integer NOT NULL CHECK (duracao_anterior_segundos > 0),
    duracao_nova_segundos integer NOT NULL CHECK (duracao_nova_segundos > 0),
    justificativa text NOT NULL CHECK (char_length(justificativa) >= 1 AND char_length(justificativa) <= 500),
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK (duracao_nova_segundos <> duracao_anterior_segundos)
);

CREATE INDEX correcoes_duracao_atividade_id ON correcoes_duracao (atividade_id, created_at);
