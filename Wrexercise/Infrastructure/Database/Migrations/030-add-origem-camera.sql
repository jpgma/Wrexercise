-- ADR 0058: a foto ou o vídeo feitos pela câmera do próprio aplicativo têm a data do momento da captura.
-- O navegador informa a origem 'Camera'; como o EXIF, é um sinal do cliente, não uma prova.
ALTER TABLE evidencias_atividade
    DROP CONSTRAINT evidencias_atividade_origem_data_captura_check,
    ADD CONSTRAINT evidencias_atividade_origem_data_captura_check
        CHECK (origem_data_captura IN ('Exif', 'Arquivo', 'Camera'));
