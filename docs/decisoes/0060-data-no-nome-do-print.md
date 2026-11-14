# 0060 — Data no nome do print e aviso "Sem data" fora dos cartões

**Status:** DECIDIDA. Complementa as ADRs 0052 (avisos) e 0058 (câmera do aplicativo).

## Contexto

- Todo print enviado como evidência recebia `FotoSemData`. O leitor de EXIF só entende JPEG, e um print não tem `DateTimeOriginal`. Sobra a data do arquivo (`Arquivo`), que a ADR 0052 só leva em conta para trás: copiar ou receber o arquivo também muda essa data.
- `FotoSemData` não impede a verificação, mas o selo "Foto sem data" aparecia no feed em quase todo print. O grupo passou a ignorá-lo.
- O Android, o Windows e o macOS põem a data e a hora da captura no nome do arquivo: `Screenshot_20261114-083012.png` (Pixel), `Screenshot_20261114_083012_Strava.jpg` (Samsung), `Screenshot_2026-11-14-08-30-12-345_com.strava.jpg` (Xiaomi), `Captura de Tela 2026-11-14 às 08.30.12.png` (macOS). O nome não muda ao copiar o arquivo.
- O iPhone não põe a data no nome. O print dele é PNG, e o leitor não lê PNG.

## Alternativas consideradas

- **Ler o texto da imagem (OCR):** pegaria a data que o aplicativo de treino mostra, mas exige uma biblioteca grande no navegador. Contraria o princípio de simplicidade.
- **Ler XMP e o bloco `eXIf` do PNG:** pode dar a data do print do iPhone, mas ainda não há um arquivo real para conferir. Fica para depois.
- **Parar de gerar `FotoSemData`:** perde o dado para a revisão mensal dos avisos (ADR 0052).
- **Data no nome do print e selo fora dos cartões:** escolhida.

## Decisão

- **Origem `Print`:** quando o nome do arquivo segue um dos padrões de captura de tela, o navegador envia a data e a hora do nome, no relógio do aparelho, com a origem `Print`. Ela vem antes do EXIF. O servidor trata `Print` como `Exif`: vale nos dois sentidos da janela [início − 2 h, fim + 12 h].
- **Selo "Print":** a API devolve `print: true` na evidência com a origem `Print`. A página da atividade e a galeria mostram o selo "Print", que lembra quem avalia de conferir a data que aparece na imagem.
- **"Sem data" fora dos cartões:** o feed não mostra mais o selo de `FotoSemData`. O aviso continua gravado e aparece na página da atividade e na tela de avaliação.
- Como os outros sinais da ADR 0052, o nome do arquivo pode ser forjado. É um aviso, não uma prova.

## Impacto

- **Migração 032:** `Print` entra na lista de `origem_data_captura`.
- **iPhone:** o print continua com `FotoSemData` (agora só na página da atividade) e sem o selo. Ler o PNG fica para quando houver arquivos reais para testar.
- **Seletor de fotos do Android:** se o seletor entrega o arquivo com outro nome, a data do nome se perde e vale a regra antiga. Conferir num aparelho real.
- Prints de gravação de tela (vídeo) não são lidos pelo nome.
- Relacionadas: 0052, 0058.
