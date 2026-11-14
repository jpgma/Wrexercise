# 0064 — Cutucada com texto escrito

**Status:** DECIDIDA. Revê em parte `0063-cutucada.md`, que só aceitava mensagens prontas.

## Contexto

- A ADR 0063 descartou o texto livre na Cutucada para não exigir moderação.
- Os grupos são de amigos próximos, e a graça da zoeira está na piada interna, que nenhuma mensagem pronta cobre. O mantenedor pediu que se possa escrever a cutucada.

## Alternativas consideradas

- **Só mensagens prontas:** é a ADR 0063, sem a piada interna.
- **Texto livre visível para o grupo, na bolha da praça:** expõe uma mensagem pessoal a todos e transforma a praça num mural, que precisaria de moderação.
- **Texto livre longo ou com várias linhas:** vira conversa, e o aplicativo não é um chat.
- **Texto curto que só quem recebe lê, ao lado das prontas:** escolhida.

## Decisão

- **Tipo `Livre`:** além das 6 mensagens prontas, a cutucada pode ter o tipo `Livre`, com um texto de 1 a 80 caracteres. Os caracteres são contados como pontos de código, e um emoji conta como um.
- **Normalização no servidor:** espaços e quebras de linha viram um espaço só, os caracteres de controle saem e as pontas são cortadas. Texto vazio ou longo demais recebe `400 DADOS_INVALIDOS`, e também a mensagem pronta enviada com texto.
- **Privacidade:** só quem recebe lê o texto, na notificação e no push ("Ana te cutucou 👉 💬 "texto""). Na praça, a bolha mostra só 💬: todos sabem que houve uma cutucada, mas não o que ela diz.
- **Interface:** a grade de mensagens prontas ganha "✍️ Escrever a minha", que abre um campo com contador (0/80), o botão "Enviar" e o link "Voltar às prontas".
- **O resto não muda:** o limite de uma cutucada por par a cada 24 horas, só para quem dorme, e o bônus Despertador valem igual.

## Impacto

- **Migração 036:** acrescenta `cutucadas.texto` e o tipo `Livre`. Uma restrição exige texto só no tipo `Livre`, de 1 a 80 caracteres.
- `CutucarRequest` e `CutucadaResponse` ganham `texto`. As notificações ganham `cutucadaTexto`. A cena continua sem o texto.
- O texto é dado pessoal: não entra em log. Ele é apagado em cascata com a cutucada, e a notificação dura 90 dias, como as outras.
- **Risco aceito:** sem moderação, alguém pode escrever algo pesado. Num grupo de amigos, o administrador resolve isso pessoalmente, e o texto nunca fica exposto ao grupo.
