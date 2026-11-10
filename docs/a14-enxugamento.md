# Enxugamento do Galaxy A14 (sem root)

Plano para deixar o telefone de produção o mais perto possível de um servidor sem interface gráfica, sem root e sem apagar o aparelho. O Termux, a pilha nativa (`docs/decisoes/0029-implantacao-nativa-no-termux.md`) e o fluxo de `run.bat --ssh` não mudam.

**Situação:** executado em 2026-11-10 (ver "Resultado" no fim). Serve de roteiro para refazer depois de uma restauração de fábrica.

## Objetivo e limites

- Remover apps e serviços que o servidor não usa, para liberar RAM e CPU de fundo para PostgreSQL, app e túnel.
- Evitar reinícios e suspensões que derrubam a pilha.
- O framework do Android (`system_server`, SystemUI, launcher) continua rodando, com cerca de 1 a 1,5 GB. Ir além disso exige root (opções 2 e 3, fora deste plano).
- Tudo aqui é reversível sem apagar o aparelho: `pm uninstall --user 0` só esconde o app do usuário 0, e o APK continua na partição do sistema.

## Antes de começar

Os arquivos de inventário ficam fora do repositório, em `%LOCALAPPDATA%\jpgma\a14-enxugamento\` (abaixo, `$d`). O `adb` não precisa estar no `PATH`: o do Visual Studio fica em `...\Microsoft Visual Studio\Shared\Android\android-sdk\platform-tools\adb.exe`.

```powershell
$d = "$env:LOCALAPPDATA\jpgma\a14-enxugamento"; mkdir $d -Force
```

1. Faça um backup e copie-o para o Windows: `.\run.bat ssh-backup`.
2. Guarde a linha de base: `.\stats.bat > $d\a14-antes.txt`.
3. No telefone: Configurações → Sobre o telefone → Informações do software → toque 7 vezes em "Número de compilação". Em Opções do desenvolvedor, ligue "Depuração USB" e "Desativar tempo limite de autorização do adb" (sem isso, a autorização do `adb forward` expira em 7 dias).
4. Conecte o USB, aceite a chave no telefone e confira:

```powershell
adb devices
adb shell getprop ro.product.model            # ex.: SM-A146M
adb shell getprop ro.build.version.release    # versão do Android
```

5. Grave o inventário, que também serve para desfazer:

```powershell
adb shell pm list packages -e > $d\pacotes-antes.txt
adb shell dumpsys meminfo > $d\meminfo-antes.txt
```

`meminfo-antes.txt` lista os processos por PSS. Use-o para ver o que de fato pesa antes de remover. Sem o USB, `ssh` no Termux com `pm list packages -e` também lista os pacotes.

## 1. Remover apps

Os nomes dos pacotes variam com o firmware. Confira cada um em `$d\pacotes-antes.txt`; um pacote ausente só gera `Failure [not installed for 0]`, sem efeito.

Função de apoio, para colar no PowerShell:

```powershell
function Remove-A14 { param([string[]]$Pacotes)
  foreach ($p in $Pacotes) { Write-Host "$p -> $(adb shell pm uninstall -k --user 0 $p)" } }
