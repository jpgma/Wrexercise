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
scripts/test-db.sh                                 # PostgreSQL dos testes de integração, na porta 5434
dotnet run --project Wrexercise -- migrate         # aplica as migrações
dotnet run --project Wrexercise                    # API em http://localhost:8080
cd Wrexercise/webapp && npm ci && npm run dev      # interface em http://localhost:5173
```

### Pilha completa no Windows

Para testar localmente o mesmo que vai para produção (Native AOT, PostgreSQL e Caddy), com o cadastro aberto:

```powershell
.\run.bat         # constrói, migra e sobe em http://localhost
.\run.bat --tunnel  # igual, e publica em https://wx-dev.jpgmaraujo.com.br
.\run.bat --tunnel-maquina  # igual, num túnel só desta máquina: https://wx-<nome da máquina>.jpgmaraujo.com.br
.\run.bat logs    # acompanha os logs da aplicação
.\run.bat backup  # grava um backup do banco e das evidências
.\run.bat restore 20261105-030000  # restaura um backup, com o app parado
.\run.bat stop    # para os serviços e mantém os dados
.\run.bat reset   # para os serviços e apaga os dados locais
```

Exige as portas 80 e 443 livres. Usa HTTP em `localhost`, sem aviso de certificado, em vez do HTTPS da produção. Os dados ficam em `%LOCALAPPDATA%\Wrexercise\local`.

Para testar o aplicativo inteiro sem montar dados à mão, com a pilha no ar:

```powershell
.\seed.bat          # recria o grupo "Seed de testes": 4 contas com histórico, Energia, conquistas e Enfeites
.\seed.bat --reset  # apaga as Jogadas do grupo do seed; as Cartas voltam para a Mão
```

Contas: `ana@seed.test`, `bruno@seed.test`, `carla@seed.test` e `diego@seed.test`, todas com a senha `wrexercise`. O que cada uma tem está em [`scripts/seed/README.md`](scripts/seed/README.md).

`--tunnel` publica esta máquina na internet por um túnel da Cloudflare, até `run.bat stop`. Na primeira vez, pede o login na Cloudflare pelo navegador e substitui o registro DNS de `wx-dev.jpgmaraujo.com.br`. O cadastro sem convite fica fechado: crie a primeira conta com `.\run.bat` antes de publicar. As credenciais do túnel ficam em `%LOCALAPPDATA%\Wrexercise\cloudflared`.

Se outra máquina também roda `--tunnel`, as duas viram conectores do mesmo túnel `wx-dev`, e a Cloudflare divide as requisições entre elas: metade cai num banco diferente. Use `--tunnel-maquina` numa delas. Ele cria o túnel `wx-<nome da máquina>` (o `%COMPUTERNAME%` em minúsculas) e o registro DNS do mesmo nome, sem mexer em `wx-dev`.

## Verificação

Não há CI. Rode antes de concluir qualquer trabalho:

```bash
scripts/verify.sh          # formatação, lint, build, testes unitários, build do frontend
scripts/verify.sh --full   # também testes de integração e, se algo que a afeta mudou, imagem arm64
scripts/verify.sh --image  # como --full, mas sempre constrói a imagem arm64
```

## Produção

A produção é um Galaxy A14 com Termux, em `https://wx.jpgmaraujo.com.br`. O PostgreSQL 18 e o túnel da Cloudflare (`a14`) são do repositório `Environment`, compartilhados com os outros apps do telefone; o app roda nativo em `~/wrexercise`. Pré-requisitos do telefone: `docs/decisoes/0029-implantacao-nativa-no-termux.md`. Decisões: `docs/decisoes/0032-a14-como-producao.md` e `docs/decisoes/0044-postgres-e-tunel-do-environment.md`. Plano para enxugar o Android do aparelho: `docs/a14-enxugamento.md`.

```powershell
adb forward tcp:8022 tcp:8022     # opcional: fala com o telefone por USB; use 127.0.0.1 como host
..\Environment\run.bat implantar  # uma vez: PostgreSQL e túnel do Environment
.\run.bat --ssh                   # constrói o app, envia ao Termux, provisiona o banco, migra e sobe o app
.\run.bat --ssh --scripts         # só os scripts de deploy, sem build; o app do telefone continua
```

Antes, abra o Termux no telefone e rode `sshd` (ele não sobe no boot). O perfil SSH é o compartilhado `%LOCALAPPDATA%\jpgma\a14.env`. Com o banco vazio, quem abrir o endereço primeiro cria a primeira conta (ver "Primeira conta").

### Estatísticas

```powershell
.\stats.bat           # CPU e memória do app, do PostgreSQL e do túnel, mais aparelho, banco, disco e backups
.\stats.bat --watch   # repete até Ctrl+C
```

Usa o perfil compartilhado `%LOCALAPPDATA%\jpgma\a14.env`; se faltar ou o telefone não responder, pergunta host, porta, usuário e chave. O PostgreSQL e o túnel aparecem aqui, mas são compartilhados: detalhes com `run.bat stats` no `Environment`.

