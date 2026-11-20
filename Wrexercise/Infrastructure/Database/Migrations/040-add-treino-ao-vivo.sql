-- Treino ao vivo (decisões/0078-treino-ao-vivo.md): a pessoa começa o treino agora e termina depois.
-- Enquanto isso, a atividade fica EmAndamento: não conta para nada, ninguém a avalia e o grupo a vê ao vivo.
ALTER TABLE atividades DROP CONSTRAINT atividades_situacao_verificacao_check;

ALTER TABLE atividades ADD CONSTRAINT atividades_situacao_verificacao_check CHECK (situacao_verificacao IN (
    'EmAndamento',
    'AguardandoFeedback',
    'Verificada',
    'EmRevisao',
    'Contestada',
    'Rejeitada',
    'Anulada'
));

-- Começou pelo "Começar treino": rende o bônus Ao vivo e pode render Parceria.
ALTER TABLE atividades ADD COLUMN ao_vivo boolean NOT NULL DEFAULT false;

-- Um treino em andamento por pessoa, em todos os grupos.
CREATE UNIQUE INDEX atividades_em_andamento_usuario ON atividades (usuario_id)
    WHERE situacao_verificacao = 'EmAndamento';

-- Torcida: um membro torce por quem está treinando agora.
CREATE TABLE torcidas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    atividade_id uuid NOT NULL REFERENCES atividades (id) ON DELETE CASCADE,
    usuario_id uuid NOT NULL REFERENCES usuarios (id),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX torcidas_atividade ON torcidas (atividade_id, created_at);

-- ComecouTreinar e Torcida avisam o grupo e quem treina; TreinoLongo lembra quem esqueceu o treino aberto.
-- Os três apontam para a atividade, então a regra da referência não muda.
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida', 'RivalEscolhido', 'Cutucada',
    'ComecouTreinar', 'Torcida', 'TreinoLongo'));

-- Até 10 fotos e vídeos por atividade (antes, 3): dá para ir mandando durante o treino.
ALTER TABLE evidencias_atividade DROP CONSTRAINT evidencias_atividade_ordem_check;
ALTER TABLE evidencias_atividade ADD CONSTRAINT evidencias_atividade_ordem_check
    CHECK (ordem >= 1 AND ordem <= 10);
