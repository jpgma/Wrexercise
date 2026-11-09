-- Comentários deixam de ser só da revisão (decisão 0042).
ALTER TABLE comentarios_revisao RENAME TO comentarios;
ALTER INDEX comentarios_revisao_atividade_id RENAME TO comentarios_atividade_id;
