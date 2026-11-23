-- ADR 0087: a carta recusada, não cumprida ou expirada devolve metade do preço a quem enviou.
-- O aviso guarda a Energia devolvida, para o texto "+N EN de volta".
ALTER TABLE notificacoes ADD COLUMN energia integer CHECK (energia > 0);

-- CartaNaoCumprida avisa quem enviou quando a carta falha ou expira. A situação ('Falhou' ou 'Expirada') fica em
-- situacao. O serviço periódico grava um aviso por Jogada: o índice impede o segundo.
ALTER TABLE notificacoes DROP CONSTRAINT notificacoes_tipo_check;
ALTER TABLE notificacoes ADD CONSTRAINT notificacoes_tipo_check CHECK (tipo IN (
    'NovaAtividade', 'Reacao', 'Comentario', 'SituacaoAlterada', 'Disputa',
    'CartaRecebida', 'CartaAceita', 'CartaRecusada', 'JogadaConcluida', 'RivalEscolhido', 'Cutucada',
    'ComecouTreinar', 'Torcida', 'TreinoLongo', 'TreinoAppConcluido', 'CartaNaoCumprida'));

CREATE UNIQUE INDEX notificacoes_carta_nao_cumprida ON notificacoes (jogada_id) WHERE tipo = 'CartaNaoCumprida';
