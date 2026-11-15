-- ADR 0065: o Movr prepara um Rascunho de atividade; quem posta é sempre a pessoa, no Wrexercise.

-- Um código de uso único e curto, trocado pelo token com PKCE (S256). O banco guarda só o hash.
CREATE TABLE codigos_integracao (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
    app text NOT NULL CHECK (app IN ('Movr')),
    code_hash bytea NOT NULL,
    code_challenge text NOT NULL,
    redirect_uri text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    used_at timestamptz
);

CREATE UNIQUE INDEX codigos_integracao_code_hash_unique ON codigos_integracao (code_hash);

-- O token do app só cria e consulta rascunhos; vale até ser revogado.
CREATE TABLE apps_conectados (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
    app text NOT NULL CHECK (app IN ('Movr')),
    token_hash bytea NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    last_used_at timestamptz,
    revoked_at timestamptz
);

CREATE UNIQUE INDEX apps_conectados_token_hash_unique ON apps_conectados (token_hash);
CREATE INDEX apps_conectados_usuario_id ON apps_conectados (usuario_id) WHERE revoked_at IS NULL;

-- Um rascunho por treino do app (external_id). Some 48 horas depois do início, como o limite do registro.
-- Apagar a atividade (ADR 0054) devolve o rascunho: atividade_id volta a NULL.
CREATE TABLE rascunhos_atividade (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
    app text NOT NULL CHECK (app IN ('Movr')),
    external_id text NOT NULL CHECK (length(external_id) BETWEEN 1 AND 64),
    tipo text NOT NULL,
    duracao_segundos integer NOT NULL CHECK (duracao_segundos BETWEEN 60 AND 86400),
    started_at timestamptz NOT NULL,
    ended_at timestamptz NOT NULL CHECK (ended_at >= started_at),
    caminho_relativo text NOT NULL,
    tipo_mime text NOT NULL,
    tamanho_bytes integer NOT NULL,
    sha256 text NOT NULL,
    caminho_miniatura text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    atividade_id uuid REFERENCES atividades (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX rascunhos_atividade_externo_unique ON rascunhos_atividade (usuario_id, app, external_id);
CREATE INDEX rascunhos_atividade_expires_at ON rascunhos_atividade (expires_at) WHERE atividade_id IS NULL;

-- A imagem do rascunho vira a primeira evidência, datada pelo fim do treino que o app informou.
ALTER TABLE evidencias_atividade
    DROP CONSTRAINT evidencias_atividade_origem_data_captura_check,
    ADD CONSTRAINT evidencias_atividade_origem_data_captura_check
        CHECK (origem_data_captura IN ('Exif', 'Arquivo', 'Camera', 'Print', 'Integracao'));
