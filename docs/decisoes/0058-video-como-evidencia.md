# 0058 — Vídeo como evidência, câmera no aplicativo e rascunho do registro

**Status:** DECIDIDA. Complementa as ADRs 0011 (evidências), 0052 (avisos) e 0053 (idempotência).

## Contexto

- Participantes pediram para comprovar o treino com vídeos curtos, além de fotos.
- Um participante, no PWA instalado no Android, escolheu a câmera no seletor de arquivos, tirou a foto e voltou para um formulário vazio. As fotos escolhidas viviam só na memória da página (`anexos`, em `RegistrarAtividadePage`). Com pouca memória, o Android encerra o PWA enquanto o aplicativo de câmera do sistema está na frente, e o arquivo devolvido não encontra mais a página. O atributo `capture` não resolve: ele também abre o aplicativo de câmera do sistema.
- A produção é um Galaxy A14 no Termux (ADR 0032). O servidor nunca decodifica mídia: o navegador já reduz a foto, calcula SHA-256 e dHash, lê o EXIF e gera a miniatura (ADR 0011, 0052). O backend só depende do Npgsql e é Native AOT.
- O orçamento é zero. O backup externo é um espelho criptografado no Google Drive, com cota de 15 GB (ADR 0046).

## Alternativas consideradas

- **Converter o vídeo no servidor com ffmpeg:** recusada. O A14 converteria um minuto de vídeo em tempo comparável à duração, disputando CPU com a API, e exigiria uma fila de processamento, que a arquitetura proíbe.
- **ffmpeg.wasm no navegador:** recusada. Cerca de 30 MB para baixar e lento demais em celulares de entrada.
- **Regravar o vídeo num canvas com `MediaRecorder`:** recusada. Leva o tempo real do vídeo, a qualidade varia por navegador e o resultado é WebM no Chrome e MP4 no Safari.
- **WebCodecs com a biblioteca `mediabunny`:** escolhida. Usa o codificador de hardware do aparelho, lê MP4, MOV e WebM, recorta e grava MP4 com o `moov` no início. É carregada sob demanda (`import()` dinâmico), só quando a pessoa escolhe um vídeo.
- **Câmera do sistema por `<input capture>`:** recusada. Mantém o problema do Android encerrar o PWA.
- **Câmera dentro do aplicativo (`getUserMedia`):** escolhida. A pessoa não sai do aplicativo. A data da captura é a do momento, e o comprovante ganha o selo "Câmera do app".
- **Vídeo sem áudio:** recusada pelo mantenedor. O áudio dá contexto (contagem de repetições). Custa cerca de 0,5 MB por minuto.

## Decisão

### Câmera, galeria e rascunho

- O formulário tem dois botões: "Câmera" (câmera dentro do aplicativo) e "Galeria" (seletor de arquivos, sem `capture`).
- A captura da câmera do aplicativo tem a origem `Camera` em `origem_data_captura`, com a data do momento e sem localização. O servidor trata `Camera` como `Exif` no cálculo dos avisos. A API devolve `gravadaNoApp` em cada evidência, e a interface mostra o selo "Câmera do app". É um sinal do navegador, como o EXIF: um cliente modificado pode forjá-lo.
- O registro em andamento fica num rascunho no IndexedDB do aparelho (`services/rascunhos.ts`): campos, comprovantes já preparados e a chave de idempotência. O rascunho volta ao abrir o formulário, é apagado no envio, no "Descartar", ao sair da conta e depois de 48 horas. Se o envio cair no meio, repetir com a mesma chave devolve a mesma atividade (ADR 0053).

### Vídeo

