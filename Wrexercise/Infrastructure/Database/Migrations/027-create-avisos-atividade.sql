-- ADR 0052: sinais sobre a evidência, lidos no navegador antes de a compressão apagar o EXIF.
-- Nada disso é prova: o navegador pode mentir. Servem para avisar o grupo e barrar a reutilização óbvia.
-- A localização do EXIF fica guardada, mas nenhuma resposta da API a devolve: mostrá-la é outra decisão.
ALTER TABLE evidencias_atividade
    ADD COLUMN capturada_em timestamptz,
    ADD COLUMN origem_data_captura text CHECK (origem_data_captura IN ('Exif', 'Arquivo')),
    ADD COLUMN sha256_original text,
    ADD COLUMN dhash bigint,
    ADD COLUMN latitude double precision CHECK (latitude BETWEEN -90 AND 90),
    ADD COLUMN longitude double precision CHECK (longitude BETWEEN -180 AND 180),
    ADD CONSTRAINT evidencias_atividade_origem_com_data
        CHECK ((capturada_em IS NULL) = (origem_data_captura IS NULL)),
    ADD CONSTRAINT evidencias_atividade_coordenadas_juntas
        CHECK ((latitude IS NULL) = (longitude IS NULL));

CREATE INDEX evidencias_atividade_sha256 ON evidencias_atividade (sha256);

CREATE INDEX evidencias_atividade_sha256_original ON evidencias_atividade (sha256_original)
    WHERE sha256_original IS NOT NULL;

CREATE TABLE avisos_atividade (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    atividade_id uuid NOT NULL REFERENCES atividades (id),
    evidencia_id uuid REFERENCES evidencias_atividade (id),
    tipo text NOT NULL CHECK (tipo IN (
        'FotoForaDoPeriodo',
        'FotoSemData',
        'FotoParecida',
        'RegistroTardio'
    )),
    -- Só em FotoParecida: a atividade da foto parecida, que pode ser de outro grupo.
    atividade_relacionada_id uuid REFERENCES atividades (id),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX avisos_atividade_atividade_id ON avisos_atividade (atividade_id);
