@echo off
rem Semeia o banco local do run.bat com o grupo de testes: 4 contas com histórico, Energia, conquistas,
rem Enfeites e avatares tingidos. É o seed para testar todo o aplicativo. Ver scripts/seed/README.md.
rem Requer a pilha do run.bat no ar (run.bat), com as migrações aplicadas. Só para testes locais.
rem   seed.bat           apaga o seed anterior, se houver, e cria tudo de novo com datas relativas a hoje
rem   seed.bat --reset   apaga as Jogadas do grupo do seed; as Cartas voltam para a Mão
chcp 65001 >nul
setlocal
cd /d "%~dp0"

rem Os mesmos nomes do run.bat: projeto wrexercise-local e dados em DATA_ROOT.
set "DB_CONTAINER=wrexercise-local-db-1"
set "AVATARES_DIR=%LOCALAPPDATA%\Wrexercise\local\avatares"
set "PSQL=docker exec -i %DB_CONTAINER% psql -U wrexercise -d wrexercise -v ON_ERROR_STOP=1 --single-transaction -q"

set "RESET="
if /i "%~1"=="--reset" set "RESET=1"
if not "%~1"=="" if not defined RESET (
  echo Uso: seed.bat [--reset]
  exit /b 2
)

docker exec %DB_CONTAINER% pg_isready -q -U wrexercise -d wrexercise >nul 2>&1
if errorlevel 1 (
  echo O banco local não está no ar. Rode run.bat antes.
  exit /b 1
)

if defined RESET (
  echo ==^> Apagando as Jogadas do grupo do seed
  %PSQL% < scripts\seed\reset-jogadas.sql
  if errorlevel 1 goto :fail
  echo Jogadas apagadas. As Cartas usadas voltaram para a Mão.
  exit /b 0
)

echo ==^> Copiando os avatares tingidos para %AVATARES_DIR%
if not exist "%AVATARES_DIR%" mkdir "%AVATARES_DIR%"
copy /y scripts\seed\avatares\*.png "%AVATARES_DIR%\" >nul
if errorlevel 1 goto :fail

echo ==^> Criando o grupo do seed
%PSQL% < scripts\seed\seed.sql
if errorlevel 1 goto :fail

echo.
echo Grupo "Seed de testes" pronto em http://localhost. Senha de todas as contas: wrexercise
echo   ana@seed.test     Proprietario
echo   bruno@seed.test   Administrador
echo   carla@seed.test   Membro
echo   diego@seed.test   Membro
echo Para apagar as Jogadas do grupo: seed.bat --reset
exit /b 0

:fail
echo.
echo seed.bat FALHOU na etapa acima.
exit /b 1
