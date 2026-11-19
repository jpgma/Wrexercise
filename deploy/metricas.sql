-- Métricas de uso do Wrexercise, só leitura. Respondem às regras que pedem revisão com dados reais
-- (docs/TODO.md §3) e aos critérios de sucesso do Jogo (docs/gamificacao.md §13).
-- Rodado por "remote-termux.sh metricas" (metricas.bat) com psql -v dias=N: o período são os últimos N dias.
-- Sem comandos do psql (\...): o teste de integração MetricasSqlTests roda este arquivo pelo Npgsql no banco
-- migrado, e falha se uma migração mudar uma tabela ou coluna usada aqui.
-- Fica de fora o que o banco não guarda: as recusas 409 AVALIACOES_PENDENTES e 400 ATIVIDADE_MUITO_ANTIGA,
-- as atividades apagadas pelo autor (ADR 0054) e a Energia ganha, que é calculada no app (CalculoEnergia).

select '== Período ==';
select 'Desde', to_char(now() - make_interval(days => :dias), 'YYYY-MM-DD HH24:MI'), :dias || ' dias até agora';

select '== Pessoas ==';
select 'Contas', count(*) || ' no total', count(*) filter (where modo_inicio = 'Jogo') || ' no modo Jogo'
from usuarios;
select 'Membros ativos', count(distinct usuario_id) || ' pessoas', count(distinct grupo_id) || ' grupos'
from membros_grupo
where removed_at is null;
select 'Registraram no período', count(distinct usuario_id) || ' pessoas'
from atividades
where started_at >= now() - make_interval(days => :dias) and situacao_verificacao <> 'Anulada';

-- §13.1: quem usa o Jogo registra com a mesma regularidade? O modo é o atual da conta, não o da semana.
-- §12.3: o bônus antes das 6 h e a carta ANTES DAS 8 deslocam o treino para a madrugada? A hora usa o fuso do grupo.
select '== Regularidade por semana (gamificação §13.1 e §12.3) ==';
select 'Semana ' || semana, 'modo ' || modo,
       count(*) || ' atividades',
       count(*) filter (where verificada) || ' verificadas',
       count(distinct usuario_id) || ' pessoas',
       round(count(*)::numeric / count(distinct usuario_id), 1) || ' por pessoa',
       round(100.0 * count(*) filter (where hora < 6) / count(*)) || '% antes das 6',
       round(100.0 * count(*) filter (where hora < 8) / count(*)) || '% antes das 8'
from (
    select date_trunc('week', a.started_at at time zone g.fuso_horario)::date as semana,
           extract(hour from a.started_at at time zone g.fuso_horario) as hora,
           u.modo_inicio as modo,
           a.usuario_id,
           a.situacao_verificacao = 'Verificada' as verificada
    from atividades a
    join grupos g on g.id = a.grupo_id
    join usuarios u on u.id = a.usuario_id
    where a.started_at >= now() - make_interval(days => :dias) and a.situacao_verificacao <> 'Anulada'
) x
group by semana, modo
order by semana, modo;

-- ADR 0012 e 0015: a fórmula de consenso rejeita alguma coisa, ou tudo para em EmRevisao?
select '== Verificação (ADR 0012, 0015, 0054) ==';
select 'Situação', situacao_verificacao, count(*) || ' atividades'
from atividades
where started_at >= now() - make_interval(days => :dias)
group by situacao_verificacao
order by count(*) desc;
select 'Tempo até verificar',
       'mediana ' || coalesce(to_char(percentile_cont(0.5) within group (order by verified_at - submitted_at), 'DD"d" HH24"h"MI'), '-'),
       'p90 ' || coalesce(to_char(percentile_cont(0.9) within group (order by verified_at - submitted_at), 'DD"d" HH24"h"MI'), '-')
from atividades
where started_at >= now() - make_interval(days => :dias) and situacao_verificacao = 'Verificada';
select 'Em revisão agora', count(*) || ' atividades',
       'a mais antiga enviada há ' || coalesce(to_char(max(now() - submitted_at), 'DD"d" HH24"h"'), '-')
from atividades
where situacao_verificacao = 'EmRevisao';
select 'Decisão de administrador', decisao, count(*)
from historico_revisao
where created_at >= now() - make_interval(days => :dias)
group by decisao
order by decisao;
select 'Reações', tipo, count(*) || ' reações', count(distinct atividade_id) || ' atividades'
from reacoes_atividade
where created_at >= now() - make_interval(days => :dias)
group by tipo
order by tipo;
select 'Comentários', count(*) || ' comentários', count(distinct atividade_id) || ' atividades'
from comentarios
where created_at >= now() - make_interval(days => :dias);

