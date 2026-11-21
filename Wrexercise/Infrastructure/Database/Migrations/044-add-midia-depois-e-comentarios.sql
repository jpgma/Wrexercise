-- ADR 0083: fotos e vídeos depois do envio.
-- A evidência que o autor manda depois do prazo da verificação (ou o resumo que o app manda depois do fim)
-- aparece na atividade, mas nunca verifica nem muda o tipo de comprovação.
ALTER TABLE evidencias_atividade
    ADD COLUMN adicionada_depois boolean NOT NULL DEFAULT false;

-- Um comentário pode ter uma foto ou um vídeo, com ou sem texto. Os arquivos ficam com as evidências.
ALTER TABLE comentarios
    ALTER COLUMN texto DROP NOT NULL,
    ADD COLUMN caminho_relativo text,
    ADD COLUMN tipo_mime text,
    ADD COLUMN tamanho_bytes integer CHECK (tamanho_bytes > 0),
    ADD COLUMN caminho_miniatura text,
    ADD COLUMN duracao_ms int CHECK (duracao_ms BETWEEN 2000 AND 61000),
    ADD CONSTRAINT comentarios_texto_ou_midia CHECK (texto IS NOT NULL OR caminho_relativo IS NOT NULL),
    ADD CONSTRAINT comentarios_midia_completa CHECK (
        (caminho_relativo IS NULL) = (tipo_mime IS NULL)
        AND (caminho_relativo IS NULL) = (tamanho_bytes IS NULL)
        AND (caminho_relativo IS NOT NULL OR caminho_miniatura IS NULL)),
    ADD CONSTRAINT comentarios_duracao_so_em_video
        CHECK (duracao_ms IS NULL OR tipo_mime LIKE 'video/%');
