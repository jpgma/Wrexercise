-- ADR 0081: um app conectado (o Movr) começa o treino ao vivo e informa o fim como sugestão.
-- O vínculo fica na atividade depois de terminada: o app ainda consulta a situação pelo identificador dele.
ALTER TABLE atividades
    ADD COLUMN app text CHECK (app IN ('Movr')),
    ADD COLUMN app_external_id text CHECK (length(app_external_id) BETWEEN 1 AND 64),
    ADD COLUMN app_duracao_segundos integer CHECK (app_duracao_segundos BETWEEN 1 AND 86400),
    ADD COLUMN app_ended_at timestamptz,
    ADD CONSTRAINT atividades_app_vinculo_check CHECK ((app IS NULL) = (app_external_id IS NULL));

-- Um treino ao vivo por treino do app: repetir o começo devolve o mesmo.
CREATE UNIQUE INDEX atividades_app_externo ON atividades (usuario_id, app, app_external_id)
    WHERE app IS NOT NULL;

-- TreinoAppConcluido lembra quem treinou no app de terminar aqui, 30 minutos depois do fim informado.
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida', 'RivalEscolhido', 'Cutucada',
    'ComecouTreinar', 'Torcida', 'TreinoLongo', 'TreinoAppConcluido'));
