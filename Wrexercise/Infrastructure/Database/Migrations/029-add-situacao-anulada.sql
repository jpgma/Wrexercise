-- ADR 0054: o proprietário ou um administrador anula uma atividade ainda não decidida (registro errado,
-- em dobro). Anulada é final e, como Rejeitada, não conta para nada; a diferença é não acusar ninguém.
ALTER TABLE atividades DROP CONSTRAINT atividades_situacao_verificacao_check;

ALTER TABLE atividades ADD CONSTRAINT atividades_situacao_verificacao_check CHECK (situacao_verificacao IN (
    'AguardandoFeedback',
    'Verificada',
    'EmRevisao',
    'Contestada',
    'Rejeitada',
    'Anulada'
));

ALTER TABLE historico_revisao DROP CONSTRAINT historico_revisao_decisao_check;

ALTER TABLE historico_revisao ADD CONSTRAINT historico_revisao_decisao_check
    CHECK (decisao IN ('Verificada', 'Rejeitada', 'Anulada'));
