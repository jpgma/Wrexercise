# 0050 — Enfeites no chão da praça

**Status:** DECIDIDA. Altera o que `0037-avatar-importado-e-enfeites.md` e `gamificacao.md` §3.1 e §9 diziam sobre como a cena mostra os Enfeites.

## Contexto

A cena mostrava só o primeiro Enfeite da vitrine, um ícone de 16 px ao lado dos pés do avatar. Ele passava despercebido, e o segundo e o terceiro só apareciam no painel do Foco. Vários objetos também não faziam sentido no chão: as três medalhas, o cronômetro, o despertador e o cachorro com a guia solta no ar.

Pôr objetos do tamanho de um avatar na praça tem um risco: com 10 membros e 3 Enfeites cada, seriam 30 objetos grandes numa praça de 180×200 px (mestre), cobrindo a cena.

## Alternativas consideradas

- **Sempre os 3, ao lado do avatar.** Mostra tudo, mas com 6 ou mais membros os vizinhos ficam a cerca de 37 px (mestre) uns dos outros: não cabe.
- **Revezar os 3.** Um por pessoa, trocando a cada tantos segundos. A densidade fica constante, mas os Enfeites piscam e quem abre a cena por pouco tempo vê só um.
- **Vagas fixas nas bordas.** Pontos escolhidos à mão junto da mureta e dos cantos, por ordem de entrada. A composição fica limpa, mas o objeto perde o vínculo com o dono.
- **Um no chão e os 3 no Foco.** Simples, mas grupos pequenos têm chão de sobra e continuariam vendo um só.

## Decisão

- **Orçamento pelo tamanho do grupo:** até 3 membros, 3 Enfeites por pessoa; 4 ou 5, 2; 6 ou mais, 1. No Foco, os Enfeites do alvo que não estavam no chão caem em volta dele. O Enfeite novo cai no chão na encenação, mesmo além do orçamento.
- **Cantinho do dono:** o Enfeite fica fora da elipse, atrás de quem está na metade de trás e ao lado de quem está na metade da frente. Ele é ordenado pela base, como avatares e objetos. Regras: não cobre a cabeça nem o tronco de quem está atrás dele; atrás de alguém, ao menos metade aparece; dois Enfeites nunca dividem o chão; o caminho da porta e os objetos da praça ficam livres. Sem chão livre, ele não aparece. A conta é pura, em `enfeitesNaPraca.ts`.
- **Tamanho real:** três portes na escala do mestre da praça (P ≈16×16, M ≈24×24, G até 36×40), num atlas de células de 40×40 com o objeto apoiado na base. O maior tem a altura de um avatar.
- **Animação:** ciclo parado de 4 quadros, fora de sincronia. O poste fica apagado de dia e aceso no entardecer e à noite. A interação com o dono (o cachorro que segue, a caixa que pula) fica para depois; o atlas aceita linhas novas.
- **Objetos novos:** medalhas → kettlebell de bronze, prata e ouro; cronômetro → bloco de largada com tênis; despertador → galo; troféu → troféu no pedestal; bola → golzinho com bola; boia → piscininha com boia. O cachorro perde a guia. Os códigos de família que mudaram de objeto mudam junto (`medalha→kettlebell`, `cronometro→largada`, `despertador→galo`, `boia→piscina`, `bola→golzinho`).
- **Arte:** gerada no ChatGPT, primeiro uma folha-base parada (estilo e escala) e depois uma tira de 4 quadros por objeto a partir do recorte da base; `pixel-art/enfeites/build.py` reduz tudo à paleta.

## Impacto

- A migração `026-renomeia-enfeites.sql` troca os códigos antigos em `avatares.vitrine` e `avatares.enfeites_vistos`, para ninguém perder a vitrine nem rever um Enfeite como novo.
- Os 19 `enfeite-*.png` dão lugar ao atlas `enfeites.png`. Até a arte nova chegar, o build monta o atlas com os ícones antigos, parados.
- O pombo não pousa sobre um Enfeite.