-- ADR 0052 e 0058: quantos avisos de cada tipo, e como terminam as atividades marcadas.
-- Falso positivo de FotoParecida (espelho da academia) aparece como muitas verificadas com esse aviso.
select '== Avisos (ADR 0052, 0058) ==';
select 'Aviso', tipo, sum(n) || ' atividades',
       string_agg(situacao_verificacao || ' ' || n, ', ' order by n desc)
from (
    select av.tipo, a.situacao_verificacao, count(distinct a.id) as n
    from avisos_atividade av
    join atividades a on a.id = av.atividade_id
    where a.started_at >= now() - make_interval(days => :dias)
    group by av.tipo, a.situacao_verificacao
) x
group by tipo
order by tipo;
select 'Com aviso e decisão de administrador', count(distinct a.id) || ' atividades'
from atividades a
where a.started_at >= now() - make_interval(days => :dias)
  and exists (select 1 from avisos_atividade av where av.atividade_id = a.id)
  and exists (select 1 from historico_revisao h where h.atividade_id = a.id);

-- O limite de 48 h barra o registro honesto atrasado? Muitos registros perto de 48 h sugerem que sim.
select 'Atraso do registro', faixa, count(*) || ' atividades'
from (
    select case
               when submitted_at - started_at < interval '2 hours' then '1. menos de 2 h'
               when submitted_at - started_at < interval '12 hours' then '2. 2 a 12 h'
               when submitted_at - started_at < interval '24 hours' then '3. 12 a 24 h'
               when submitted_at - started_at < interval '36 hours' then '4. 24 a 36 h'
               else '5. 36 a 48 h'
           end as faixa
    from atividades
    where started_at >= now() - make_interval(days => :dias)
) x
group by faixa
order by faixa;

-- ADR 0046 e 0058: o crescimento das evidências contra a estimativa (cerca de 9,5 MB por minuto de vídeo).
select '== Evidências (ADR 0046, 0058, 0060, 0061) ==';
select 'Arquivos', case when tipo_mime like 'video/%' then 'vídeo' else 'foto' end,
       count(*) || ' arquivos', round(sum(tamanho_bytes) / 1048576.0, 1) || ' MB'
from evidencias_atividade
where created_at >= now() - make_interval(days => :dias)
group by 2
order by 2;
select 'Crescimento', round(coalesce(sum(tamanho_bytes), 0) / 1048576.0 / :dias, 1) || ' MB por dia'
from evidencias_atividade
where created_at >= now() - make_interval(days => :dias);
select 'Vídeo', round(coalesce(sum(duracao_ms), 0) / 60000.0, 1) || ' minutos',
       coalesce(round(sum(tamanho_bytes) / 1048576.0 / nullif(sum(duracao_ms) / 60000.0, 0), 1) || ' MB por minuto', '-')
from evidencias_atividade
where created_at >= now() - make_interval(days => :dias) and tipo_mime like 'video/%';
select 'Origem da data', coalesce(origem_data_captura, 'sem data'), count(*)
from evidencias_atividade
where created_at >= now() - make_interval(days => :dias)
group by 2
order by count(*) desc;
select 'Prints', count(*) filter (where print) || ' prints',
       count(*) filter (where print and origem_data_captura = 'Print') || ' com a data do nome'
from evidencias_atividade
where created_at >= now() - make_interval(days => :dias);
-- ADR 0061: o local fica ligado por engano no treino em casa? Calistenia e Outro com local merecem um olhar.
select 'Local mostrado', a.tipo, count(*) filter (where e.latitude is not null) || ' de ' || count(*) || ' evidências'
from evidencias_atividade e
join atividades a on a.id = e.atividade_id
where e.created_at >= now() - make_interval(days => :dias)
group by a.tipo
order by a.tipo;

-- §13.2: quem joga gasta Energia toda semana? ADR 0059: alguém manda cartas a inativos para dar FADIGA?
select '== Jogo (gamificação §13.2, ADR 0059) ==';
-- A Energia é da pessoa, não do grupo: a semana segue o fuso do grupo mais antigo.
select 'Energia gasta, semana '
           || date_trunc('week', created_at at time zone (select fuso_horario from grupos order by created_at limit 1))::date,
       sum(preco) || ' EN', count(*) || ' cartas', count(distinct usuario_id) || ' pessoas'
from cartas
where created_at >= now() - make_interval(days => :dias)
group by 1
order by 1;
select 'Carta comprada', codigo, count(*)
from cartas
where created_at >= now() - make_interval(days => :dias)
group by codigo
order by count(*) desc;
select 'Jogadas', count(*) || ' enviadas',
       count(*) filter (where resposta = 'Aceita') || ' aceitas',
       count(*) filter (where resposta = 'Recusada') || ' recusadas',
       count(*) filter (where resposta is null and expira_em < now()) || ' expiradas',
       count(*) filter (where resposta is null and expira_em >= now()) || ' aguardando',
       count(*) filter (where carta_fusao_id is not null) || ' fusões'
