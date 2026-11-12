-- Seed de testes: um grupo com 4 contas e 20 semanas de histórico verificado (ver scripts/seed/README.md).
-- Só para o banco local do run.bat. Rodar de novo apaga e recria o seed; as datas são relativas a hoje.
-- Rodado pelo seed.bat dentro de uma transação (psql --single-transaction).

-- "Aleatório" fixo: o mesmo texto dá sempre o mesmo número em [0, 1). Assim o seed é reproduzível.
CREATE FUNCTION pg_temp.sorte(chave text) RETURNS float8
LANGUAGE sql IMMUTABLE
AS $$ SELECT ('x' || substr(md5(chave), 1, 8))::bit(32)::bigint / 4294967296.0 $$;

CREATE TEMP TABLE seed_pessoas (
    n int PRIMARY KEY,
    id uuid NOT NULL,
    nome text NOT NULL,
    email text NOT NULL,
    papel text NOT NULL,
    -- Sorteados com repetição: a frequência na lista é o peso.
    tipos text[] NOT NULL,
    horas int[] NOT NULL,
    -- Chance de treinar num dia que não é obrigatório.
    chance float8 NOT NULL,
    vitrine text[] NOT NULL,
    -- Nulo: todos os Enfeites, preenchido logo abaixo.
    vistos text[]
) ON COMMIT DROP;

INSERT INTO seed_pessoas VALUES
    (1, '5eed0000-0000-4000-8000-000000000001', 'Ana', 'ana@seed.test', 'Proprietario',
     '{Corrida,Corrida,Corrida,Musculacao,Musculacao,Yoga,Yoga,Natacao,Caminhada,Ciclismo}',
     '{5,6,6,6,7,12,18,19}', 0.55, '{despertador,trofeu,planta}', NULL),
    (2, '5eed0000-0000-4000-8000-000000000002', 'Bruno', 'bruno@seed.test', 'Administrador',
     '{Musculacao,Musculacao,Musculacao,Musculacao,Calistenia,Calistenia,Esporte,Esporte,Corrida,Outro}',
     '{20,21,21,22,18,19,7,12}', 0.60, '{poste,medalha,trofeu}', NULL),
    (3, '5eed0000-0000-4000-8000-000000000003', 'Carla', 'carla@seed.test', 'Membro',
     '{Ciclismo,Ciclismo,Ciclismo,Natacao,Natacao,Yoga,Yoga,Caminhada,Esporte,Outro}',
     '{6,9,10,17,18,20,11,7}', 0.45, '{mochila,planta,trofeu}', NULL),
    -- Diego: vitrine e vistos vazios, para testar os Enfeites "novos" e o preenchimento automático da vitrine.
    (4, '5eed0000-0000-4000-8000-000000000004', 'Diego', 'diego@seed.test', 'Membro',
     '{Esporte,Esporte,Esporte,Calistenia,Calistenia,Musculacao,Musculacao,Corrida,Caminhada,Outro}',
     '{19,20,6,12,17,18,21,13}', 0.50, '{}', '{}');

-- Todos os Enfeites como já vistos (docs: Features/Jogo/Enfeites.cs).
UPDATE seed_pessoas SET vistos = '{planta,medalha,trofeu,haltere,cronometro,cachorro,bicicleta,boia,bola,tapete,paralelas,despertador,poste,caixa-de-som,mochila}'
WHERE vistos IS NULL;

-- ---------------------------------------------------------------------------------------------------------
-- 1. Apaga o seed anterior: as contas do seed (pelo id ou pelo e-mail), os grupos criados por elas e tudo o
--    que aponta para esses grupos ou contas. Outras contas e grupos não mudam.
-- ---------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE velhos_usuarios ON COMMIT DROP AS
SELECT u.id FROM usuarios u
WHERE u.id IN (SELECT id FROM seed_pessoas) OR lower(u.email) IN (SELECT email FROM seed_pessoas);

CREATE TEMP TABLE velhos_grupos ON COMMIT DROP AS
SELECT g.id FROM grupos g
WHERE g.id = '5eed0000-0000-4000-8000-0000000000a0' OR g.criador_id IN (SELECT id FROM velhos_usuarios);

