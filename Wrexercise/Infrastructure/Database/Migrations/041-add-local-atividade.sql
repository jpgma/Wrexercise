-- ADR 0080: o treino ao vivo guarda onde começou, só quando a pessoa escolhe mostrar o local ao grupo.
ALTER TABLE atividades
    ADD COLUMN latitude double precision CHECK (latitude BETWEEN -90 AND 90),
    ADD COLUMN longitude double precision CHECK (longitude BETWEEN -180 AND 180),
    ADD CONSTRAINT atividades_coordenadas_check
        CHECK ((latitude IS NULL) = (longitude IS NULL));
