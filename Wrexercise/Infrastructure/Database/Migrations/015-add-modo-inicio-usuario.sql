-- Modo da tela inicial de cada conta: 'App' (tela atual) ou 'Jogo' (cena do grupo).
-- O padrão é 'App', então ninguém percebe mudança até escolher o Jogo (docs/gamificacao.md §2).
ALTER TABLE usuarios
    ADD COLUMN modo_inicio text NOT NULL DEFAULT 'App' CHECK (modo_inicio IN ('App', 'Jogo'));
