-- Cutucada com texto livre (decisoes/0064-cutucada-com-texto.md): o tipo `Livre` leva um texto de até 80 caracteres.
ALTER TABLE cutucadas ADD COLUMN texto text;
ALTER TABLE cutucadas DROP CONSTRAINT cutucadas_tipo_check;
ALTER TABLE cutucadas ADD CONSTRAINT cutucadas_tipo_check
    CHECK (tipo IN ('Sofa', 'Frango', 'Meta', 'Halter', 'Despertador', 'Fantasma', 'Livre'));
ALTER TABLE cutucadas ADD CONSTRAINT cutucadas_texto_check
    CHECK ((tipo = 'Livre') = (texto IS NOT NULL) AND (texto IS NULL OR char_length(texto) BETWEEN 1 AND 80));
