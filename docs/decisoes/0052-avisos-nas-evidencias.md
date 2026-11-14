# 0052 — Avisos sobre a evidência e o horário

**Status:** DECIDIDA. Complementa `0011-atividades.md` e `0012-verificacao-comunitaria.md`. Fecha a parte de hashes de Q020 e decide Q030 em parte.

## Contexto

Até aqui, qualquer foto da galeria servia de evidência, e o início da atividade podia ser de qualquer data no passado (`ValidacaoAtividade` só recusava mais de 1 hora no futuro). O navegador reencoda a foto num canvas, o que apaga o EXIF: o servidor nunca sabia quando a foto foi tirada. O `sha256` era gravado, mas nada o lia. O único sinal para quem avalia era a reação `Suspeito`, sem motivo.

Havia também um buraco de privacidade (Q030): se o canvas falhasse, o navegador enviava o arquivo original, com EXIF e GPS.

O grupo é pequeno e de amigos. Fotos repassadas pelo WhatsApp, prints e imagens editadas não têm data no EXIF. Recusar essas fotos bloquearia mais gente honesta do que fraudes.

## Alternativas consideradas

- **Recusar a foto com data fora do horário:** pega a fraude casual, mas erra com relógio errado, fuso, ou foto tirada depois. Sem recurso, o autor perde a evidência.
- **Ler o EXIF no servidor:** mais difícil de forjar, mas exige enviar o original (maior e com GPS) e escrever no servidor, sem biblioteca, um leitor e um removedor de EXIF compatíveis com Native AOT.
- **Exigir foto pela câmera do app (`capture`):** o Android ainda deixa escolher da galeria, e o iOS ignora o atributo para fotos antigas. Não resolve.
- **Avisos automáticos para o grupo, lidos no navegador:** escolhida.

## Decisão

### Sinais lidos no navegador

Antes do canvas, o navegador lê da foto original e envia em campos à parte:

- `capturadaEm` e `origemDataCaptura`: a data `DateTimeOriginal` do EXIF (com `OffsetTimeOriginal`, quando existe). Sem ela, a data do arquivo (`File.lastModified`), com origem `Arquivo`.
- `sha256Original`: o SHA-256 do arquivo original.
- `dhash`: um hash perceptual de 64 bits (a imagem em 9×8 tons de cinza).
- `latitude` e `longitude`: o GPS do EXIF, quando existe.

O arquivo enviado continua sem EXIF. Se o navegador não consegue processar a imagem, ela não é enviada (fecha o buraco de Q030). O leitor de EXIF é escrito à mão em `lerMetadadosFoto.ts`, sem dependência. Tudo isso pode ser forjado por quem quiser: são avisos, não prova.

### Avisos (`Aviso`)

| Tipo | Quando | Impede a verificação automática |
|---|---|---|
| `FotoForaDoPeriodo` | Data do EXIF fora de [início − 2 h, fim + 12 h], ou no futuro. Data do arquivo antes do início − 2 h. | Sim |
| `FotoSemData` | Sem data no EXIF (a data do arquivo dentro ou depois da janela não diz nada). | Não: só informa |
| `FotoParecida` | `dhash` a no máximo 6 bits de uma foto de outra atividade, em qualquer grupo de quem envia. Os dois hashes precisam ter de 12 a 52 bits ligados. | Sim |
| `RegistroTardio` | Registro mais de 12 h depois do fim do treino. | Sim |

- A data do arquivo só vale para trás: copiar ou receber o arquivo muda essa data para depois, nunca para antes da foto.
- Todos os membros do grupo veem os avisos, inclusive o autor, no feed, na página da atividade e na tela de avaliação. O link para a atividade da foto parecida só aparece para quem também pode vê-la.
- O formulário mostra ao autor, antes do envio, o aviso que a data da foto vai gerar. Assim ele corrige um horário digitado errado, que é a causa mais comum.

### Regras novas

- **Início até 48 h no passado** (`ATIVIDADE_MUITO_ANTIGA`): o mesmo prazo da janela de avaliação.
- **Foto repetida é recusada** (`409 EVIDENCIA_DUPLICADA`): o mesmo SHA-256 (do arquivo enviado ou do original) já usado em qualquer grupo de quem envia, inclusive na mesma atividade.
- **Com aviso que impede, o silêncio não verifica:** ao fim da janela, a atividade vai para `EmRevisao`, e não para `Verificada`. Se todos os revisores reagirem com `Kudos`, ela é verificada normalmente: o grupo decidiu. Sem revisores, nada muda.
- **Ninguém decide a própria disputa** (`403 REVISAO_PROPRIA_NAO_PERMITIDA`): vale para toda atividade em `EmRevisao`, com ou sem aviso.

### Localização

A latitude e a longitude do EXIF ficam guardadas em `evidencias_atividade`. Nenhuma resposta da API as devolve, e o log não as registra. Mostrá-las ao grupo, usá-las num aviso ou apagá-las é uma decisão futura.

## Impacto

- **Migração 027:** colunas novas em `evidencias_atividade` e a tabela `avisos_atividade`.
- **Mesma foto em dois grupos:** é recusada. Quem posta o mesmo treino em dois grupos precisa de outra foto. Compromisso aceito para pegar o reaproveitamento entre grupos.
- **Falsos positivos de `FotoParecida`:** selfies no mesmo espelho da academia podem ficar a menos de 6 bits. Revisar o limite com dados reais (`AvisosEvidencia.DistanciaMaximaFotoParecida`). **Atualização (2026-11-13):** em produção, prints de apps com fundo liso (resumo do treino, Strava, Samsung Health, Wellhub) deram hashes com 2 a 4 bits ligados, a 4 bits uns dos outros, e geraram o aviso entre imagens que não se parecem. O dHash só liga um bit quando um ponto é mais claro que o vizinho da direita, e a cor lisa deixa os bits desligados. Agora só entram na comparação hashes com 12 a 52 bits ligados (`AvisosEvidencia.IsDhashInformativo`).
- **Prints sem data (2026-11-14):** todo print recebia `FotoSemData`. A ADR 0060 lê a data do nome dos prints do Android, do Windows e do macOS (origem `Print`, que vale como `Exif`) e tira o selo "Foto sem data" do feed.
- **Sem foto, sem aviso de foto:** uma atividade `RegistroManual` só recebe `RegistroTardio`. O limite de 48 h vale para todas.
- **Privacidade:** a localização passa a ser um dado pessoal guardado. Os participantes devem saber disso; avisar na tela de registro fica para a decisão sobre exibição. A exclusão entra em Q029.
- **Revisão:** depois do primeiro mês de uso real, conferir quantos avisos houve de cada tipo e quantos viraram `Rejeitada`.
- Relacionadas: 0011, 0012, 0015, 0041.