```

### Lote 1: Samsung, sem risco

```powershell
Remove-A14 @(
  'com.samsung.android.bixby.agent', 'com.samsung.android.bixby.wakeup', 'com.samsung.android.visionintelligence',
  'com.samsung.android.bixbyvision.framework', 'com.samsung.android.app.spage', 'com.samsung.android.app.routines',
  'com.samsung.android.game.gamehome', 'com.samsung.android.game.gametools', 'com.samsung.android.game.gos',
  'com.sec.android.app.samsungapps', 'com.sec.android.app.sbrowser', 'com.sec.android.app.shealth',
  'com.samsung.android.spay', 'com.samsung.android.samsungpass', 'com.samsung.android.arzone',
  'com.samsung.android.aremoji', 'com.samsung.android.oneconnect', 'com.samsung.android.kidsinstaller',
  'com.samsung.android.app.tips', 'com.samsung.android.app.appsedge', 'com.samsung.android.app.taskedge',
  'com.samsung.android.app.aodservice', 'com.samsung.android.themestore', 'com.samsung.android.app.dressroom',
  'com.samsung.android.dynamiclock', 'com.sec.android.daemonapp', 'com.samsung.android.messaging',
  'com.samsung.android.email.provider', 'com.sec.android.gallery3d', 'com.sec.android.app.camera',
  'com.samsung.android.calendar', 'com.sec.android.app.voicenote', 'com.samsung.android.app.notes',
  'com.samsung.android.scloud', 'com.samsung.android.mobileservice', 'com.samsung.android.smartswitchassistant',
  'com.sec.android.easyMover', 'com.samsung.android.app.sharelive', 'com.samsung.android.mdx',
  'com.samsung.android.app.watchmanagerstub', 'com.samsung.android.service.peoplestripe'
)
```

### Lote 2: pré-instalados de terceiros e Google (exceto o Google Play Services)

```powershell
Remove-A14 @(
  'com.facebook.katana', 'com.facebook.system', 'com.facebook.appmanager', 'com.facebook.services',
  'com.netflix.mediaclient', 'com.spotify.music', 'com.microsoft.skydrive', 'com.microsoft.office.officehubrow',
  'com.google.android.youtube', 'com.google.android.apps.youtube.music', 'com.google.android.apps.maps',
  'com.google.android.gm', 'com.google.android.apps.photos', 'com.google.android.apps.docs',
  'com.google.android.apps.tachyon', 'com.google.android.apps.googleassistant',
  'com.google.android.googlequicksearchbox', 'com.android.chrome', 'com.google.android.apps.wellbeing',
  'com.google.android.projection.gearhead', 'com.google.android.videos', 'com.google.android.feedback',
  'com.google.android.apps.messaging', 'com.google.ar.core'
)
```

### Lote 2b: extras do firmware brasileiro (A146M)

Propaganda e instaladores (o AppCloud da AppLovin instala apps sozinho), extras do Google e serviços da Samsung que o servidor não usa:

```powershell
Remove-A14 @(
  'com.applovin.array.apphub.samsung', 'com.opera.max.oem', 'com.hiya.star', 'com.samsung.shop',
  'com.sidia.suframa.notification',
  'com.google.android.apps.bard', 'com.google.android.keep', 'com.google.android.apps.turbo',
  'com.google.android.as', 'com.google.android.as.oss', 'com.google.android.apps.restore',
  'com.google.android.gms.location.history', 'com.google.android.tts',
  'com.samsung.android.rubin.app', 'com.samsung.android.forest', 'com.samsung.android.da.daagent',
  'com.samsung.android.app.parentalcare', 'com.samsung.android.video', 'com.sec.android.app.fm',
  'com.samsung.android.visual.cloudcore', 'com.samsung.storyservice', 'com.samsung.cmh',
  'com.samsung.android.app.reminder', 'com.samsung.android.app.clipboardedge',
  'com.samsung.android.stickercenter', 'com.samsung.android.mapsagent', 'com.samsung.android.smartface'
)
```

Reinicie (`adb reboot`), desbloqueie, espere a pilha subir (cerca de 5 minutos, ver 0029) e confira com `.\stats.bat` antes de seguir.

### Lote 3 (opcional): Google Play Services e Play Store

O GMS costuma usar 200 a 400 MB e acorda a CPU com frequência. O Termux vem da F-Droid e o app não usa notificação push do Google (0020), então nada da pilha depende dele. Ainda assim, alguns componentes da Samsung esperam o GMS: remova por último, sozinho, e reinicie logo depois.

```powershell
Remove-A14 @('com.google.android.gms', 'com.android.vending', 'com.google.android.gsf')
adb reboot
```

Se algo quebrar (Wi-Fi, laço de travamentos, Termux:Boot parado), restaure os três (ver "Desfazer").

### Não remova

| Pacote | Por quê |
|---|---|
| `com.termux`, `com.termux.boot`, `com.termux.api` | a pilha, o boot e `stats` |
| `com.android.systemui`, `com.sec.android.app.launcher` | sem eles o telefone fica sem tela de desbloqueio ou sem como abrir o Termux |
| `com.samsung.android.honeyboard` | teclado, para digitar o PIN e usar o Termux |
| `com.android.settings`, `com.samsung.android.lool` | Configurações e Manutenção do aparelho (proteção da bateria) |
| `com.android.phone`, `com.android.server.telecom`, `com.android.providers.*` | serviços base; removê-los pode travar o boot |
| `com.android.wifi.*`, `com.samsung.android.wifi.*`, `com.android.networkstack*`, `com.android.cellbroadcast*` | rede |
| `com.android.shell`, `com.android.packageinstaller`, `com.google.android.packageinstaller` | `adb shell` e reinstalação |
| `com.samsung.android.knox.*`, `com.samsung.klmsagent`, `com.sec.enterprise.*` | Knox; removê-los pode travar o boot |
| `com.google.android.webview` | outros componentes do sistema o usam |

## 2. Ajustes por ADB

```powershell
# Sem animações e com a tela no mínimo
adb shell settings put global window_animation_scale 0
adb shell settings put global transition_animation_scale 0
adb shell settings put global animator_duration_scale 0
adb shell settings put system screen_brightness_mode 0
adb shell settings put system screen_brightness 1
adb shell settings put system screen_off_timeout 15000

