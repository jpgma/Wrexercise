# 0073 — Apagar com reações, vídeo gravado no iPhone e rastro das recusas

**Status:** DECIDIDA. Muda `0054-correcao-de-atividade.md` (quando o autor apaga). Complementa `0058-video-como-evidencia.md` (vídeo sem conversão) e `0056-erros-recentes.md` (o que `erros.bat` lê).

## Contexto

Em 18/11/2026, dois testes em produção mostraram quatro problemas:

- **Apagar:** o autor só apagava a própria atividade em `AguardandoFeedback` (0054). Um `Suspeito` de teste levou a atividade a `EmRevisao` e fechou a exclusão em segundos. Uma atividade anulada também ficava para sempre. Foi preciso apagar os testes direto no banco.
- **Vídeo do iPhone:** três atividades ficaram como `Fotografia` e sem nenhuma evidência. O vídeo gravado na câmera do aplicativo foi preparado no aparelho e recusado pelo servidor. O `MediaRecorder` do Safari grava MP4 fragmentado (`moof`/`mdat`), com a duração zerada no `mvhd`. Sem `AudioEncoder` (iOS anterior ao 26), o navegador não converte e envia o original (0058). O `LeitorMp4` lê só o `moov`, vê duração 0 e responde `VIDEO_MUITO_CURTO`. A causa foi reproduzida fora do iPhone, fragmentando um MP4 comum; falta confirmar num aparelho.
- **Sem rastro:** a recusa de uma evidência é um `400`, e o servidor não registrava nada. Além disso, o backup diário esvazia o `app.log` (0046), e `erros.bat` lia só o arquivo atual: no dia seguinte, não havia o que ler.
- **Sem saída na tela:** quando o envio de uma evidência falha, a tela "Boa! Tá registrado." só dizia que um comprovante não foi enviado. A atividade ficava sem evidência.

## Alternativas consideradas

- **Apagar também a `Verificada`:** recusada. Ela já pode ter virado Energia e Jogada concluída (0054); desfazer isso é muito código para um caso raro em 15 minutos.
- **Apagar só em `AguardandoFeedback` e `EmRevisao`:** resolve a reação de teste, mas não o registro anulado ou rejeitado por engano dentro do prazo.
- **Apagar em qualquer situação menos `Verificada`, nos 15 minutos:** escolhida.
- **Servidor aceitar MP4 fragmentado:** exige ler `mvex` e somar os `moof` em C#, e o arquivo guardado continuaria fragmentado, pior para avançar o vídeo. Recusada: o servidor não processa mídia (0058).
- **Reempacotar no navegador:** escolhida. O `mediabunny`, que já está no projeto, copia as faixas para um MP4 comum sem recodificar.
- **Fila de reenvio offline das evidências:** recusada; as escritas não têm fila offline (`webapp/AGENTS.md`). O reenvio é manual e só na tela aberta.

## Decisão

### O autor apaga

- A janela de 15 minutos continua. Dentro dela, o autor apaga em qualquer situação menos `Verificada`: `AguardandoFeedback`, `EmRevisao`, `Rejeitada` e `Anulada`. Reações de qualquer tipo não impedem.
- Fora disso, continua `409 EXCLUSAO_NAO_PERMITIDA`, agora com o texto "Só dá para apagar nos primeiros 15 minutos, antes de o treino ser verificado."
- Risco aceito: quem recebe `Suspeito` pode apagar e registrar de novo para fugir da revisão, mas só nos primeiros 15 minutos, e o grupo recebe a notificação do registro novo.

### Vídeo sem conversão

- Quando o navegador não converte e o original é aceito (0058), ele reempacota o original antes de enviar: copia as faixas H.264 e AAC para um MP4 comum, com o `moov` no início e a duração no `mvhd`. Nada é recodificado, e as etiquetas do original não vão junto.
- Se o reempacotamento falhar, vai o original, como antes.
- O servidor não muda.

### Rastro das recusas

- Cada evidência recusada gera uma linha `Warning` na categoria `Wrexercise.EvidenciaRecusada`: o código, a atividade, o motivo interno, o tamanho e o tipo informado. No vídeo fora das regras, o motivo traz os codecs, as dimensões e a duração lidos. Nunca o nome do arquivo, o conteúdo ou a localização. O cliente continua recebendo só o código.
- `remote-termux.sh erros` lê o `app.log` atual e o `app.log` guardado nos backups que ainda estão no telefone (os 3 últimos), do mais antigo ao mais recente, e mostra também as linhas `Warning`.

### Tentar de novo

- Na tela "Boa! Tá registrado.", as evidências que falharam sem recusa do servidor (sem conexão, `429`, `5xx`) ficam guardadas na memória da tela, e o botão "Tentar de novo" as reenvia.
- Uma recusa (`400`, `409`) não oferece reenvio: o resultado seria o mesmo. O motivo aparece no aviso.
- Ao sair da tela, o que faltou se perde. O servidor continua aceitando evidências só do autor e só em `AguardandoFeedback`.

## Impacto

- `ExclusaoAtividade.PermitidaAte` troca `== AguardandoFeedback` por `!= Verificada`. Sem migração.
- `prepararVideo.ts` ganha `reempacotar`. Sem dependência nova.
- `AnexarEvidencia` recebe `ILoggerFactory`.
- O log passa a ter linhas `Warning` do aplicativo; `stats.bat` continua contando só `Error` e `Critical`.
- Falta testar a gravação num iPhone depois da implantação. Se o vídeo ainda for recusado, a linha `Wrexercise.EvidenciaRecusada` diz o motivo.