CREATE TEMP TABLE velhas_atividades ON COMMIT DROP AS
SELECT a.id FROM atividades a
WHERE a.grupo_id IN (SELECT id FROM velhos_grupos) OR a.usuario_id IN (SELECT id FROM velhos_usuarios);

CREATE TEMP TABLE velhos_desafios ON COMMIT DROP AS
SELECT d.id FROM desafios d
WHERE d.grupo_id IN (SELECT id FROM velhos_grupos) OR d.criador_id IN (SELECT id FROM velhos_usuarios);

DELETE FROM notificacoes
WHERE usuario_id IN (SELECT id FROM velhos_usuarios) OR ator_id IN (SELECT id FROM velhos_usuarios)
   OR grupo_id IN (SELECT id FROM velhos_grupos) OR atividade_id IN (SELECT id FROM velhas_atividades);

DELETE FROM rivais
WHERE grupo_id IN (SELECT id FROM velhos_grupos) OR desafio_id IN (SELECT id FROM velhos_desafios)
   OR escolhedor_id IN (SELECT id FROM velhos_usuarios) OR rival_id IN (SELECT id FROM velhos_usuarios);

-- Um só DELETE: a Jogada original e o DESAFIO DUPLO que aponta para ela saem juntos.
DELETE FROM jogadas
WHERE grupo_id IN (SELECT id FROM velhos_grupos)
   OR remetente_id IN (SELECT id FROM velhos_usuarios) OR destinatario_id IN (SELECT id FROM velhos_usuarios)
   OR carta_id IN (SELECT id FROM cartas WHERE usuario_id IN (SELECT id FROM velhos_usuarios))
   OR carta_resposta_id IN (SELECT id FROM cartas WHERE usuario_id IN (SELECT id FROM velhos_usuarios));

DELETE FROM cartas WHERE usuario_id IN (SELECT id FROM velhos_usuarios);

DELETE FROM reacoes_atividade
WHERE atividade_id IN (SELECT id FROM velhas_atividades) OR usuario_id IN (SELECT id FROM velhos_usuarios);
DELETE FROM comentarios
WHERE atividade_id IN (SELECT id FROM velhas_atividades) OR usuario_id IN (SELECT id FROM velhos_usuarios);
DELETE FROM historico_revisao
WHERE atividade_id IN (SELECT id FROM velhas_atividades) OR responsavel_id IN (SELECT id FROM velhos_usuarios);
DELETE FROM revisores_atividade
WHERE atividade_id IN (SELECT id FROM velhas_atividades) OR usuario_id IN (SELECT id FROM velhos_usuarios);
-- Os arquivos das evidências ficam no disco sem referência; o seed não cria evidências.
DELETE FROM evidencias_atividade WHERE atividade_id IN (SELECT id FROM velhas_atividades);
DELETE FROM atividades WHERE id IN (SELECT id FROM velhas_atividades);

DELETE FROM metas_tipo_desafio WHERE desafio_id IN (SELECT id FROM velhos_desafios);
DELETE FROM desafios WHERE id IN (SELECT id FROM velhos_desafios);

DELETE FROM convites_grupo
WHERE grupo_id IN (SELECT id FROM velhos_grupos) OR criador_id IN (SELECT id FROM velhos_usuarios);
DELETE FROM membros_grupo
WHERE grupo_id IN (SELECT id FROM velhos_grupos) OR usuario_id IN (SELECT id FROM velhos_usuarios);
DELETE FROM grupos WHERE id IN (SELECT id FROM velhos_grupos);

-- Sessões, redefinições de senha e avatares saem em cascata.
DELETE FROM usuarios WHERE id IN (SELECT id FROM velhos_usuarios);

-- ---------------------------------------------------------------------------------------------------------
-- 2. Contas, grupo e desafios. Semana 0 é a semana atual (segunda a domingo, no fuso do grupo).
-- ---------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE seed_datas ON COMMIT DROP AS
SELECT hoje, hoje - (extract(isodow FROM hoje)::int - 1) AS m0
FROM (SELECT (now() AT TIME ZONE 'America/Sao_Paulo')::date AS hoje) h;

