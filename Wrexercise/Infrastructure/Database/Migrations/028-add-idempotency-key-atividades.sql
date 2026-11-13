-- ADR 0053: a mesma chave de quem registra devolve a atividade já criada, em vez de criar outra.
-- Opcional: registros antigos e clientes sem o cabeçalho Idempotency-Key ficam com NULL.
ALTER TABLE atividades ADD COLUMN idempotency_key uuid;

CREATE UNIQUE INDEX atividades_usuario_idempotency_key ON atividades (usuario_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