# Sem localização nem varredura de rede em segundo plano
# (a One UI volta location_mode para 3 no boot; use o comando do serviço de localização)
adb shell cmd location set-location-enabled false
adb shell settings put global wifi_scan_always_enabled 0
adb shell settings put global ble_scan_always_enabled 0

# Rádio: modo avião desliga o modem; o Wi-Fi volta em seguida
adb shell cmd connectivity airplane-mode enable
adb shell svc wifi enable
adb shell svc bluetooth disable
adb shell svc nfc disable    # o A14 não tem NFC: "Got a null NfcAdapter" é esperado

# A bateria adaptativa não deve restringir o Termux
adb shell settings put global adaptive_battery_management_enabled 0
adb shell dumpsys deviceidle whitelist +com.termux
adb shell dumpsys deviceidle whitelist +com.termux.boot
adb shell dumpsys deviceidle whitelist +com.termux.api

# Pré-requisitos da 0029 (refazê-los não faz mal; em 2026-11-10 o appops estava negado)
adb shell settings put global settings_enable_monitor_phantom_procs false
adb shell device_config put activity_manager max_phantom_processes 2147483647
adb shell cmd appops set com.termux SYSTEM_ALERT_WINDOW allow
adb shell cmd appops set com.termux.boot SYSTEM_ALERT_WINDOW allow
```

Os ajustes acima sobrevivem ao reinício. Confira com `adb shell settings get global airplane_mode_on` (1) e `adb shell cmd location is-location-enabled` (false).

## 3. Ajustes pela tela (One UI)

Os nomes mudam um pouco entre versões da One UI.

- **Reinício automático:** Manutenção do aparelho → Automação → desligue "Reinício automático em horários definidos" e "Otimização automática diária". Um reinício às 3h derruba a pilha por cerca de 5 minutos e, com PIN, até alguém desbloquear.
- **Proteção da bateria:** Bateria → Mais configurações da bateria → Proteger bateria → "Máxima" (para em 80%) ou "Básica" (85%). Um telefone ligado na tomada o tempo todo a 100% acaba com a bateria inchada.
- **Atualização de software:** desligue "Baixar automaticamente por Wi-Fi". Instale as atualizações de segurança à mão, num horário planejado, e confira a pilha depois.
- **RAM Plus:** Manutenção do aparelho → Memória → RAM Plus → desligado. Ele grava páginas de memória no armazenamento e gasta a memória flash; o zram do kernel continua ativo. Exige reinício. Se o `stats` mostrar processos mortos por falta de memória depois disso, volte para o mínimo.
- **Apps que nunca dormem:** confira se Termux, Termux:Boot e Termux:API continuam na lista e com bateria "Sem restrições".
- **Contas:** em Contas e backup, remova a conta Samsung e a do Google (se não removeu o GMS, isso ainda corta a sincronização).

## 4. Sem PIN

Com um bloqueio de tela, o armazenamento do Termux só abre no primeiro desbloqueio (0029). Sem bloqueio, o Android abre o armazenamento sozinho no boot, e a pilha volta depois de queda de energia ou reinício sem ninguém por perto.

**Decisão (2026-11-10): sem PIN.** Configurações → Tela de bloqueio e AOD → Tipo de bloqueio de tela → Nenhum. Custo aceito: quem pegar o telefone tem acesso ao banco (hashes de senha, dados dos membros) e às evidências. O telefone fica em casa, num lugar fixo. Registrado na 0029 ("Pré-requisitos no telefone").

## 5. Verificar

1. `adb reboot` e espere a pilha, sem tocar no telefone. O Termux:Boot roda cerca de 4 a 5 minutos depois do boot.
2. `https://wx.jpgmaraujo.com.br/ready` responde 200.
3. `.\stats.bat > $d\a14-depois.txt` e compare com `$d\a14-antes.txt`.
4. `adb shell dumpsys meminfo > $d\meminfo-depois.txt` e compare os totais ("Total RAM", "Free RAM", "Used RAM").
5. Deixe rodar 24 horas e confira de novo com `.\stats.bat`: a pilha não reiniciou e a bateria fica abaixo do limite.

