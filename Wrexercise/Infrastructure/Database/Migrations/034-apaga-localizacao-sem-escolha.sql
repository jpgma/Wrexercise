-- ADR 0061: a localização da evidência só é guardada quando o autor escolhe mostrá-la ao grupo, e todo o
-- grupo a vê. As coordenadas anteriores foram guardadas sem aviso e sem essa escolha: são apagadas.
UPDATE evidencias_atividade
SET latitude = NULL, longitude = NULL
WHERE latitude IS NOT NULL;