from jogadas
where enviada_em >= now() - make_interval(days => :dias);
-- Inativo: sem atividade registrada nos 7 dias antes do envio.
select 'Jogadas para inativos', count(*) || ' enviadas',
       count(*) filter (where resposta = 'Recusada' or (resposta is null and expira_em < now())) || ' recusadas ou expiradas (FADIGA)'
from jogadas j
where j.enviada_em >= now() - make_interval(days => :dias)
  and not exists (
      select 1 from atividades a
      where a.usuario_id = j.destinatario_id
        and a.started_at between j.enviada_em - interval '7 days' and j.enviada_em);
-- Quem compra o quê, e as cartas ficam paradas? "Nunca jogadas" são as que não entraram em nenhuma Jogada,
-- nem como Resposta. Desde a decisão 0074 a Provocação jogada não volta: "nunca jogadas" é a Mão, fora as
-- Provocações que voltaram de Jogadas recusadas ou expiradas antes de 19/11/2026 (gamificação §6.2).
select 'Cartas de ' || pessoa, sum(n) || ' compradas', sum(energia) || ' EN',
       'nunca jogadas: ' || coalesce(string_agg(codigo || ' ' || paradas, ', ' order by codigo) filter (where paradas > 0), '-'),
       sum(provocacoes) || ' em Jogadas', sum(respostas) || ' usadas como Resposta'
from (
    select usuario_id, pessoa, codigo, count(*) as n, sum(preco) as energia,
           count(*) filter (where not provocacao and not resposta) as paradas,
           count(*) filter (where provocacao) as provocacoes,
           count(*) filter (where resposta) as respostas
    from (
        select c.usuario_id, split_part(u.nome_exibicao, ' ', 1) as pessoa, c.codigo, c.preco,
               exists (select 1 from jogadas j where c.id in (j.carta_id, j.carta_fusao_id)) as provocacao,
               exists (select 1 from jogadas j where j.carta_resposta_id = c.id) as resposta
        from cartas c
        join usuarios u on u.id = c.usuario_id
        where c.created_at >= now() - make_interval(days => :dias)
    ) y
    group by usuario_id, pessoa, codigo
) x
group by usuario_id, pessoa
order by sum(n) desc, pessoa;
-- As 30 Jogadas mais recentes, uma por linha. "Concluída" vem do aviso JogadaConcluida, que a limpeza das
-- notificações antigas apaga: numa Jogada antiga, a falta do aviso não quer dizer que ela não foi cumprida.
select 'Jogada ' || to_char(j.enviada_em at time zone g.fuso_horario, 'YYYY-MM-DD HH24:MI'),
       split_part(r.nome_exibicao, ' ', 1) || ' para ' || split_part(d.nome_exibicao, ' ', 1),
       c.codigo || coalesce(' + ' || cf.codigo || ' (FUSÃO)', '') || coalesce(' (objetivo ' || j.codigo_objetivo || ')', '')
           || case when j.origem_id is not null then ' (devolvida por DesafioDuplo)' else '' end,
       case
           when j.resposta = 'Recusada' then 'recusada'
           when j.resposta = 'Aceita' then 'aceita' || coalesce(' com ' || cr.codigo, '')
           when j.expira_em < now() then 'expirada sem resposta'
           else 'aguardando resposta'
       end,
       case
           when exists (select 1 from notificacoes n where n.jogada_id = j.id and n.tipo = 'JogadaConcluida') then 'concluída'
           when j.resposta = 'Aceita' and j.expira_em >= now() then 'prazo até ' || to_char(j.expira_em at time zone g.fuso_horario, 'YYYY-MM-DD HH24:MI')
           when j.resposta = 'Aceita' then 'sem aviso de conclusão'
           else '-'
       end
from jogadas j
join grupos g on g.id = j.grupo_id
join cartas c on c.id = j.carta_id
join usuarios r on r.id = j.remetente_id
join usuarios d on d.id = j.destinatario_id
left join cartas cr on cr.id = j.carta_resposta_id
left join cartas cf on cf.id = j.carta_fusao_id
where j.enviada_em >= now() - make_interval(days => :dias)
order by j.enviada_em desc
limit 30;
select 'Rivais escolhidos', count(*), count(distinct escolhedor_id) || ' pessoas escolheram'
from rivais
where created_at >= now() - make_interval(days => :dias);
