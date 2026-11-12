-- ADR 0049: notificações push. Cada aparelho com o push ligado é uma inscrição.
CREATE TABLE push_subscriptions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
    endpoint text NOT NULL UNIQUE,
    p256dh text NOT NULL,
    auth text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX push_subscriptions_usuario_id ON push_subscriptions (usuario_id);

-- Nulo enquanto o aviso não foi ao telefone. Uma reação ou um comentário somado à mesma linha o anula de novo.
ALTER TABLE notificacoes ADD COLUMN push_enviado_em timestamptz;
UPDATE notificacoes SET push_enviado_em = now();
CREATE INDEX notificacoes_push_pendente ON notificacoes (updated_at) WHERE push_enviado_em IS NULL;
