-- ADR 0039 (Andamento de 2026-11-09): a importação volta no formato em 8 direções, e as folhas de lado
-- (256×384) importadas antes não servem mais. Todos voltam ao avatar padrão. Os arquivos antigos em
-- /dados/avatares ficam sem referência e são removidos pela limpeza automática (LimpezaAvatares).
UPDATE avatares SET arquivo = NULL, updated_at = now() WHERE arquivo IS NOT NULL;
