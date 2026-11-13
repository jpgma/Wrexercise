# 0051 — Monitor externo do `/ready`

**Status:** DECIDIDA. Complementa `arquitetura.md` §14 (Observabilidade) e o item "Fora desta decisão" de `0046-backup-no-google-drive.md`.

## Contexto

A produção é o Galaxy A14 (0032). Ela para se o Android matar o Termux, a bateria acabar, o Wi-Fi cair ou o túnel da Cloudflare sair do ar. `/health` e `/ready` existem, mas ninguém os consulta de fora. Hoje, quem descobre a queda é um participante.

Em 2026-11-12, o envio ao Drive ficou parado por 20 h, e só o `stats.bat` mostrava isso (ver 0022, "Último teste de restauração"). Esse caso não entra aqui: fica nos compromissos abaixo.

Nada disso pode pesar no telefone, e o orçamento é zero. `AGENTS.md` proíbe "observabilidade dedicada" sem uma decisão registrada: esta é a decisão.

## Alternativas consideradas

- **Aviso do próprio telefone (Termux:API):** não avisa justamente quando o telefone, o Termux ou a rede caem.
- **Workflow agendado no GitHub Actions:** sem conta nova, mas com repositório privado gasta a cota gratuita de minutos (cada execução conta como 1 minuto, e a cada 15 min isso passa de 2.800 minutos por mês). O agendamento também atrasa sem garantia, e 0003 decidiu ficar sem CI.
- **Uptime Kuma no PC:** sem conta externa, mas só vigia com o PC ligado.
- **Better Stack:** uma conta cobre o monitor HTTP e um heartbeat do backup, mas o plano gratuito é mais restrito e muda com frequência.
- **Heartbeat do backup (Healthchecks.io ou Better Stack):** o `nuvem.sh` chamaria uma URL depois de cada envio bem-sucedido, e a falta dessa chamada geraria um alerta. Pegaria o caso de 2026-11-12, mas exige código no telefone e um segredo novo. Fica para depois (ver Impacto).
- **UptimeRobot, plano gratuito:** checagem HTTP de fora a cada 5 min, alerta por e-mail e pelo app, sem código e sem nada no telefone.

## Decisão

- Um monitor HTTP(s) do **UptimeRobot**, no plano gratuito, consulta `https://wx.jpgmaraujo.com.br/ready` a cada 5 min.
- Vigia o `/ready`, não o `/health`: o `/ready` faz um `SELECT 1`. Assim, uma resposta 200 confirma de uma vez o telefone, o Termux, o túnel, o app e o PostgreSQL. Com o banco fora, ele responde 503.
- O alerta vai por e-mail, para um endereço que o mantenedor lê, e por push no app do UptimeRobot no celular pessoal (não no A14).
- A configuração fica só na conta do UptimeRobot. Não há código, segredo nem arquivo novo no repositório. O passo a passo está no README ("Monitor externo").

## Impacto

- **Custo no telefone:** 288 requisições por dia, cada uma com um `SELECT 1`. O `/ready` não grava log quando dá certo, e nenhum limitador de taxa se aplica a ele.
- **Conta externa nova:** sem dados do grupo. O UptimeRobot só vê a URL pública e o código HTTP.
- **Quedas curtas:** com checagem a cada 5 min, uma queda menor que isso pode passar sem alerta. É aceitável.
- **Compromisso aceito:** uma falha do backup continua visível só no `stats.bat` e em `remote-termux.sh status`. Se outra falha de backup passar despercebida, acrescente o heartbeat descrito acima: o `nuvem.sh` chama `<url>` depois de cada envio bem-sucedido e `<url>/fail` numa falha.
- **Revisão:** se o UptimeRobot mudar o plano gratuito, qualquer monitor HTTP externo serve. Basta a mesma URL.
- Relacionadas: 0022, 0029, 0032, 0044, 0046.
