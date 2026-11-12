-- ADR 0050: os Enfeites ficam no chão da praça, e os que não faziam sentido no chão mudaram de objeto.
-- A vitrine e os Enfeites vistos guardam o código da família: troca os códigos antigos pelos novos, para
-- ninguém perder a vitrine nem rever o Enfeite como novo.
UPDATE avatares SET
    vitrine = array_replace(array_replace(array_replace(array_replace(array_replace(vitrine,
        'medalha', 'kettlebell'), 'cronometro', 'largada'), 'despertador', 'galo'), 'boia', 'piscina'),
        'bola', 'golzinho'),
    enfeites_vistos = array_replace(array_replace(array_replace(array_replace(array_replace(enfeites_vistos,
        'medalha', 'kettlebell'), 'cronometro', 'largada'), 'despertador', 'galo'), 'boia', 'piscina'),
        'bola', 'golzinho')
WHERE vitrine && '{medalha,cronometro,despertador,boia,bola}'
   OR enfeites_vistos && '{medalha,cronometro,despertador,boia,bola}';