-- Senha de todas as contas: wrexercise (PBKDF2 com sal fixo, só para testes).
INSERT INTO usuarios (id, nome_exibicao, email, password_hash, created_at)
SELECT p.id, p.nome, p.email,
    'pbkdf2-sha256$600000$d3JleGVyY2lzZS1zZWVkIQ==$aowXJyNCEDEL26IC3QKwwmx4weCzv+ZduZfxKtzKoh0=',
    ((d.m0 - 20 * 7) + time '08:00') AT TIME ZONE 'America/Sao_Paulo'
FROM seed_pessoas p CROSS JOIN seed_datas d;

INSERT INTO grupos (id, nome, criador_id, fuso_horario, created_at)
SELECT '5eed0000-0000-4000-8000-0000000000a0', 'Seed de testes', '5eed0000-0000-4000-8000-000000000001',
    'America/Sao_Paulo', ((d.m0 - 20 * 7) + time '08:00') AT TIME ZONE 'America/Sao_Paulo'
FROM seed_datas d;

-- A entrada antecede o primeiro desafio: todas as semanas dele contam para todos.
INSERT INTO membros_grupo (grupo_id, usuario_id, papel, joined_at)
SELECT '5eed0000-0000-4000-8000-0000000000a0', p.id, p.papel,
    ((d.m0 - 20 * 7) + time '08:00') AT TIME ZONE 'America/Sao_Paulo' + p.n * interval '1 minute'
FROM seed_pessoas p CROSS JOIN seed_datas d;

-- Encerrado: 14 semanas, da semana -19 à -6. Todos cumprem tudo, menos Diego (falha a semana -13).
INSERT INTO desafios (id, grupo_id, criador_id, nome, descricao, data_inicio, data_fim,
    duracao_minima_segundos, sessoes_por_semana, created_at)
SELECT '5eed0000-0000-4000-8000-0000000000d1', '5eed0000-0000-4000-8000-0000000000a0',
    '5eed0000-0000-4000-8000-000000000001', 'Projeto verão', 'Três treinos de 30 min por semana.',
    d.m0 - 19 * 7, d.m0 - 5 * 7 - 1, 30 * 60, 3,
    ((d.m0 - 20 * 7) + time '09:00') AT TIME ZONE 'America/Sao_Paulo'
FROM seed_datas d;

-- Ativo: 12 semanas, da semana -4 à 7, com uma meta por tipo.
INSERT INTO desafios (id, grupo_id, criador_id, nome, descricao, data_inicio, data_fim,
    duracao_minima_segundos, sessoes_por_semana, created_at)
SELECT '5eed0000-0000-4000-8000-0000000000d2', '5eed0000-0000-4000-8000-0000000000a0',
    '5eed0000-0000-4000-8000-000000000002', 'Rumo aos 100', 'Quatro dias de 40 min por semana.',
    d.m0 - 4 * 7, d.m0 + 8 * 7 - 1, 40 * 60, 4,
    ((d.m0 - 5 * 7) + time '09:00') AT TIME ZONE 'America/Sao_Paulo'
FROM seed_datas d;

INSERT INTO metas_tipo_desafio (desafio_id, ordem, nome, tipos, duracao_minima_segundos, created_at)
SELECT '5eed0000-0000-4000-8000-0000000000d2', 0, 'Pedal ou corrida', '{Corrida,Ciclismo}', 25 * 60,
    ((d.m0 - 5 * 7) + time '09:00') AT TIME ZONE 'America/Sao_Paulo'
FROM seed_datas d;

-- ---------------------------------------------------------------------------------------------------------
-- 3. Histórico: no máximo uma atividade verificada por dia e pessoa, das 20 semanas até ontem.
--    Segunda, terça, quinta e sábado são obrigatórios e longos (cumprem as metas dos dois desafios);
--    os outros dias saem pela chance da pessoa. Tipos e horários seguem os pesos de seed_pessoas.
-- ---------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE seed_atividades ON COMMIT DROP AS
SELECT p.id AS usuario_id, p.n, dia,
    p.tipos[1 + floor(pg_temp.sorte(p.n || 'tipo' || dia) * cardinality(p.tipos))::int] AS tipo,
    (CASE WHEN obrigatorio
          THEN 45 + floor(pg_temp.sorte(p.n || 'dur' || dia) * 46)::int
          ELSE 20 + floor(pg_temp.sorte(p.n || 'dur' || dia) * 56)::int END) * 60 AS duracao,
    (dia + make_time(p.horas[1 + floor(pg_temp.sorte(p.n || 'hora' || dia) * cardinality(p.horas))::int],
                     15 * floor(pg_temp.sorte(p.n || 'min' || dia) * 4)::int, 0))
        AT TIME ZONE 'America/Sao_Paulo' AS inicio