### Backups

O telefone envia o backup cifrado ao Google Drive sozinho, uma vez por dia e antes das migrações de cada `run.bat --ssh` (ver `docs/decisoes/0046-backup-no-google-drive.md`). Datas em UTC.

- **Arquivo diário** `diarios/wrexercise-AAAAMMDD-HHMMSS[-implantacao].tar.gz`: dump do banco, avatares, logs e `versao.txt`. O Drive guarda 31 dias, e sempre os 7 mais novos; o telefone, os 3 mais novos.
- **Evidências:** espelho em `evidencias/`, que só envia as novas. O que sai do telefone fica 31 dias em `evidencias-removidas/<data>/`.
- **Logs:** cada backup leva `log/app.log` e o esvazia; no telefone ficam só os logs desde o último backup.
- O `Environment` continua gravando o dump local de todos os bancos em `~/ambiente/backups` (7 dias).

Situação: `remote-termux.sh status` ou `.\stats.bat` (avisam se o último envio tem mais de 26 h).

#### Configuração (uma vez)

Use de preferência uma conta Google só para backups (15 GB gratuitos, separados do Gmail e do Fotos).

1. No Termux: `pkg install rclone`.
2. No Windows: `winget install Rclone.Rclone` e, com o navegador logado na conta dos backups:

   ```powershell
   rclone authorize "drive" "eyJzY29wZSI6ImRyaXZlLmZpbGUifQ"   # {"scope":"drive.file"}: o rclone só vê o que ele criou
   ```

   Copie o token JSON que ele imprime.
3. No Termux, `rclone config`:
   - remoto `gdrive`: tipo `drive`, client ID e secret vazios (o padrão do rclone), escopo `drive.file`, sem configuração automática (`n`), e cole o token;
   - remoto `wx-cripto`: tipo `crypt`, `remote = gdrive:wrexercise`, cifrar nomes (`standard`), deixe o rclone gerar as duas senhas.

   Depois: `chmod 600 ~/.config/rclone/rclone.conf`.
4. **Guarde o `rclone.conf` no gerenciador de senhas.** Sem as senhas do crypt, ninguém lê o backup, nem você.
5. Teste: `sh ~/wrexercise/deploy/remote-termux.sh backup` e `rclone lsl wx-cripto:diarios`.

Com um client OAuth próprio, publique-o como "In production": em "Testing", o token expira em 7 dias e o backup para de enviar.

#### Comandos

```bash
sh ~/wrexercise/deploy/remote-termux.sh backup                    # backup agora
sh ~/wrexercise/deploy/remote-termux.sh restore                   # para o app e restaura o mais recente do Drive
sh ~/wrexercise/deploy/remote-termux.sh restore 20261105-030000   # ou um backup específico
sh ~/wrexercise/deploy/remote-termux.sh iniciar                   # depois de conferir
```

Restaurar o mais recente traz todas as evidências, de qualquer idade. Um backup mais antigo traz de volta também as evidências removidas depois dele.

Cópia extra dos arquivos diários (sem as evidências) para `BACKUP_DIR` no Windows, opcional:

```powershell
.\run.bat ssh-backup
```

#### Teste de restauração

De tempos em tempos, sem mexer na produção: no Windows, com uma cópia do `rclone.conf`, baixe o mais recente (`rclone copy wx-cripto:diarios/<arquivo> .` e `rclone copy wx-cripto:evidencias <pasta>`), suba a pilha local com `.\run.bat`, restaure o `wrexercise.dump` com `pg_restore --clean --if-exists --no-owner` no contêiner `db`, copie avatares e evidências para `%LOCALAPPDATA%\Wrexercise\local` e confira as atividades e as imagens em http://localhost.

Na pilha local de Docker, o backup continua em `BACKUP_DIR`: `.\run.bat backup`, `.\run.bat reset`, `.\run.bat` e `.\run.bat restore <data>`.

### Senha esquecida

Não há envio de e-mail. O operador gera um link de uso único, válido por 1 hora, e o passa à pessoa. O comando encerra as sessões da conta. No telefone:

```bash
sh ~/wrexercise/deploy/remote-termux.sh reset-senha pessoa@exemplo.com
```

### Primeira conta

Com o banco sem nenhum usuário, o app mostra a tela de configuração inicial (`/configuracao-inicial`). Abra o endereço do app logo após subir e crie a primeira conta ali. A tela se fecha para sempre assim que existe um usuário. Quem abrir o endereço primeiro cria a conta, então não deixe o app exposto sem abri-lo. Ver `docs/decisoes/0031-configuracao-inicial.md`.

### Novos membros

A primeira conta cria o primeiro grupo. Depois disso, o cadastro só funciona por convite: o Proprietário ou um Administrador gera um link de convite na tela do grupo, e quem o recebe cria a conta por ele. Cada link vale para uma pessoa, por 7 dias. Ver `docs/decisoes/0008-grupos-e-convites.md`.
