-- Cartas Globais (ADR 0091): o TODO MUNDO convoca o grupo inteiro para uma semana de um tipo de treino.
ALTER TABLE cartas DROP CONSTRAINT cartas_codigo_check;
ALTER TABLE cartas ADD CONSTRAINT cartas_codigo_check CHECK (codigo IN (
    'Mais10Min', 'Mais20Min', 'Variedade', 'FimDeSemana', 'AntesDas8',
    'DesafioDuplo', 'Aposta', 'PrazoExtra', 'MudancaDePlanos',
    'TodoMundo'));

-- A Convocação: a carta Global jogada num grupo. O tipo e o prazo não mudam depois de criada.
-- A situação, as recompensas e a devolução são calculadas na leitura (docs/gamificacao.md §6.6).
CREATE TABLE convocacoes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    grupo_id uuid NOT NULL REFERENCES grupos (id),
    anfitriao_id uuid NOT NULL REFERENCES usuarios (id),
    -- Uma carta gera uma Convocação só: a segunda tentativa com a mesma carta falha aqui.
    carta_id uuid NOT NULL UNIQUE REFERENCES cartas (id),
    tipo_atividade text NOT NULL CHECK (tipo_atividade IN (
        'Musculacao', 'Corrida', 'Caminhada', 'Ciclismo', 'Natacao', 'Futebol', 'Volei', 'Luta', 'Yoga',
        'Calistenia')),
    created_at timestamptz NOT NULL DEFAULT now(),
    expira_em timestamptz NOT NULL,
    CHECK (expira_em > created_at)
);

CREATE INDEX convocacoes_grupo ON convocacoes (grupo_id, expira_em DESC);

-- Quem foi convidado, fixado na criação: o anfitrião e os membros no modo Jogo naquele momento.
-- A decisão é final: 'Aceita' só pode virar 'Desistiu'; 'Recusada' e 'Desistiu' não mudam.
CREATE TABLE participantes_convocacao (
    convocacao_id uuid NOT NULL REFERENCES convocacoes (id),
    usuario_id uuid NOT NULL REFERENCES usuarios (id),
    decisao text CHECK (decisao IN ('Aceita', 'Recusada', 'Desistiu')),
    decidida_em timestamptz,
    PRIMARY KEY (convocacao_id, usuario_id),
    CHECK ((decisao IS NULL) = (decidida_em IS NULL))
);

CREATE INDEX participantes_convocacao_usuario ON participantes_convocacao (usuario_id);

-- Aviso para cada convidado quando a Convocação começa.
ALTER TABLE notificacoes ADD COLUMN convocacao_id uuid REFERENCES convocacoes (id) ON DELETE CASCADE;
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida', 'RivalEscolhido', 'Cutucada',
    'ComecouTreinar', 'Torcida', 'TreinoLongo', 'TreinoAppConcluido', 'CartaNaoCumprida', 'Convocacao'));
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_referencia_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_referencia_check
    CHECK (num_nonnulls(atividade_id, jogada_id, rival_id, cutucada_id, convocacao_id) = 1);

CREATE INDEX notificacoes_convocacao_id ON notificacoes (convocacao_id);
