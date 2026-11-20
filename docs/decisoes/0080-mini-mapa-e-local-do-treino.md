# 0080 — Mini-mapa do comprovante e local do treino ao vivo

**Status:** DECIDIDA. Complementa as ADRs 0052, 0061 e 0078.

## Contexto

- A ADR 0061 mostra o local do comprovante só como um botão "Local", que abre o Google Maps fora do aplicativo. Quem avalia não vê onde foi o treino sem sair do app.
- A data da captura (`capturada_em`, `origem_data_captura`, ADR 0052) é guardada, mas a API não a devolvia. Quem avalia via só os avisos, não a hora da foto.
- No treino ao vivo (ADR 0078), a localização nunca saía do aparelho. Os participantes querem mostrar onde estão treinando, mesmo sem foto.
- A regra do repositório proíbe carregar recursos de servidores externos no navegador.

## Alternativas consideradas

- **Imagem do OpenStreetMap direto no navegador:** o mapa real mais simples, mas o celular de cada pessoa que olha manda as coordenadas ao OSM. Quebra a regra e expõe quem olhou.
- **Cartão estilizado, sem mapa real:** nada sai do servidor, mas não mostra o lugar.
- **O servidor busca o tile e o guarda em disco:** escolhida. O navegador só fala com o Wrexercise.

## Decisão

### Detalhes do comprovante

- A API devolve `capturadaEm` e `origemDataCaptura` em cada evidência.
- Abaixo do comprovante, uma faixa mostra: o tipo ("Foto tirada", "Vídeo gravado", "Print feito"), a hora da captura, o momento em relação ao treino ("12 min antes do início", "Durante o treino", "3h depois do fim") e a origem da data ("Dados da foto", "Câmera do app", "Data do arquivo", "Nome do print", "Movr"). "Data do arquivo" aparece em amarelo: é o sinal mais fraco.
- Com local, a faixa mostra o mini-mapa: um quadrado com o pino no centro. Um toque abre o Google Maps, como antes. A galeria em tela cheia mostra a mesma linha e um mapa menor.

### Mini-mapa pelo servidor

- `GET /api/atividades/{id}/evidencias/{evidenciaId}/mapa/{dx}/{dy}` e `GET /api/atividades/{id}/mapa/{dx}/{dy}`, com `dx` e `dy` de −1 a 1. As coordenadas vêm sempre do banco, e só de quem é membro do grupo: a rota não serve tiles de qualquer lugar.
- Zoom fixo 16 (nível de rua). O navegador monta 2×2 tiles e centraliza o pino.
- `CacheMapas` busca `tile.openstreetmap.org/{z}/{x}/{y}.png` com um `User-Agent` próprio, como pede a política de uso do OSM, e guarda o PNG em `Mapas:Diretorio` para sempre. Um tile já guardado nunca é buscado de novo.
- Sem resposta do OSM: `503 MAPA_INDISPONIVEL`. O navegador mostra só o pino sobre o padrão do app.
- O mapa mostra a atribuição "Mapa © OpenStreetMap".

### Local do treino ao vivo

- "Começar agora" tem a caixa "Mostrar ao grupo o local do treino", marcada por padrão (a mesma regra da ADR 0061). Marcada, o navegador pede a posição (GPS por até 6 s, depois a aproximada) e a envia com o início. Desmarcada, a posição não sai do aparelho.
- O servidor guarda `latitude` e `longitude` em `atividades` (migração 041). Coordenadas inválidas não impedem o treino: ele só começa sem local.
- A página da atividade, a tela de avaliação e o cronômetro mostram "Local do treino" com o mini-mapa.
- Fotos e vídeos enviados durante o treino continuam sem local.

## Impacto

- **Privacidade:** o OSM vê o IP do servidor e o tile, nunca quem olhou. O tile não diz de quem é o treino. O log não registra o tile nem as coordenadas.
- **Cache:** fica fora do backup e pode ser apagado a qualquer hora. Cada local novo custa 4 tiles de uns 10 a 30 KB.
- **Rede:** o servidor precisa de saída para a internet. Sem ela, o resto do app segue igual.
- **Exclusão:** apagar o local do treino junto com a conta entra em Q029, como o do comprovante.
- Relacionadas: 0052, 0061, 0078.
