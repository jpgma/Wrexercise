@echo off
chcp 65001 >nul
rem Mostra os erros recentes do Wrexercise no celular (Termux) por SSH, lidos de ~/wrexercise/log/app.log.
rem   erros.bat                 os 10 erros mais recentes (Error e Critical), com o começo da exceção
rem   erros.bat 30              os 30 mais recentes (até 200)
rem   erros.bat 7F3K2Q          as linhas da referência que o app mostrou ("Código: 7F3K2Q"), com a exceção inteira
rem   erros.bat --perfil NOME   usa o perfil NOME em vez de a14 (combina com os outros)
rem O horário é UTC. O log guarda o último 1 MB a cada reinício do app.
rem Usa o perfil SSH compartilhado com o Environment (%LOCALAPPDATA%\jpgma\a14.env), o mesmo do stats.bat.
rem Se o perfil não existir ou o celular não responder, pergunta host, porta, usuário do Termux e chave.
rem O trabalho roda no celular: deploy/remote-termux.sh erros. Somente leitura.
setlocal
cd /d "%~dp0"

set "SSH_EXE=%SystemRoot%\System32\OpenSSH\ssh.exe"
set "SSH_DIR=%LOCALAPPDATA%\jpgma"
set "S_NOME=a14"
set "FILTRO="

:parse
if "%~1"=="" goto :start
if /i "%~1"=="--perfil" (
  set "S_NOME=%~2"
  shift
  shift
  goto :parse
)
set "FILTRO=%~1"
shift
goto :parse

:start
if not exist "%SSH_DIR%" mkdir "%SSH_DIR%"
set "SSH_FILE=%SSH_DIR%\%S_NOME%.env"

rem Mesmos nomes e padrões de run.bat --ssh e stats.bat, para os três lerem o mesmo perfil.
set "S_HOST=127.0.0.1"
set "S_PORT=8022"
set "S_USER="
set "S_KEY="
if exist "%SSH_FILE%" call :load

if not defined S_USER goto :ask
call :probe
if not errorlevel 1 goto :run
echo.
echo Não consegui falar com o celular em %S_USER%@%S_HOST%:%S_PORT%.
echo Se o erro foi "Connection refused", abra o Termux no celular e rode sshd, que não sobe no boot.
echo Com adb forward tcp:8022 tcp:8022, use 127.0.0.1 como host.

:ask
echo.
echo Informe onde achar o celular por SSH. Enter mantém o valor entre colchetes.
set /p "S_HOST=Host ou IP do celular [%S_HOST%]: "
set /p "S_PORT=Porta SSH do Termux [%S_PORT%]: "
set /p "S_USER=Usuário do Termux, resultado de whoami [%S_USER%]: "
set /p "S_KEY=Arquivo da chave SSH, vazio usa o ssh-agent ou a padrão [%S_KEY%]: "
if not defined S_USER (
  echo Informe o usuário do Termux.
  exit /b 2
)
call :probe
if errorlevel 1 (
  echo.
  echo Sem acesso por chave em %S_USER%@%S_HOST%:%S_PORT%. Confira os dados e o sshd. Para instalar a chave:
  echo   type %%USERPROFILE%%\.ssh\id_ed25519.pub ^| ssh -p %S_PORT% %S_USER%@%S_HOST% "cat >> ~/.ssh/authorized_keys"
  exit /b 1
)
>"%SSH_FILE%" echo S_HOST=%S_HOST%
>>"%SSH_FILE%" echo S_PORT=%S_PORT%
>>"%SSH_FILE%" echo S_USER=%S_USER%
>>"%SSH_FILE%" echo S_KEY=%S_KEY%
echo Perfil salvo em %SSH_FILE%

:run
"%SSH_EXE%" %SSH_OPTS% %TARGET% "test -f wrexercise/deploy/remote-termux.sh"
if errorlevel 1 (
  echo.
  echo O celular responde, mas o Wrexercise não está implantado nele. Rode: run.bat --ssh
  exit /b 1
)
rem Entre aspas simples, o filtro chega ao celular como texto; o script aceita só número ou letras e números.
"%SSH_EXE%" %SSH_OPTS% %TARGET% "sh wrexercise/deploy/remote-termux.sh erros '%FILTRO%'"
exit /b %errorlevel%

:probe
set "TARGET=%S_USER%@%S_HOST%"
set "SSH_OPTS=-p %S_PORT% -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o BatchMode=yes"
if defined S_KEY set "SSH_OPTS=%SSH_OPTS% -i "%S_KEY%" -o IdentitiesOnly=yes"
"%SSH_EXE%" %SSH_OPTS% %TARGET% "true" >nul 2>&1
exit /b %errorlevel%

:load
for /f "usebackq tokens=1* delims==" %%a in ("%SSH_FILE%") do set "%%a=%%b"
exit /b 0
