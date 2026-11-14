-- ADR 0060: captura de tela com a data e a hora no nome do arquivo (Screenshot_20261114-083012.png).
-- O navegador informa a origem 'Print'; como o EXIF, é um sinal do cliente, não uma prova.
ALTER TABLE evidencias_atividade
    DROP CONSTRAINT evidencias_atividade_origem_data_captura_check,
    ADD CONSTRAINT evidencias_atividade_origem_data_captura_check
        CHECK (origem_data_captura IN ('Exif', 'Arquivo', 'Camera', 'Print'));
