-- Cartas compradas na Loja. O preço pago fica na linha: mudar o catálogo não altera cartas já compradas.
-- Se a carta está na Mão, em jogo ou gasta é calculado a partir das Jogadas (docs/gamificacao.md §6.2).
CREATE TABLE cartas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL REFERENCES usuarios (id),
    codigo text NOT NULL CHECK (codigo IN (
        'Mais10Min', 'Mais20Min', 'Variedade', 'FimDeSemana', 'AntesDas8',
        'DesafioDuplo', 'Aposta', 'PrazoExtra', 'MudancaDePlanos')),
    preco integer NOT NULL CHECK (preco > 0),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX cartas_usuario_id ON cartas (usuario_id);

-- Uma Provocação jogada para um colega. A situação é calculada na leitura (docs/gamificacao.md §6.4).
CREATE TABLE jogadas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    grupo_id uuid NOT NULL REFERENCES grupos (id),
    remetente_id uuid NOT NULL REFERENCES usuarios (id),
    destinatario_id uuid NOT NULL REFERENCES usuarios (id),
    carta_id uuid NOT NULL REFERENCES cartas (id),
    enviada_em timestamptz NOT NULL DEFAULT now(),
    expira_em timestamptz NOT NULL,
    resposta text CHECK (resposta IN ('Aceita', 'Recusada')),
    respondida_em timestamptz,
    -- G4: a Resposta usada (gasta ao ser usada) e a Jogada original do DESAFIO DUPLO.
    carta_resposta_id uuid UNIQUE REFERENCES cartas (id),
    origem_id uuid REFERENCES jogadas (id),
    -- Fixados no envio: a duração mínima de +10 MIN e +20 MIN e o tipo a evitar da VARIEDADE.
    duracao_alvo_segundos integer CHECK (duracao_alvo_segundos > 0),
    tipo_evitar text,
    vista_em timestamptz,
    resultado_visto_em timestamptz,
    CHECK (remetente_id <> destinatario_id),
    CHECK (expira_em > enviada_em),
    CHECK ((resposta IS NULL) = (respondida_em IS NULL))
);

CREATE INDEX jogadas_remetente_id ON jogadas (remetente_id);
CREATE INDEX jogadas_destinatario_id ON jogadas (destinatario_id);
CREATE INDEX jogadas_carta_id ON jogadas (carta_id);

-- Avisos de Jogadas: não têm atividade, exceto a conclusão, que aponta para a Jogada.
ALTER TABLE notificacoes ALTER COLUMN atividade_id DROP NOT NULL;
ALTER TABLE notificacoes ADD COLUMN jogada_id uuid REFERENCES jogadas (id) ON DELETE CASCADE;
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida'));
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_referencia_check
    CHECK ((atividade_id IS NULL) <> (jogada_id IS NULL));

CREATE INDEX notificacoes_jogada_id ON notificacoes (jogada_id);
