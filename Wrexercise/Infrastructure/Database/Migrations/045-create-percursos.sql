-- ADR 0084: o Percurso do treino ao vivo (Corrida, Caminhada, Ciclismo), gravado pelo navegador.
-- A linha só existe quando a pessoa escolheu gravar o percurso ao começar o treino.
-- O resumo é recalculado pelo servidor a cada lote de pontos, ao terminar e ao corrigir a duração.
CREATE TABLE percursos (
    atividade_id uuid PRIMARY KEY REFERENCES atividades (id) ON DELETE CASCADE,
    distancia_metros integer NOT NULL DEFAULT 0 CHECK (distancia_metros >= 0),
    velocidade_maxima double precision CHECK (velocidade_maxima >= 0),
    ganho_elevacao_metros integer CHECK (ganho_elevacao_metros >= 0),
    total_pontos integer NOT NULL DEFAULT 0 CHECK (total_pontos >= 0),
    updated_at timestamptz NOT NULL DEFAULT now()
);

-- Os pontos crus, como o navegador os mandou. O filtro (precisão, saltos, lacunas) é feito na leitura, em C#:
-- mudar a regra não exige migrar dados. O seq vem do aparelho, e repetir um lote não duplica nada.
CREATE TABLE pontos_percurso (
    atividade_id uuid NOT NULL REFERENCES percursos (atividade_id) ON DELETE CASCADE,
    seq integer NOT NULL CHECK (seq BETWEEN 0 AND 100000),
    recorded_at timestamptz NOT NULL,
    latitude double precision NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude double precision NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    precisao_metros real NOT NULL CHECK (precisao_metros >= 0),
    altitude_metros real,
    segmento smallint NOT NULL CHECK (segmento >= 0),
    PRIMARY KEY (atividade_id, seq)
);

-- A imagem do percurso vira evidência com a origem Percurso, que só o servidor atribui. Uma por atividade.
ALTER TABLE evidencias_atividade
    DROP CONSTRAINT evidencias_atividade_origem_data_captura_check,
    ADD CONSTRAINT evidencias_atividade_origem_data_captura_check
        CHECK (origem_data_captura IN ('Exif', 'Arquivo', 'Camera', 'Print', 'Integracao', 'Percurso'));

CREATE UNIQUE INDEX evidencias_atividade_percurso_unico
    ON evidencias_atividade (atividade_id) WHERE origem_data_captura = 'Percurso';