Depois de cada reinício, o `adb` pode mostrar o aparelho como `offline` ou `unauthorized`. Rode `adb kill-server` e, se pedir, aceite a chave de novo no telefone. Isso não afeta a pilha, que usa SSH pelo Wi-Fi.

## Desfazer

```powershell
adb shell cmd package install-existing <pacote>
```

Para restaurar tudo o que saiu, compare `$d\pacotes-antes.txt` com a lista atual e reinstale cada pacote que falta. Para voltar o PIN, defina-o na tela e desfaça a nota da 0029. Se o telefone não iniciar, entre no modo de recuperação (Volume + e Liga com o cabo USB) e use "Wipe data/factory reset". Depois, refaça os pré-requisitos da 0029, rode `.\run.bat --ssh` e restaure o último backup (`remote-termux.sh restore`).

## Resultado

Executado em 2026-11-10 no SM-A146M (Galaxy A14 5G), Android 15, firmware `A146MUBSFDZI1`.

- **Lotes:** 1, 2, 2b e 3. Saíram 69 pacotes; dos listados, 27 já não existiam neste firmware. Nenhuma falha. Wi-Fi, SSH e Termux:Boot continuaram funcionando sem o GMS.
- **Ajustes:** todos os de §2 e §3. O `appops` "Exibir sobre outros apps" do Termux e do Termux:Boot estava negado e foi concedido. A One UI volta `location_mode` para 3: use `cmd location set-location-enabled false`. RAM Plus desligado (swap de 4096 MB para 3072 MB, só o zram). A conta Samsung continuou conectada.
- **Memória** (`stats`): antes, 1081 MB disponíveis e 1241 MB de swap em uso, com o telefone ligado havia 1 dia. Depois, 1315 MB disponíveis e 651 MB de swap, 11 minutos depois do boot. No `dumpsys meminfo`, logo depois dos lotes 1 e 2, o GMS e a Play Store somavam cerca de 500 MB de PSS.
- **Problema:** depois do reinício, o PID gravado em `run/*.pid` pode pertencer a outro processo do Termux, como o do Termux:Boot. Os PIDs do boot se repetem, e o `kill -0` aceitava esse PID: `iniciar` não subia a tarefa. Aconteceu com o app do Wrexercise e, no último reinício, com o túnel do Environment e com o Movr (`/ready` com 530 até subir à mão). `deploy/remote-termux.sh` agora confere a linha de comando do processo (`tarefa_rodando`). O Environment (`termux/ambiente.sh`) e o Movr (`deploy/remote-termux.sh`) receberam a mesma correção; implante-os com `run.bat implantar` e `deploy-a14.bat --scripts`.
- **Bateria:** sem leitura, porque o Termux:API não está instalado (`pkg install termux-api` e o app da F-Droid). A proteção de 80% ficou ligada pela tela.