FROM seed_pessoas p
CROSS JOIN seed_datas d
CROSS JOIN LATERAL generate_series(d.m0 - 20 * 7, d.hoje - 1, interval '1 day') AS s(t)
CROSS JOIN LATERAL (SELECT s.t::date AS dia) x
CROSS JOIN LATERAL (SELECT extract(isodow FROM x.dia)::int IN (1, 2, 4, 6) AS obrigatorio) o
WHERE NOT (p.n = 4 AND dia >= d.m0 - 13 * 7 AND dia < d.m0 - 12 * 7)
  AND (obrigatorio OR pg_temp.sorte(p.n || 'treina' || dia) < p.chance);

INSERT INTO atividades (grupo_id, usuario_id, tipo, duracao_segundos, started_at, descricao, tipo_comprovacao,
    situacao_verificacao, submitted_at, created_at, verified_at)
SELECT '5eed0000-0000-4000-8000-0000000000a0', a.usuario_id, a.tipo, a.duracao, a.inicio,
    CASE WHEN pg_temp.sorte(a.n || 'desc' || a.dia) < 0.3
         THEN (ARRAY['Pesado hoje!', 'Treino leve para recuperar.', 'Bati meu recorde.',
                     'Com chuva e tudo.', 'Melhor parte do dia.', 'Quase desisti, mas fui.'])
              [1 + floor(pg_temp.sorte(a.n || 'texto' || a.dia) * 6)::int] END,
    'RegistroManual', 'Verificada',
    a.inicio + a.duracao * interval '1 second' + interval '10 minutes',
    a.inicio + a.duracao * interval '1 second' + interval '10 minutes',
    least(a.inicio + a.duracao * interval '1 second' + interval '3 hours', now())
FROM seed_atividades a;

-- Os revisores são os outros membros, como no envio de verdade.
INSERT INTO revisores_atividade (atividade_id, usuario_id)
SELECT a.id, m.usuario_id
FROM atividades a
JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id <> a.usuario_id
WHERE a.grupo_id = '5eed0000-0000-4000-8000-0000000000a0';

-- "Dar moral" de parte dos colegas, para o Feed não ficar vazio.
INSERT INTO reacoes_atividade (atividade_id, usuario_id, tipo, created_at, updated_at)
SELECT r.atividade_id, r.usuario_id, 'Kudos', a.submitted_at + interval '1 hour', a.submitted_at + interval '1 hour'
FROM revisores_atividade r
JOIN atividades a ON a.id = r.atividade_id
WHERE a.grupo_id = '5eed0000-0000-4000-8000-0000000000a0'
  AND pg_temp.sorte(r.atividade_id::text || r.usuario_id::text) < 0.45
  AND a.submitted_at + interval '1 hour' < now();

-- ---------------------------------------------------------------------------------------------------------
-- 4. Avatares tingidos (scripts/seed/avatares, copiados pelo seed.bat) e vitrines.
-- ---------------------------------------------------------------------------------------------------------
INSERT INTO avatares (usuario_id, arquivo, vitrine, enfeites_vistos)
SELECT p.id, '5eed000000000000000000000000000' || p.n || '.png', p.vitrine, p.vistos
FROM seed_pessoas p;

-- Resumo para o seed.bat mostrar.
SELECT p.nome, p.email, count(a.id) AS atividades_verificadas,
    count(DISTINCT (a.started_at AT TIME ZONE 'America/Sao_Paulo')::date) AS dias_com_treino
FROM seed_pessoas p
LEFT JOIN atividades a ON a.usuario_id = p.id
GROUP BY p.n, p.nome, p.email
ORDER BY p.n;
