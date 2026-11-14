-- ADR 0058: vídeo como evidência. O tipo do arquivo já está em tipo_mime ('video/mp4'); a duração vem do
-- MP4, lida pelo servidor. A miniatura e o dhash são os do quadro do meio, gerados no navegador.
ALTER TABLE evidencias_atividade
    ADD COLUMN duracao_ms int CHECK (duracao_ms BETWEEN 2000 AND 61000),
    ADD CONSTRAINT evidencias_atividade_duracao_so_em_video
        CHECK ((duracao_ms IS NULL) = (tipo_mime NOT LIKE 'video/%'));

-- VideoParado só informa: não entra na lista que impede a verificação automática.
ALTER TABLE avisos_atividade
    DROP CONSTRAINT avisos_atividade_tipo_check,
    ADD CONSTRAINT avisos_atividade_tipo_check CHECK (tipo IN (
        'FotoForaDoPeriodo',
        'FotoSemData',
        'FotoParecida',
        'RegistroTardio',
        'VideoParado'
    ));
