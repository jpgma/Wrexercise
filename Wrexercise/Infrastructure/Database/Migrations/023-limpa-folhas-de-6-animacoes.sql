-- ADR 0039 (Andamento de 2026-11-10): a folha em 8 direções passa de 49 para 97 linhas (432×4656), com
-- as 12 animações do Jogo, e o base masculino vira o avatar padrão. As folhas importadas antes
-- (432×2352) não servem mais. Todos voltam ao avatar padrão. Os arquivos antigos em /dados/avatares
-- ficam sem referência e são removidos pela limpeza automática (LimpezaAvatares).
UPDATE avatares SET arquivo = NULL, updated_at = now() WHERE arquivo IS NOT NULL;
