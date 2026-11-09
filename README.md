# Wrexercise

Aplicação web privada para pequenos grupos de amigos manterem uma rotina de exercícios, com desafios compartilhados, registro de atividades e validação comunitária.

.NET 10 Native AOT · PostgreSQL · React PWA · Termux (produção) · Docker Compose (local)

- Regras do repositório: [`AGENTS.md`](AGENTS.md)
- Arquitetura: [`docs/arquitetura.md`](docs/arquitetura.md)
- Questões em aberto: [`docs/questoes-abertas.md`](docs/questoes-abertas.md)
- Decisões: [`docs/decisoes/`](docs/decisoes/)

## Pré-requisitos

- .NET SDK 10.0.403 ou superior na faixa 10.0.4xx
- Node.js 24
- Docker
- No Windows, Git Bash para executar os scripts

## Desenvolvimento

```bash
scripts/dev-db.sh                                  # PostgreSQL na porta 5433
dotnet run --project Wrexercise -- migrate         # aplica as migrações
dotnet run --project Wrexercise                    # API em http://localhost:8080
cd Wrexercise/webapp && npm ci && npm run dev      # interface em http://localhost:5173
```

### Pilha completa no Windows

Para testar localmente o mesmo que vai para produção (Native AOT, PostgreSQL e Caddy), com o cadastro aberto:

```powershell
.\run.bat         # constrói, migra e sobe em http://localhost
.\run.bat --tunnel  # igual, e publica em https://wx-dev.jpgmaraujo.com.br
.\run.bat logs    # acompanha os logs da aplicação
.\run.bat backup  # grava um backup do banco e das evidências
.\run.bat restore 20261105-030000  # restaura um backup, com o app parado
.\run.bat stop    # para os serviços e mantém os dados
.\run.bat reset   # para os serviços e apaga os dados locais
```

Exige as portas 80 e 443 livres. Usa HTTP em `localhost`, sem aviso de certificado, em vez do HTTPS da produção. Os dados ficam em `%LOCALAPPDATA%\Wrexercise\local`.

`--tunnel` publica esta máquina na internet por um túnel da Cloudflare, até `run.bat stop`. Na primeira vez, pede o login na Cloudflare pelo navegador e substitui o registro DNS de `wx-dev.jpgmaraujo.com.br`. O cadastro sem convite fica fechado: crie a primeira conta com `.\run.bat` antes de publicar. As credenciais do túnel ficam em `%LOCALAPPDATA%\Wrexercise\cloudflared`.

## Verificação

Não há CI. Rode antes de concluir qualquer trabalho:

```bash
scripts/verify.sh          # formatação, lint, build, testes unitários, build do frontend
scripts/verify.sh --full   # também testes de integração e imagem arm64
```

## Produção

A produção é um Galaxy A14 com Termux, em `https://wx.jpgmaraujo.com.br`, por um túnel da Cloudflare chamado `wx`. Pré-requisitos do telefone: `docs/decisoes/0029-implantacao-nativa-no-termux.md`. Decisão: `docs/decisoes/0032-a14-como-producao.md`. Plano para enxugar o Android do aparelho: `docs/a14-enxugamento.md`.

```powershell
adb forward tcp:8022 tcp:8022   # opcional: fala com o telefone por USB; use 127.0.0.1 como host
.\run.bat --ssh                 # constrói o app, envia ao Termux, migra e sobe PostgreSQL, app e túnel
```

Antes, abra o Termux no telefone e rode `sshd` (ele não sobe no boot). Com o banco vazio, quem abrir o endereço primeiro cria a primeira conta (ver "Primeira conta").

### Estatísticas

```powershell
.\stats.bat           # CPU e memória do app, do PostgreSQL e do túnel, mais aparelho, banco, disco e backups
.\stats.bat --watch   # repete até Ctrl+C
```

Usa o perfil salvo por `run.bat --ssh`; se faltar ou o telefone não responder, pergunta host, porta, usuário e chave.

### Backups

No telefone, `deploy/backup.sh` grava o dump do banco e um arquivo das evidências em `~/wrexercise/dados/backups`, mantém os últimos 7 dias e confere se os arquivos podem ser lidos. As datas nos nomes estão em UTC. Ver `docs/decisoes/0022-backups.md` e `docs/decisoes/0032-a14-como-producao.md`.

Para gravar um backup no telefone e copiá-lo para o Windows:

```powershell
.\run.bat ssh-backup
```

Agende uma execução diária (o telefone precisa estar acessível por SSH):

```powershell
schtasks /create /sc daily /st 03:00 /tn WrexerciseBackup /tr "\"D:\Repos\jpgma\Wrexercise\run.bat\" ssh-backup"
```

Um backup que só existe no telefone não protege contra a perda do aparelho. Aponte `BACKUP_DIR` para uma pasta sincronizada (OneDrive, Google Drive); o padrão é `%LOCALAPPDATA%\Wrexercise\backups`. O `ssh-backup` não apaga cópias antigas no Windows. Os backups contêm as evidências e os hashes das senhas: mantenha a pasta privada.

Para restaurar no telefone, com o par `wrexercise-*.dump` e `evidencias-*.tar.gz` já em `~/wrexercise/dados/backups`:

```bash
sh ~/wrexercise/deploy/remote-termux.sh restore 20261105-030000
sh ~/wrexercise/deploy/remote-termux.sh iniciar
```

Teste a restauração de tempos em tempos, com a pilha local: `.\run.bat backup`, `.\run.bat reset`, `.\run.bat` e `.\run.bat restore <data>`. Depois, confira se as atividades e as imagens voltaram.

### Senha esquecida

Não há envio de e-mail. O operador gera um link de uso único, válido por 1 hora, e o passa à pessoa. O comando encerra as sessões da conta. No telefone:

```bash
sh ~/wrexercise/deploy/remote-termux.sh reset-senha pessoa@exemplo.com
```

### Primeira conta

Com o banco sem nenhum usuário, o app mostra a tela de configuração inicial (`/configuracao-inicial`). Abra o endereço do app logo após subir e crie a primeira conta ali. A tela se fecha para sempre assim que existe um usuário. Quem abrir o endereço primeiro cria a conta, então não deixe o app exposto sem abri-lo. Ver `docs/decisoes/0031-configuracao-inicial.md`.

### Novos membros

A primeira conta cria o primeiro grupo. Depois disso, o cadastro só funciona por convite: o Proprietário ou um Administrador gera um link de convite na tela do grupo, e quem o recebe cria a conta por ele. Cada link vale para uma pessoa, por 7 dias. Ver `docs/decisoes/0008-grupos-e-convites.md`.
