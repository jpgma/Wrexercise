-- ADR 0060: a evidência é um print. O navegador decide pelo nome do arquivo (origem 'Print') ou por uma
-- imagem do tamanho exato da tela, sem câmera no EXIF. No Android, o seletor de fotos troca o nome do
-- arquivo, e a data do print se perde: o selo fica, sem a data. Sinal do cliente, não prova.
ALTER TABLE evidencias_atividade
    ADD COLUMN print boolean NOT NULL DEFAULT false;

UPDATE evidencias_atividade SET print = true WHERE origem_data_captura = 'Print';
