-- G7 (docs/gamificacao.md §8 e decisoes/0038-rival-da-semana.md).

-- O horário em que a atividade ficou Verificada: decide quem cumpriu a meta primeiro no Rival da semana.
-- Verificada é final, então o horário nunca muda depois de gravado.
ALTER TABLE atividades ADD COLUMN verified_at timestamptz;

-- Atividades já verificadas: a última revisão que as verificou ou, sem revisão, o envio.
UPDATE atividades a
SET verified_at = coalesce(
    (SELECT max(h.created_at)
     FROM historico_revisao h
     WHERE h.atividade_id = a.id AND h.decisao = 'Verificada'),
    a.submitted_at)
WHERE a.situacao_verificacao = 'Verificada';

ALTER TABLE atividades ADD CONSTRAINT atividades_verified_at_check
    CHECK (situacao_verificacao <> 'Verificada' OR verified_at IS NOT NULL);

-- Um Rival escolhido para uma semana do desafio em destaque. O resultado é calculado na leitura.
CREATE TABLE rivais (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    grupo_id uuid NOT NULL REFERENCES grupos (id),
    desafio_id uuid NOT NULL REFERENCES desafios (id),
    -- A segunda-feira da semana do desafio, no fuso do grupo.
    semana_inicio date NOT NULL,
    escolhedor_id uuid NOT NULL REFERENCES usuarios (id),
    rival_id uuid NOT NULL REFERENCES usuarios (id),
    created_at timestamptz NOT NULL DEFAULT now(),
    -- O Rival já viu a provocação; quem escolheu já viu o resultado.
    vista_em timestamptz,
    resultado_visto_em timestamptz,
    UNIQUE (grupo_id, escolhedor_id, semana_inicio),
    CHECK (escolhedor_id <> rival_id)
);

CREATE INDEX rivais_escolhedor_id ON rivais (escolhedor_id);
CREATE INDEX rivais_rival_id ON rivais (rival_id);

-- Aviso para quem foi escolhido como Rival.
ALTER TABLE notificacoes ADD COLUMN rival_id uuid REFERENCES rivais (id) ON DELETE CASCADE;
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida', 'RivalEscolhido'));
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_referencia_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_referencia_check
    CHECK (num_nonnulls(atividade_id, jogada_id, rival_id) = 1);

CREATE INDEX notificacoes_rival_id ON notificacoes (rival_id);