- Formato guardado: MP4 com H.264 e áudio AAC. O navegador grava o `moov` antes do `mdat`; o servidor aceita os dois jeitos.
- O navegador converte para 720p (lado maior 1280 px), 30 quadros por segundo, cerca de 1,2 Mbps, com áudio AAC mono a 64 kbps. Um minuto ocupa cerca de 9,5 MB.
- Duração de 2 a 60 segundos. Um vídeo da galeria mais longo abre um recorte: a pessoa escolhe o trecho de 60 segundos.
- Sem WebCodecs para H.264 ou AAC, o navegador envia o original se ele for MP4 ou MOV com H.264, de até 60 segundos e 25 MB. Senão, recusa.
- Mistura livre: até 3 evidências por atividade, fotos ou vídeos.
- A miniatura do vídeo é o quadro do meio, gerado no navegador como a das fotos. O `dhash` é o desse quadro: `FotoParecida` compara vídeos e fotos.
- O navegador também envia `dhashInicio` e `dhashFim` (quadros a 10% e 90%). Se os três quadros quase não mudam, o servidor cria o aviso informativo `VideoParado`, que não impede a verificação automática: uma prancha filmada num tripé também parece parada.
- A data do vídeo vem do `creation_time` (`mvhd`) do original (origem `Exif`), senão da data do arquivo (`Arquivo`). A localização vem do texto ISO 6709 do `moov` (`©xyz` no Android, a chave `com.apple.quicktime.location.ISO6709` no iPhone). Um leitor próprio (`lerMetadadosVideo.ts`) lê só as caixas do topo, por fatias do arquivo. O arquivo convertido sai sem metadados.
- O `sha256Original` de um original acima de 64 MB cobre só os primeiros 8 MB, os últimos 8 MB e o tamanho: hashear centenas de MB travaria um celular de entrada.
- O tipo de comprovação continua `Fotografia` quando há vídeo: ele indica que há mídia, e mudar o valor exigiria mexer em todas as telas e nas regras de verificação sem ganho. As regras de `FotoForaDoPeriodo` e `FotoSemData` valem para vídeos; os nomes dos avisos ficam como estão (ADR 0052) e só os textos da interface dizem "foto ou vídeo".
- O servidor lê as caixas do MP4 em C#, sem biblioteca, e recusa: tipo de arquivo desconhecido, estrutura ilegível, duração fora de 2 a 61 segundos, sem faixa de vídeo, vídeo que não seja H.264, lado maior acima de 1920 px. Limite de 25 MB por vídeo; o das imagens continua 5 MB.
- O retorno do vídeo usa `Range`, que o `<video>` precisa para avançar e que o Safari exige.
- Retenção: igual à das fotos, sem prazo (a questão Q029 continua aberta).

### Backups

- Nada muda. No telefone, o backup é o `deploy/nuvem.sh` (ADR 0046): as evidências vão para o Drive por `rclone sync`, que só envia os arquivos novos, e o arquivo diário leva só o banco e os avatares. O tar diário de todas as evidências (ADR 0022) só existe na pilha local de Docker, usada para testes. O planejamento chegou a prever um tar por mês, partindo da leitura errada de que o telefone guardava 7 tars completos; como o efeito seria só na pilha de testes, a mudança não foi feita.

## Impacto

- Migrações `030-add-origem-camera.sql` e `031-add-video-evidencia.sql`.
- Novo `Infrastructure/Files/LeitorMp4.cs`. Nenhum pacote novo no backend.
- Dependência nova no frontend: `mediabunny` 1.64.0, versão fixa. Licença MPL-2.0: copyleft só nos arquivos da biblioteca, que entram sem modificação no bundle. Sem dependências de execução (só pacotes de tipos). Carregada sob demanda, num pedaço separado do bundle.
- Novo `Aviso`: `VideoParado`.
- Espaço: cerca de 9,5 MB por minuto de vídeo; no pior caso, cerca de 28 MB por atividade. Com 10 pessoas e um vídeo curto por dia, cerca de 1,5 GB por mês: a cota de 15 GB do Drive dura uns 9 meses. Medir depois do primeiro mês (`docs/TODO.md`).
- Compromissos aceitos:
  - Um cliente modificado pode enviar um vídeo sem converter, dentro de 25 MB, ou forjar a origem `Camera`.
  - `VideoParado` erra em exercícios parados; por isso só informa.
  - O áudio pode gravar a voz de outras pessoas.
  - O rascunho guarda fotos e vídeos no aparelho até o envio ou o descarte.

## Andamento

- Câmera no aplicativo, galeria, rascunho e origem `Camera`: feito.
- Vídeo no servidor: feito. A caixa `moov` pode vir antes ou depois do `mdat`: as câmeras de celular a gravam no fim, e exigir o `moov` no início recusaria quase todo original enviado sem conversão.
- Vídeo no navegador (câmera em modo vídeo, conversão, recorte, progresso de envio) e exibição (página da atividade, galeria, feed): feito.
