-- Cutucada (decisões/0063-cutucada.md): um membro chama um colega que dorme no grupo.

CREATE TABLE cutucadas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    grupo_id uuid NOT NULL REFERENCES grupos (id),
    autor_id uuid NOT NULL REFERENCES usuarios (id),
    destinatario_id uuid NOT NULL REFERENCES usuarios (id),
    tipo text NOT NULL CHECK (tipo IN ('Sofa', 'Frango', 'Meta', 'Halter', 'Despertador', 'Fantasma')),
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK (autor_id <> destinatario_id)
);

-- As cutucadas recebidas na cena e o limite de uma por par a cada 24 horas.
CREATE INDEX cutucadas_grupo_destinatario ON cutucadas (grupo_id, destinatario_id, created_at);
-- O bônus Despertador de quem cutucou.
CREATE INDEX cutucadas_autor ON cutucadas (autor_id, created_at);

-- Aviso para quem foi cutucado.
ALTER TABLE notificacoes ADD COLUMN cutucada_id uuid REFERENCES cutucadas (id) ON DELETE CASCADE;
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida', 'RivalEscolhido', 'Cutucada'));
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_referencia_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_referencia_check
    CHECK (num_nonnulls(atividade_id, jogada_id, rival_id, cutucada_id) = 1);

CREATE INDEX notificacoes_cutucada_id ON notificacoes (cutucada_id);
