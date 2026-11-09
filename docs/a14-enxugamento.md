# Enxugamento do Galaxy A14 (sem root)

Plano para deixar o telefone de produção o mais perto possível de um servidor sem interface gráfica, sem root e sem apagar o aparelho. O Termux, a pilha nativa (`docs/decisoes/0029-implantacao-nativa-no-termux.md`) e o fluxo de `run.bat --ssh` não mudam.

**Situação:** planejado, ainda não executado. Ao executar, preencha "Resultado" no fim.

## Objetivo e limites

- Remover apps e serviços que o servidor não usa, para liberar RAM e CPU de fundo para PostgreSQL, app e túnel.
- Evitar reinícios e suspensões que derrubam a pilha.
- O framework do Android (`system_server`, SystemUI, launcher) continua rodando, com cerca de 1 a 1,5 GB. Ir além disso exige root (opções 2 e 3, fora deste plano).
- Tudo aqui é reversível sem apagar o aparelho: `pm uninstall --user 0` só esconde o app do usuário 0, e o APK continua na partição do sistema.

## Antes de começar

1. Faça um backup e copie-o para o Windows: `.\run.bat ssh-backup`.
2. Guarde a linha de base: `.\stats.bat > a14-antes.txt`.
3. No telefone: Configurações → Sobre o telefone → Informações do software → toque 7 vezes em "Número de compilação". Em Opções do desenvolvedor, ligue "Depuração USB" e "Desativar tempo limite de autorização do adb" (sem isso, a autorização do `adb forward` expira em 7 dias).
4. Conecte o USB, aceite a chave no telefone e confira:

```powershell
adb devices
adb shell getprop ro.product.model            # ex.: SM-A146M
adb shell getprop ro.build.version.release    # versão do Android
```

5. Grave o inventário, que também serve para desfazer:

```powershell
mkdir a14 -Force
adb shell pm list packages -e > a14\pacotes-antes.txt
adb shell dumpsys meminfo > a14\meminfo-antes.txt
```

`a14\meminfo-antes.txt` lista os processos por PSS. Use-o para ver o que de fato pesa antes de remover.

## 1. Remover apps

Os nomes dos pacotes variam com o firmware. Confira cada um em `a14\pacotes-antes.txt`; um pacote ausente só gera `Failure [not installed for 0]`, sem efeito.

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
adb shell settings put secure location_mode 0
adb shell settings put global wifi_scan_always_enabled 0
adb shell settings put global ble_scan_always_enabled 0

# Rádio: modo avião desliga o modem; o Wi-Fi volta em seguida
adb shell cmd connectivity airplane-mode enable
adb shell svc wifi enable
adb shell svc bluetooth disable
adb shell svc nfc disable

# A bateria adaptativa não deve restringir o Termux
adb shell settings put global adaptive_battery_management_enabled 0
adb shell dumpsys deviceidle whitelist +com.termux
adb shell dumpsys deviceidle whitelist +com.termux.boot
adb shell dumpsys deviceidle whitelist +com.termux.api
```

Confira também os ajustes de processos fantasmas e de "Exibir sobre outros apps" da 0029: refazê-los não faz mal.

## 3. Ajustes pela tela (One UI)

Os nomes mudam um pouco entre versões da One UI.

- **Reinício automático:** Manutenção do aparelho → Automação → desligue "Reinício automático em horários definidos" e "Otimização automática diária". Um reinício às 3h derruba a pilha por cerca de 5 minutos e, com PIN, até alguém desbloquear.
- **Proteção da bateria:** Bateria → Mais configurações da bateria → Proteger bateria → "Máxima" (para em 80%) ou "Básica" (85%). Um telefone ligado na tomada o tempo todo a 100% acaba com a bateria inchada.
- **Atualização de software:** desligue "Baixar automaticamente por Wi-Fi". Instale as atualizações de segurança à mão, num horário planejado, e confira a pilha depois.
- **RAM Plus:** Manutenção do aparelho → Memória → RAM Plus → desligado. Ele grava páginas de memória no armazenamento e gasta a memória flash; o zram do kernel continua ativo. Exige reinício. Se o `stats` mostrar processos mortos por falta de memória depois disso, volte para o mínimo.
- **Apps que nunca dormem:** confira se Termux, Termux:Boot e Termux:API continuam na lista e com bateria "Sem restrições".
- **Contas:** em Contas e backup, remova a conta Samsung e a do Google (se não removeu o GMS, isso ainda corta a sincronização).

## 4. Decisão pendente: tirar o PIN

Com um bloqueio de tela, o armazenamento do Termux só abre no primeiro desbloqueio (0029). Sem bloqueio, o Android abre o armazenamento sozinho no boot, e a pilha volta depois de queda de energia ou reinício sem ninguém por perto.

O custo: quem pegar o telefone tem acesso ao banco (hashes de senha, dados dos membros) e às evidências. Decida antes de executar. Se tirar, registre a escolha na 0029 ("Pré-requisitos no telefone").

## 5. Verificar

1. `adb reboot`, desbloqueie (se houver PIN) e espere a pilha.
2. `https://wx.jpgmaraujo.com.br/ready` responde 200.
3. `.\stats.bat > a14-depois.txt` e compare com `a14-antes.txt`.
4. `adb shell dumpsys meminfo > a14\meminfo-depois.txt` e compare os totais ("Total RAM", "Free RAM", "Used RAM").
5. Deixe rodar 24 horas e confira de novo com `.\stats.bat`: a pilha não reiniciou e a bateria fica abaixo do limite.

## Desfazer

```powershell
adb shell cmd package install-existing <pacote>
```

Para restaurar tudo o que saiu, compare `a14\pacotes-antes.txt` com a lista atual e reinstale cada pacote que falta. Se o telefone não iniciar, entre no modo de recuperação (Volume + e Liga com o cabo USB) e use "Wipe data/factory reset". Depois, refaça os pré-requisitos da 0029, rode `.\run.bat --ssh` e restaure o último backup (`remote-termux.sh restore`).

## Resultado

Preencha ao executar: data, modelo e versão do Android, lotes aplicados, RAM livre antes e depois, problemas encontrados.
