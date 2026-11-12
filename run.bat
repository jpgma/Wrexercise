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
rem                          e o sshd ligado no Termux. Pode ser combinado com --rebuild. Ver
rem                          docs/decisoes/0029-implantacao-nativa-no-termux.md e 0044.
rem   run.bat --ssh --scripts  envia só deploy/*.sh e reinicia o app que já está no celular, sem build
rem   run.bat logs         acompanha os logs da aplicação
rem   run.bat backup       grava um backup do banco e das evidências em BACKUP_DIR
rem   run.bat ssh-backup   roda o backup das evidências e avatares no celular e copia a pasta de backups
rem                          dele para BACKUP_DIR. O banco é copiado pelo run.bat ssh-backup do Environment.
rem                          Aponte BACKUP_DIR para uma pasta sincronizada. Não apaga cópias antigas daqui.
rem                          Ver docs/decisoes/0032-a14-como-producao.md e 0044.
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
if /i "%~1"=="restore" goto :restore
if /i "%~1"=="stop" goto :stop
if /i "%~1"=="reset" goto :reset
echo Uso: run.bat [--rebuild^|rebuild] [--tunnel^|--tunnel-maquina^|--ssh [--scripts]] ^| logs ^| backup ^| ssh-backup ^| restore AAAAMMDD-HHMMSS ^| stop ^| reset
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
set "SSH_EXE=%SystemRoot%\System32\OpenSSH\ssh.exe"
set "SSH_DIR=%LOCALAPPDATA%\jpgma"
if not exist "%SSH_DIR%" mkdir "%SSH_DIR%"
set "SSH_FILE=%SSH_DIR%\a14.env"
rem 127.0.0.1 com "adb forward tcp:8022 tcp:8022" (decisão 0029); com IP do Wi-Fi, digite-o.
set "S_HOST=127.0.0.1"
set "S_PORT=8022"
set "S_USER="
set "S_KEY="
set "S_ARCH=arm64"
set "TUNNEL_HOST=wx.jpgmaraujo.com.br"
if exist "%SSH_FILE%" call :ssh_load
if defined S_USER goto :ssh_alvo

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

:ssh_alvo
set "TARGET=%S_USER%@%S_HOST%"
set "SSH_OPTS=-p %S_PORT% -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10"
if defined S_KEY set "SSH_OPTS=%SSH_OPTS% -i "%S_KEY%" -o IdentitiesOnly=yes"
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
"%SSH_EXE%" %SSH_OPTS% %TARGET% "mkdir -p wrexercise/deploy && rm -rf wrexercise/app.novo && mkdir wrexercise/app.novo"
if errorlevel 1 goto :fail
tar --format ustar -cf - -C "%PUBLISH_DIR%" . | "%SSH_EXE%" %SSH_OPTS% %TARGET% "tar -xf - -C wrexercise/app.novo"
if errorlevel 1 goto :fail
rmdir /s /q "%PUBLISH_DIR%"

:ssh_enviar_scripts
echo.
echo ==^> Enviando os scripts de deploy
"%SSH_EXE%" %SSH_OPTS% %TARGET% "mkdir -p wrexercise/deploy"
if errorlevel 1 goto :fail
tar --format ustar -cf - deploy/backup.sh deploy/remote-termux.sh | "%SSH_EXE%" %SSH_OPTS% %TARGET% "tar -xf - -C wrexercise"
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
rem Sem perguntas, para rodar pelo Agendador de Tarefas. O banco vem pelo ssh-backup do Environment.
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
echo ==^> Gravando o backup das evidências e avatares no celular
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh backup"
if errorlevel 1 goto :fail
echo.
echo ==^> Copiando para %BACKUP_DIR%
"%SSH_EXE%" %SSH_OPTS% %TARGET% "tar -cf - -C wrexercise/dados/backups ." | tar -xf - -C "%BACKUP_DIR%"
if errorlevel 1 goto :fail
echo Backups do celular copiados para %BACKUP_DIR%
exit /b 0

:ssh_load
for /f "usebackq tokens=1* delims==" %%a in ("%SSH_FILE%") do set "%%a=%%b"
exit /b 0
