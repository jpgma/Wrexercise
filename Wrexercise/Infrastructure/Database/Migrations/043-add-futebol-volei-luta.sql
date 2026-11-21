-- Três esportes ganham tipo próprio: Futebol, Vôlei e Luta (ADR 0082). "Esporte" continua, como
-- "Outro esporte" na interface, e as atividades antigas não mudam.
ALTER TABLE atividades DROP CONSTRAINT atividades_tipo_check;

ALTER TABLE atividades ADD CONSTRAINT atividades_tipo_check CHECK (tipo IN (
    'Musculacao',
    'Corrida',
    'Caminhada',
    'Ciclismo',
    'Natacao',
    'Futebol',
    'Volei',
    'Luta',
    'Esporte',
    'Yoga',
    'Calistenia',
    'Outro'
));
