@echo off
rem Sobe localmente a mesma pilha da produção: Native AOT, PostgreSQL e Caddy.
rem Requer Docker Desktop, com as portas 80 e 443 livres. Só para testes: a senha abaixo não é segredo.
rem   run.bat              constrói a imagem, aplica as migrações e sobe os serviços
rem   run.bat --rebuild    igual ao padrão, mas reconstrói a imagem sem cache do Docker
rem                          (frontend wwwroot + Native AOT) e recria o contêiner app
rem   run.bat rebuild      atalho para --rebuild
rem   run.bat --tunnel     igual ao padrão, mas publica em https://wx-dev.jpgmaraujo.com.br por um túnel
rem                          da Cloudflare. Pode ser combinado com --rebuild. Na primeira vez, pede o
rem                          login na Cloudflare pelo navegador. Com o banco vazio, quem abrir o link
rem                          primeiro cria a primeira conta: abra-o logo após subir.
rem   run.bat --tunnel-maquina  igual a --tunnel, mas com um túnel só desta máquina, em
rem                          https://wx-<nome da máquina>.jpgmaraujo.com.br. Use quando outra máquina
rem                          já publica em wx-dev: dois conectores no mesmo túnel dividem o tráfego.
rem   run.bat --ssh        em vez de subir aqui, implanta o app no celular Android (Termux, sem root) por SSH.
rem                          O app roda nativo; PostgreSQL e túnel da Cloudflare são do repositório
rem                          Environment, que precisa estar implantado antes. Usa o perfil SSH compartilhado
rem                          %LOCALAPPDATA%\jpgma\a14.env (pergunta o que faltar). Exige chave SSH (sem senha)
rem                          e o sshd ligado no Termux. Pode ser combinado com --rebuild. Antes das
rem                          migrações, grava um backup e o envia ao Google Drive (decisão 0046). Ver
rem                          docs/decisoes/0029-implantacao-nativa-no-termux.md e 0044.
rem   run.bat --ssh --scripts  envia só deploy/*.sh e reinicia o app que já está no celular, sem build
rem   run.bat logs         acompanha os logs da aplicação
rem   run.bat backup       grava um backup do banco e das evidências em BACKUP_DIR
rem   run.bat ssh-backup   roda o backup no celular (banco, avatares e logs; envia ao Google Drive com as
rem                          evidências) e copia os últimos arquivos dele para BACKUP_DIR. O celular já faz
rem                          o backup sozinho todo dia: este comando é uma cópia extra, sem as evidências.
rem                          Não apaga cópias antigas daqui. Ver docs/decisoes/0046-backup-no-google-drive.md.
rem   run.bat configurar-backup  configura tudo do backup no Google Drive, neste PC e no celular, com
rem                          perguntas: rclone (winget e pkg), login no Google, remoto cifrado, cópia da
rem                          configuração para o celular, tarefa de backup, teste e cópia extra agendada.
rem                          Também aceita --configurar-backup. Pode rodar de novo. Ver decisão 0046.
rem   run.bat restore D    restaura o backup da data D (AAAAMMDD-HHMMSS), com o app parado
rem   run.bat stop         para os serviços e mantém os dados
rem   run.bat reset        para os serviços e APAGA os dados locais
chcp 65001 >nul
setlocal
cd /d "%~dp0"

rem HTTP evita o aviso de certificado. Os navegadores tratam localhost como seguro
rem e aceitam o cookie Secure da sessão; só o Caddy responde a este nome.
set "URL=http://localhost"
set "DATA_ROOT=%LOCALAPPDATA%\Wrexercise\local"

rem O compose.yaml exige estas variáveis em qualquer comando, inclusive logs e down.
set "DOMAIN=%URL%"
set "DB_PASSWORD=wrexercise_local"
set "DB_CONNECTION=Host=db;Port=5432;Database=wrexercise;Username=wrexercise;Password=wrexercise_local"
set "WREXERCISE_IMAGE=wrexercise:local"
set "DATA_DIR=%DATA_ROOT:\=/%"

rem Fora de DATA_ROOT: run.bat reset apaga os dados, mas mantém os backups.
rem Defina BACKUP_DIR antes, por exemplo com uma pasta do OneDrive, para ter cópia fora desta máquina.
if not defined BACKUP_DIR set "BACKUP_DIR=%LOCALAPPDATA%\Wrexercise\backups"
set "BACKUP_DIR=%BACKUP_DIR:\=/%"

rem Fora de DATA_ROOT: run.bat reset apaga os dados, mas mantém o túnel.
set "TUNNEL_HOST=wx-dev.jpgmaraujo.com.br"
set "TUNNEL_NAME=wx-dev"
set "TUNNEL_ROOT=%LOCALAPPDATA%\Wrexercise\cloudflared"
set "TUNNEL_DIR=%TUNNEL_ROOT:\=/%"
set "TUNNEL_IMAGE=cloudflare/cloudflared:2026.10.3"
set "CLOUDFLARED=docker run --rm -v "%TUNNEL_DIR%:/home/nonroot/.cloudflared" %TUNNEL_IMAGE% tunnel"

rem Projeto próprio: não se mistura com o banco de desenvolvimento (wrexercise-dev).
set "COMPOSE=docker compose -p wrexercise-local -f deploy/compose.yaml"

set "DOCKER_BUILD_EXTRAS="
set "REBUILD="
set "TUNNEL="
set "SSH_MODE="
set "SO_SCRIPTS="
set "TUNNEL_LOG_HINT=docker logs wrexercise-local-tunnel-1"

:parse
if "%~1"=="" goto :start
if /i "%~1"=="--rebuild" (
  set "DOCKER_BUILD_EXTRAS=--no-cache"
  set "REBUILD=1"
  shift
  goto :parse
)
if /i "%~1"=="rebuild" (
  set "DOCKER_BUILD_EXTRAS=--no-cache"
  set "REBUILD=1"
  shift
  goto :parse
)
if /i "%~1"=="--tunnel" (
  set "TUNNEL=1"
  shift
  goto :parse
)
if /i "%~1"=="--tunnel-maquina" (
  set "TUNNEL=1"
  rem Um rótulo só (wx-nome, não wx-dev.nome): o certificado gratuito da Cloudflare cobre um nível.
  for /f %%n in ('powershell -NoProfile -Command "$env:COMPUTERNAME.ToLower() -replace '[^a-z0-9-]','-'"') do (
    set "TUNNEL_NAME=wx-%%n"
    set "TUNNEL_HOST=wx-%%n.jpgmaraujo.com.br"
  )
  shift
  goto :parse
)
if /i "%~1"=="--ssh" (
  set "SSH_MODE=1"
  shift
  goto :parse
)
if /i "%~1"=="--scripts" (
  set "SO_SCRIPTS=1"
  shift
  goto :parse
)
if /i "%~1"=="logs" goto :logs
if /i "%~1"=="backup" goto :backup
if /i "%~1"=="ssh-backup" goto :ssh_backup
if /i "%~1"=="configurar-backup" goto :configurar_backup
if /i "%~1"=="--configurar-backup" goto :configurar_backup
if /i "%~1"=="restore" goto :restore
if /i "%~1"=="stop" goto :stop
if /i "%~1"=="reset" goto :reset
echo Uso: run.bat [--rebuild^|rebuild] [--tunnel^|--tunnel-maquina^|--ssh [--scripts]] ^| logs ^| backup ^| ssh-backup ^| configurar-backup ^| restore AAAAMMDD-HHMMSS ^| stop ^| reset
exit /b 2

:start
if defined SSH_MODE goto :ssh
if defined TUNNEL (
  set "COMPOSE=%COMPOSE% -f deploy/compose.tunnel.yaml"
  call :prepare_tunnel
  if errorlevel 1 goto :fail
)

echo.
if defined REBUILD (
  echo ==^> Construindo %WREXERCISE_IMAGE% do zero ^(--no-cache^)
) else (
  echo ==^> Construindo %WREXERCISE_IMAGE% para esta máquina
)
docker buildx build %DOCKER_BUILD_EXTRAS% -f deploy/Dockerfile -t %WREXERCISE_IMAGE% --load .
if errorlevel 1 goto :fail

echo.
echo ==^> Aplicando migrações
%COMPOSE% run --rm app migrate
if errorlevel 1 goto :fail

echo.
echo ==^> Subindo os serviços
if defined REBUILD (
  rem Proxy também recriado para garantir que nada fique apontando para app antigo.
  %COMPOSE% up -d --force-recreate --remove-orphans
) else (
  rem Sem --tunnel, remove o túnel que tenha ficado de uma execução anterior.
  %COMPOSE% up -d --remove-orphans
)
if errorlevel 1 goto :fail

echo.
echo ==^> Verificando %URL%/ready
curl.exe -fs --retry 30 --retry-all-errors --retry-delay 1 --max-time 5 %URL%/ready
if errorlevel 1 goto :fail

echo.
echo.
echo Wrexercise rodando em %URL%
if defined TUNNEL call :check_tunnel
echo Para parar: run.bat stop
exit /b 0

:prepare_tunnel
if not exist "%TUNNEL_ROOT%" mkdir "%TUNNEL_ROOT%"
if errorlevel 1 exit /b 1
if not exist "%TUNNEL_ROOT%\cert.pem" (
  echo.
  echo ==^> Abra o link abaixo e autorize a zona jpgmaraujo.com.br
  %CLOUDFLARED% login
  if errorlevel 1 exit /b 1
)
if exist "%TUNNEL_ROOT%\%TUNNEL_NAME%.json" goto :route_tunnel
rem O túnel pode já existir na conta, criado em outra máquina ou antes de apagarem a pasta
rem local: nesse caso, só baixa as credenciais de novo.
%CLOUDFLARED% info %TUNNEL_NAME% >nul 2>&1
if errorlevel 1 (
  echo.
  echo ==^> Criando o túnel %TUNNEL_NAME%
  %CLOUDFLARED% create --cred-file /home/nonroot/.cloudflared/%TUNNEL_NAME%.json %TUNNEL_NAME%
) else (
  echo.
  echo ==^> Baixando as credenciais do túnel %TUNNEL_NAME%, que já existe
  %CLOUDFLARED% token --cred-file /home/nonroot/.cloudflared/%TUNNEL_NAME%.json %TUNNEL_NAME%
)
if errorlevel 1 exit /b 1
:route_tunnel
echo.
echo ==^> Apontando %TUNNEL_HOST% para o túnel
%CLOUDFLARED% route dns --overwrite-dns %TUNNEL_NAME% %TUNNEL_HOST%
exit /b %errorlevel%

:check_tunnel
rem Não é falha: o DNS desta máquina pode demorar a enxergar o registro novo.
echo.
echo ==^> Verificando https://%TUNNEL_HOST%/ready
curl.exe -fs --retry 30 --retry-all-errors --retry-delay 2 --max-time 5 https://%TUNNEL_HOST%/ready
if errorlevel 1 (
  echo Ainda sem resposta. Se persistir, veja: %TUNNEL_LOG_HINT%
) else (
  echo.
  echo Wrexercise publicado em https://%TUNNEL_HOST%
)
exit /b 0

:logs
%COMPOSE% logs -f app
exit /b %errorlevel%

:backup
%COMPOSE% run --rm backup backup
if errorlevel 1 goto :fail
echo Backups em %BACKUP_DIR%
exit /b 0

:restore
if "%~2"=="" (
  echo Uso: run.bat restore AAAAMMDD-HHMMSS. Backups em %BACKUP_DIR%
  exit /b 2
)
rem O app fica parado para ninguém escrever no banco durante a restauração.
%COMPOSE% stop app
if errorlevel 1 goto :fail
%COMPOSE% run --rm backup restore %~2
if errorlevel 1 goto :fail
%COMPOSE% start app
if errorlevel 1 goto :fail
exit /b 0

:stop
%COMPOSE% down --remove-orphans
exit /b %errorlevel%

:reset
%COMPOSE% down -v --remove-orphans
if errorlevel 1 goto :fail
if exist "%DATA_ROOT%" rmdir /s /q "%DATA_ROOT%"
if exist "%DATA_ROOT%" goto :fail
echo Dados locais apagados.
exit /b 0

:fail
echo.
echo run.bat FALHOU na etapa acima.
exit /b 1

:ssh
rem Implanta no celular por SSH. Perfil compartilhado com o Environment e os outros apps.
set "S_ARCH=arm64"
set "TUNNEL_HOST=wx.jpgmaraujo.com.br"
call :ssh_perfil
if errorlevel 1 exit /b 2
set "TUNNEL_LOG_HINT=ssh -p %S_PORT% %TARGET% sh wrexercise/deploy/remote-termux.sh status"

echo.
echo ==^> Testando o SSH em %TARGET%:%S_PORT%
"%SSH_EXE%" %SSH_OPTS% -o BatchMode=yes %TARGET% "uname -m" >"%TEMP%\wrexercise-ssh-arch.txt"
if errorlevel 1 (
  echo.
  echo Se o erro foi "Connection refused": no celular, abra o Termux e rode sshd ^(ele não sobe no boot^). Confira também o IP e a porta ^(Termux: 8022^).
  echo Sem acesso por chave. O envio do app usa o stdin do ssh e não aceita senha. Instale a chave:
  echo   type %%USERPROFILE%%\.ssh\id_ed25519.pub ^| ssh -p %S_PORT% %TARGET% "cat >> ~/.ssh/authorized_keys"
  goto :fail
)
set "R_ARCH="
set /p R_ARCH=<"%TEMP%\wrexercise-ssh-arch.txt"
if "%R_ARCH%"=="aarch64" set "R_ARCH=arm64"
if "%R_ARCH%"=="x86_64" set "R_ARCH=amd64"
if /i not "%R_ARCH%"=="%S_ARCH%" (
  echo A máquina remota é %R_ARCH%, não %S_ARCH%. Apague %SSH_FILE% ou corrija a resposta.
  goto :fail
)

"%SSH_EXE%" %SSH_OPTS% -o BatchMode=yes %TARGET% "test -f ambiente/deploy/ambiente.sh"
if errorlevel 1 (
  echo O Environment ^(PostgreSQL e túnel^) não está no celular. No repositório Environment: run.bat implantar
  goto :fail
)
if defined SO_SCRIPTS (
  set "SSH_IMAGE=app atual"
  goto :ssh_enviar_scripts
)
set "SSH_IMAGE="
set "DIRTY="
for /f %%i in ('git status --porcelain') do set "DIRTY=-dirty"
for /f %%i in ('git rev-parse --short HEAD') do set "SSH_IMAGE=wrexercise:%%i%DIRTY%-%S_ARCH%"
if not defined SSH_IMAGE (
  echo Não foi possível obter o commit atual.
  goto :fail
)
if defined DIRTY echo AVISO: há alterações sem commit; a imagem será marcada como %SSH_IMAGE%.

echo.
echo ==^> Construindo %SSH_IMAGE% para linux/%S_ARCH%
docker buildx build %DOCKER_BUILD_EXTRAS% --platform linux/%S_ARCH% -f deploy/Dockerfile -t %SSH_IMAGE% --load .
if errorlevel 1 goto :fail

rem Sem Docker no alvo. O binário Native AOT sai da imagem construída acima e vai por tar em pipe.
set "PUBLISH_DIR=%TEMP%\wrexercise-publish"
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
for /f %%i in ('docker create --platform linux/%S_ARCH% %SSH_IMAGE%') do set "CTR=%%i"
docker cp "%CTR%:/app/." "%PUBLISH_DIR%"
docker rm "%CTR%" >nul
if not exist "%PUBLISH_DIR%\Wrexercise" (
  echo Não foi possível extrair o app da imagem %SSH_IMAGE%.
  goto :fail
)
echo.
echo ==^> Enviando o app para o Termux
"%SSH_EXE%" %SSH_OPTS% %TARGET% "mkdir -p wrexercise/deploy && rm -rf wrexercise/app.novo && mkdir wrexercise/app.novo && echo %SSH_IMAGE% > wrexercise/app.novo/versao.txt"
if errorlevel 1 goto :fail
tar --format ustar -cf - -C "%PUBLISH_DIR%" . | "%SSH_EXE%" %SSH_OPTS% %TARGET% "tar -xf - -C wrexercise/app.novo"
if errorlevel 1 goto :fail
rmdir /s /q "%PUBLISH_DIR%"

:ssh_enviar_scripts
echo.
echo ==^> Enviando os scripts de deploy
"%SSH_EXE%" %SSH_OPTS% %TARGET% "mkdir -p wrexercise/deploy"
if errorlevel 1 goto :fail
tar --format ustar -cf - deploy/nuvem.sh deploy/remote-termux.sh | "%SSH_EXE%" %SSH_OPTS% %TARGET% "tar -xf - -C wrexercise"
if errorlevel 1 goto :fail

echo.
echo ==^> Aplicando migrações e subindo o app no Termux
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh implantar"
if errorlevel 1 goto :fail

echo.
call :check_tunnel
echo.
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh status"
echo.
echo Implantação em %TARGET% concluída ^(%SSH_IMAGE%, Termux^). Perfil em %SSH_FILE%
exit /b 0

:ssh_backup
rem Usa o perfil compartilhado. O backup roda no celular; depois a pasta de backups vem por tar em pipe.
rem Sem perguntas, para rodar pelo Agendador de Tarefas. Leva os arquivos diários (banco, avatares e logs).
set "SSH_EXE=%SystemRoot%\System32\OpenSSH\ssh.exe"
set "SSH_FILE=%LOCALAPPDATA%\jpgma\a14.env"
if not exist "%SSH_FILE%" (
  echo Perfil %SSH_FILE% não existe. Rode run.bat --ssh antes.
  exit /b 2
)
call :ssh_load
set "TARGET=%S_USER%@%S_HOST%"
set "SSH_OPTS=-p %S_PORT% -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o BatchMode=yes"
if defined S_KEY set "SSH_OPTS=%SSH_OPTS% -i "%S_KEY%" -o IdentitiesOnly=yes"
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
echo.
echo ==^> Gravando o backup no celular e enviando ao Google Drive
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh backup"
if errorlevel 1 goto :fail
echo.
echo ==^> Copiando para %BACKUP_DIR%
"%SSH_EXE%" %SSH_OPTS% %TARGET% "cd wrexercise/dados/backups && tar -cf - wrexercise-*.tar.gz" | tar -xf - -C "%BACKUP_DIR%"
if errorlevel 1 goto :fail
echo Backups do celular copiados para %BACKUP_DIR%
exit /b 0

:ssh_load
for /f "usebackq tokens=1* delims==" %%a in ("%SSH_FILE%") do set "%%a=%%b"
exit /b 0

:ssh_perfil
rem Lê o perfil SSH compartilhado (pergunta e salva o que faltar) e define SSH_EXE, TARGET e SSH_OPTS.
set "SSH_EXE=%SystemRoot%\System32\OpenSSH\ssh.exe"
set "SSH_DIR=%LOCALAPPDATA%\jpgma"
if not exist "%SSH_DIR%" mkdir "%SSH_DIR%"
set "SSH_FILE=%SSH_DIR%\a14.env"
rem 127.0.0.1 com "adb forward tcp:8022 tcp:8022" (decisão 0029); com IP do Wi-Fi, digite-o.
set "S_HOST=127.0.0.1"
set "S_PORT=8022"
set "S_USER="
set "S_KEY="
if exist "%SSH_FILE%" call :ssh_load
if defined S_USER goto :ssh_perfil_alvo
echo.
set /p "S_HOST=Host ou IP do celular [%S_HOST%]: "
set /p "S_PORT=Porta SSH do Termux [%S_PORT%]: "
set /p "S_USER=Usuário do Termux (resultado de whoami): "
set /p "S_KEY=Arquivo da chave SSH (vazio = ssh-agent ou padrão) [%S_KEY%]: "
if not defined S_USER (
  echo Informe o usuário do Termux.
  exit /b 2
)
>"%SSH_FILE%" echo S_HOST=%S_HOST%
>>"%SSH_FILE%" echo S_PORT=%S_PORT%
>>"%SSH_FILE%" echo S_USER=%S_USER%
>>"%SSH_FILE%" echo S_KEY=%S_KEY%
:ssh_perfil_alvo
set "TARGET=%S_USER%@%S_HOST%"
set "SSH_OPTS=-p %S_PORT% -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10"
if defined S_KEY set "SSH_OPTS=%SSH_OPTS% -i "%S_KEY%" -o IdentitiesOnly=yes"
exit /b 0

:configurar_backup
rem Configura o backup no Google Drive de ponta a ponta (decisão 0046), com perguntas:
rem rclone neste PC e no celular, login no Google, remoto cifrado, cópia da configuração para o
rem celular, tarefa de backup, backup de teste e, se quiser, a cópia extra agendada neste PC.
rem Pode rodar de novo: reaproveita a configuração que já existir aqui ou no celular.
set "RCLONE_DIR=%LOCALAPPDATA%\Wrexercise\rclone"
set "RCLONE_CONF=%RCLONE_DIR%\rclone.conf"
set "REMOTO=wx-cripto"
set "DRIVE=gdrive"
set "NOVA_CHAVE="
echo.
echo ==^> Configuração do backup no Google Drive ^(docs/decisoes/0046-backup-no-google-drive.md^)

echo.
echo ==^> 1/7 Celular
call :ssh_perfil
if errorlevel 1 exit /b 2
"%SSH_EXE%" %SSH_OPTS% -o BatchMode=yes %TARGET% "test -f wrexercise/deploy/remote-termux.sh"
if errorlevel 1 (
  echo Sem acesso ao celular em %TARGET%:%S_PORT%, ou o Wrexercise ainda não está nele.
  echo Confira o sshd no Termux e o adb forward tcp:8022 tcp:8022. Se nunca implantou: run.bat --ssh
  goto :fail
)
rem Os scripts novos vão antes: o celular pode ter uma versão sem o nuvem.sh.
tar --format ustar -cf - deploy/nuvem.sh deploy/remote-termux.sh | "%SSH_EXE%" %SSH_OPTS% %TARGET% "tar -xf - -C wrexercise"
if errorlevel 1 goto :fail
echo Scripts de deploy atualizados no celular.
"%SSH_EXE%" %SSH_OPTS% %TARGET% "command -v rclone >/dev/null"
if not errorlevel 1 goto :cb_rclone_cel_ok
echo O rclone não está no celular. Instalando com pkg...
"%SSH_EXE%" %SSH_OPTS% %TARGET% "pkg install -y rclone"
if errorlevel 1 goto :fail
:cb_rclone_cel_ok
"%SSH_EXE%" %SSH_OPTS% %TARGET% "rclone version | head -n 1"

echo.
echo ==^> 2/7 rclone neste PC
set "RCLONE="
for /f "delims=" %%r in ('where rclone 2^>nul') do if not defined RCLONE set "RCLONE=%%r"
if not defined RCLONE if exist "%LOCALAPPDATA%\Microsoft\WinGet\Links\rclone.exe" set "RCLONE=%LOCALAPPDATA%\Microsoft\WinGet\Links\rclone.exe"
if defined RCLONE goto :cb_rclone_pc_ok
set "R="
set /p "R=O rclone não está neste PC. Instalar com o winget agora? [S/n]: "
if /i "%R%"=="n" goto :fail
winget install -e --id Rclone.Rclone --accept-source-agreements --accept-package-agreements
if exist "%LOCALAPPDATA%\Microsoft\WinGet\Links\rclone.exe" set "RCLONE=%LOCALAPPDATA%\Microsoft\WinGet\Links\rclone.exe"
if defined RCLONE goto :cb_rclone_pc_ok
echo O winget terminou, mas o rclone.exe não foi encontrado. Abra um terminal novo e rode de novo: run.bat configurar-backup
goto :fail
:cb_rclone_pc_ok
"%RCLONE%" version | findstr /b /c:"rclone v"
echo Executável: %RCLONE%
if not exist "%RCLONE_DIR%" mkdir "%RCLONE_DIR%"
set "RC="%RCLONE%" --config "%RCLONE_CONF%""

echo.
echo ==^> 3/7 Configuração do rclone ^(%RCLONE_CONF%^)
%RC% listremotes 2>nul | findstr /x /c:"%REMOTO%:" >nul
if errorlevel 1 goto :cb_sem_config_pc
set "R="
set /p "R=Já existe uma configuração neste PC. Reaproveitar? [S/n]: "
if /i not "%R%"=="n" goto :cb_testar_pc
goto :cb_criar
:cb_sem_config_pc
rem Se o celular já tem os remotos (configurados à mão antes), traz de lá: a chave é a dos backups existentes.
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/nuvem.sh configurado"
if errorlevel 1 goto :cb_criar
set "R="
set /p "R=O celular já tem o remoto %REMOTO%. Copiar a configuração dele para este PC? [S/n]: "
if /i "%R%"=="n" goto :cb_criar
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/nuvem.sh exportar-config" >"%RCLONE_CONF%"
if errorlevel 1 goto :fail
goto :cb_testar_pc

:cb_criar
echo.
echo Login no Google:
echo   - Use de preferência uma conta Google só para backups ^(15 GB gratuitos, longe do Gmail e do Fotos^).
echo   - O navegador vai abrir. Entre com essa conta e autorize o rclone.
echo   - O acesso fica limitado aos arquivos que o próprio rclone criar ^(escopo drive.file^).
echo   - Se o rclone perguntar sobre Shared Drive ^(Team Drive^), responda n.
pause
if exist "%RCLONE_CONF%" del "%RCLONE_CONF%"
%RC% config create %DRIVE% drive scope=drive.file
if errorlevel 1 goto :fail
%RC% listremotes | findstr /x /c:"%DRIVE%:" >nul
if errorlevel 1 (
  echo O login no Google não foi concluído.
  goto :fail
)
rem Uma chave nova não lê backups cifrados com outra: não mistura os dois na mesma pasta.
set "EXISTENTE="
rem Aspas externas: o for /f roda o comando por cmd /c, que tira o primeiro e o último caractere de aspas.
for /f "delims=" %%f in ('"%RC% lsf %DRIVE%:wrexercise 2>nul"') do set "EXISTENTE=1"
if not defined EXISTENTE goto :cb_chave
echo.
echo ATENÇÃO: esta conta já tem backups em wrexercise/, cifrados com outra chave. Uma chave nova não os lê.
echo Recupere o rclone.conf antigo ^(gerenciador de senhas^), copie-o para %RCLONE_CONF% e rode de novo.
set "R="
set /p "R=Para apagar esses backups antigos e começar do zero, digite APAGAR: "
if not "%R%"=="APAGAR" goto :fail
%RC% purge %DRIVE%:wrexercise --drive-use-trash=false
if errorlevel 1 goto :fail
:cb_chave
rem Duas senhas aleatórias de 256 bits para o rclone crypt (conteúdo e nomes cifrados).
set "P1="
set "P2="
for /f %%p in ('powershell -NoProfile -Command "$b=New-Object byte[] 32; (New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b); [BitConverter]::ToString($b).Replace('-','')"') do set "P1=%%p"
for /f %%p in ('powershell -NoProfile -Command "$b=New-Object byte[] 32; (New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b); [BitConverter]::ToString($b).Replace('-','')"') do set "P2=%%p"
if not defined P2 (
  echo Não foi possível gerar as senhas do crypt.
  goto :fail
)
%RC% config create %REMOTO% crypt remote=%DRIVE%:wrexercise filename_encryption=standard directory_name_encryption=true password=%P1% password2=%P2% --obscure >nul
if errorlevel 1 goto :fail
set "NOVA_CHAVE=1"

:cb_testar_pc
echo.
echo ==^> 4/7 Testando o Drive a partir deste PC
%RC% mkdir %REMOTO%:diarios
if not errorlevel 1 goto :cb_drive_ok
echo O rclone deste PC não acessa o Drive ^(token expirado ou revogado, ou sem rede^).
set "R="
set /p "R=Refazer só o login no Google, mantendo a chave do crypt? [S/n]: "
if /i "%R%"=="n" goto :fail
%RC% config reconnect %DRIVE%:
if errorlevel 1 goto :fail
%RC% mkdir %REMOTO%:diarios
if errorlevel 1 goto :fail
:cb_drive_ok
%RC% about %DRIVE%:

echo.
echo ==^> 5/7 Enviando a configuração para o celular
type "%RCLONE_CONF%" | "%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/nuvem.sh importar-config"
if errorlevel 1 goto :fail

echo.
echo ==^> 6/7 Tarefa de backup e teste
rem iniciar sobe só o que não estiver rodando: o app no ar continua; a tarefa de backup passa a existir.
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh iniciar"
if errorlevel 1 goto :fail
set "R="
set /p "R=Fazer um backup de teste agora (grava e envia ao Drive)? [S/n]: "
if /i "%R%"=="n" goto :cb_agendar
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh backup"
if errorlevel 1 goto :fail
echo.
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/nuvem.sh status"
echo.
echo Arquivos diários no Drive, vistos deste PC:
%RC% lsl %REMOTO%:diarios

:cb_agendar
echo.
echo ==^> 7/7 Cópia extra neste PC ^(opcional^)
echo O celular já envia tudo ao Drive. Este PC pode também copiar os arquivos diários ^(sem as evidências^)
echo todo dia às 03:30, pelo Agendador de Tarefas. Exige o celular acessível por SSH nesse horário.
set "R="
set /p "R=Agendar a cópia extra? [s/N]: "
if /i not "%R%"=="s" goto :cb_resumo
set "PASTA=%BACKUP_DIR:/=\%"
set /p "PASTA=Pasta da cópia (de preferência sincronizada, como o OneDrive) [%PASTA%]: "
if /i not "%PASTA%"=="%BACKUP_DIR:/=\%" setx BACKUP_DIR "%PASTA%" >nul
schtasks /create /f /sc daily /st 03:30 /tn WrexerciseBackup /tr "\"%~f0\" ssh-backup"
if errorlevel 1 goto :fail
echo Tarefa WrexerciseBackup agendada; cópias em %PASTA%.

:cb_resumo
echo.
echo ==^> Backup configurado
echo - O celular grava e envia o backup todo dia e em cada run.bat --ssh. Situação: stats.bat
echo - Configuração do rclone neste PC: %RCLONE_CONF%
echo - GUARDE ESSE ARQUIVO NO GERENCIADOR DE SENHAS. Sem as senhas do crypt, ninguém lê os backups.
if not defined NOVA_CHAVE goto :cb_fim
set "R="
set /p "R=Mostrar as senhas do crypt agora, para anotar no gerenciador? [s/N]: "
if /i not "%R%"=="s" goto :cb_fim
echo   remoto %REMOTO%, remote = %DRIVE%:wrexercise, filename_encryption = standard
echo   password  = %P1%
echo   password2 = %P2%
:cb_fim
echo - Para ver os backups deste PC: "%RCLONE%" --config "%RCLONE_CONF%" lsl %REMOTO%:diarios
exit /b 0
