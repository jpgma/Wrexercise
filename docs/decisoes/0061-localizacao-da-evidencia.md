# 0061 — Localização da evidência por escolha do autor

**Status:** DECIDIDA. Complementa a ADR 0052 e fecha a parte de exibição de Q030.

## Contexto

- Desde a ADR 0052, o navegador lê o GPS do EXIF da foto (e, na ADR 0058, o ISO 6709 do vídeo) e o envia em campos à parte. O servidor guarda a latitude e a longitude em `evidencias_atividade`, mas nenhuma resposta da API as devolve.
- Ninguém foi avisado de que a localização era guardada. Um dado pessoal sem uso nem aviso só aumenta o risco: ele vai para os backups e complica a exclusão de conta (Q029).
- O local do treino ajuda quem avalia (academia, parque, rua), e muita gente gosta de mostrar onde treinou. Mas o local de uma foto tirada em casa revela o endereço.

## Alternativas consideradas

- **Parar de coletar:** a mais simples e a mais privada, mas perde o local para quem quer mostrá-lo.
- **Continuar guardando sem mostrar, com um aviso no formulário:** guarda um dado sem uso.
- **Mostrar sempre ao grupo:** expõe o endereço de quem treina em casa sem escolha.
- **O autor escolhe, com a opção ligada por padrão:** escolhida.

## Decisão

- **Caixa no formulário:** quando um comprovante escolhido tem localização, o formulário mostra "Mostrar ao grupo o local do comprovante", marcada por padrão. A caixa vale para todos os comprovantes da atividade e fica no rascunho do registro.
- **Desmarcada, a localização não sai do aparelho:** o navegador não envia `latitude` nem `longitude`. O servidor não precisa de uma coluna com a escolha: há coordenadas só quando o autor escolheu mostrá-las.
- **Câmera do aplicativo:** a captura não tem EXIF. Com a caixa marcada, a câmera pede a posição do aparelho ao navegador (`watchPosition`) enquanto está aberta, e a captura leva a última posição: a do instante da foto, ou a do fim da gravação. A primeira posição do GPS pode levar vários segundos, e a foto costuma sair antes: por isso a câmera pede também a posição aproximada ao abrir, e a foto sem posição espera a primeira por até 6 segundos ("Buscando o local…"). A primeira vez, o navegador pede a permissão. Sem permissão ou sem sinal, a captura segue sem local. O pedido para quando a câmera fecha, para não gastar bateria.
- **O grupo vê:** a API devolve `latitude` e `longitude` em cada evidência. A página da atividade e a tela de avaliação mostram o botão "Local" sobre o comprovante, que abre o Google Maps fora do aplicativo. É só um link: nada é carregado de servidores externos, e sem toque o Google não recebe nada.
- **Sem endereço legível:** transformar coordenadas em endereço exige um serviço externo.
- **Nada de aviso pelo local:** a localização não gera `Aviso`. Fica para depois, se o grupo pedir.
- **Coordenadas antigas apagadas:** foram guardadas sem aviso e sem escolha. A migração 034 as apaga.

## Impacto

- **Migração 034:** apaga a latitude e a longitude de todas as evidências. Os backups no Drive ainda as têm até saírem da retenção de 31 dias (ADR 0046).
- **Privacidade:** o autor que esquecer a caixa marcada mostra o local ao grupo. Não há como esconder depois: o autor só apaga a atividade nos primeiros 15 minutos (ADR 0054). Revisar se alguém pedir.
- **Exclusão:** apagar a localização junto com a conta entra em Q029.
- Relacionadas: 0046, 0052, 0054, 0058.
