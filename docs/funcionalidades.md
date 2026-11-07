# Wrexercise — Feature summary

> **Language:** This document uses ASD-STE100 Simplified Technical English. It is the only document in English in the repository. It gives agents and people a short, complete list of the features of the application, for example to discuss changes.
>
> **Maintenance:** Each change that adds, removes or changes a feature must update this document in the same change (see `AGENTS.md`).
>
> **Source of truth:** If this document and the code or the decisions in `docs/decisoes/` do not agree, the code and the decisions are correct. Correct this document.

## 1. Purpose of the application

Wrexercise is a private web application for small groups of friends. It helps them to exercise regularly.

A group sets one weekly goal, for example "4 sessions of 60 minutes each week". Each member records his or her exercise sessions. The other members examine each session and give a reaction. The reactions decide if the session is valid. Only valid sessions count for the goal.

The application is a Progressive Web App (PWA). The interface is in Brazilian Portuguese and is for smartphones first.

## 2. Terms

The code uses Portuguese names for the terms of the domain. This document uses the English words in the table.

| English word in this document | Name in the code | Name in the interface |
|---|---|---|
| user | `Usuario` | — |
| session (login) | `Sessao` | — |
| group | `Grupo` | Grupo |
| member | `Membro` | Membro |
| role | `Papel` | — |
| owner | `Proprietario` | Proprietário |
| administrator | `Administrador` | Administrador |
| invitation | `Convite` | Convite |
| challenge | `Desafio` | Desafio, Meta do grupo |
| goal | `Meta` | Meta |
| week | `Semana` | Semana |
| activity | `Atividade` | Atividade, treino |
| evidence | `Evidencia` | Comprovante |
| proof method | `Comprovacao` | — |
| reviewer | `Revisor` | — |
| reaction | `Reacao` | Reação |
| comment | `Comentario` | Comentário |
| administrative review | `Revisao` | Revisão administrativa |
| ranking | `Classificacao` | Classificação |
| streak | `Sequencia` | Semanas seguidas |
| achievement | `Conquista` | Conquista |
| notification | `Notificacao` | Notificação |

Reaction types (the code names are also used in the interface):

| Code name | Symbol | Interface text | Meaning |
|---|---|---|---|
| `Kudos` | 💪 | "Dar moral", "deu moral" | The activity is valid. |
| `Suspeito` | 🤨 | "Suspeito" | The activity must be examined. |
| `Rejeitar` | ❌ | "Rejeitar" | The activity is not valid. |

## 3. Accounts and login

### 3.1. Create an account

- A person can create an account only with an invitation link. The account, the login session and the entry into the group occur in one operation.
- If the system refuses the invitation, the system does not create the account.
- If the request has no invitation, the system refuses it with the code `CADASTRO_FECHADO`.
- Data for an account: display name (1 to 80 characters), e-mail (unique, maximum 254 characters) and password (8 to 128 characters).
- If the e-mail is already in use, the system tells the person.

### 3.1.1. First setup (`ConfiguracaoInicial`)

- When the application has no `Usuario`, the page `/configuracao-inicial` creates the first account. The data are the same as in section 3.1, without an invitation.
- The first account and its login session occur in one operation. After that, the user goes to the home page.
- The first user is a normal `Usuario`. The user has no global role. The user creates a `Grupo`, becomes its `Proprietario` and sends the invitations.
- While no `Usuario` exists, the pages `/entrar` and `/criar-conta` send the person to `/configuracao-inicial`.
- When one `Usuario` exists, the page closes for ever. The page sends the person to `/entrar`. The system refuses new requests with the code `CONFIGURACAO_JA_CONCLUIDA`.
- If two requests arrive at the same time, the system creates only one account.
- The page has no secret code. The first person who opens the page creates the account. The operator opens the application address right after the installation.
- The operator does not need a setting for this. There is no `Autenticacao:CadastroAberto`.

### 3.2. Log in and log out

- The user logs in with e-mail and password.
- The login stays active for 30 days. Each day that the user uses the application, the 30 days start again.
- Each device has its own login session. "Sair" closes only the session on the current device.
- If the e-mail or the password is incorrect, the message is the same. Thus the login page does not show which e-mails exist.
- The system accepts a maximum of 10 login and account attempts each minute from one IP address.

### 3.3. Change the account data

The Settings page ("Configurações") has these functions:

- Change the display name.
- Change the password. The user must type the current password. The system then closes all the other login sessions of the user.
- Install the application (see 13.1).
- Log out.

The user cannot change the e-mail.

### 3.3.1. Home screen mode (`ModoInicio`)

- Each account has a home screen mode: `App` or `Jogo`. The mode is a setting of the account. It is the same on all devices.
- The default mode is `App`. A new account starts in `App`.
- `PUT /api/usuarios/eu/modo-inicio` changes the mode. The body is `{ "modoInicio": "App" }` or `{ "modoInicio": "Jogo" }`. Any other value gives the code `DADOS_INVALIDOS`. The response is the session, as for the change of the display name.
- The session (`GET /api/autenticacao/sessao`) has the field `modoInicio`.
- The mode does not change the goal, the verification, the ranking or the streaks.

### 3.4. Forgotten password

- The login page does not have a "forgot password" function.
- The user must speak to the operator. The operator runs the command `reset-senha <email>`.
- The command closes all the login sessions of the account. It shows a link that the user can use one time, for 1 hour.
- The user opens the link and sets a new password. Then the user logs in again.

## 4. Groups

### 4.1. Create a group

- Each user can create a group. The user becomes the owner of the group.
- Data for a group: name (1 to 80 characters) and time zone (IANA name, default `America/Sao_Paulo`).
- Nobody can change the name or the time zone after the system creates the group.
- Nobody can delete a group.
- A user can be a member of more than one group.

### 4.2. Capacity

A group has a maximum of 10 active members.

### 4.3. Roles and permissions

| Operation | Owner | Administrator | Member |
|---|---|---|---|
| See the group, the members and the activities | Yes | Yes | Yes |
| See, make and cancel the invitation link | Yes | Yes | No |
| Create, edit and cancel a challenge | Yes | Yes | No |
| Do an administrative review of an activity | Yes | Yes | No |
| Change a role between administrator and member | Yes | No | No |
| Remove a member | All other members | Only members with the role "member" | No |
| Give the ownership to a different member | Yes | No | No |
| Leave the group | Only after the owner gives the ownership | Yes | Yes |

- When the owner gives the ownership to a member, the old owner becomes an administrator.
- The functions to change a role, remove a member and give the ownership are on the member page.
- Members see the name and the role of other members. They do not see the e-mail.
- If a user is not an active member, the system shows "not found". It does not show that the group exists.

### 4.4. Leave or remove

- When a member leaves or is removed, the access to the group stops immediately.
- The system keeps the activities and the reactions of that member.
- If a member leaves, the system cancels the invitation link that this member made.
- If an owner or administrator removes a member, the system cancels the current invitation link.
- A person who left can come back only with a new invitation. The person comes back with the role "member". The original entry date stays the same.

## 5. Invitations

- Each group has a maximum of one active invitation link.
- Many persons can use the same link. The link is valid for 7 days.
- Only the owner and the administrators can see, make and cancel the link. The link stays visible on the "Convidar amigos" page while it is valid.
- "Gerar novo link" cancels the old link and makes a new link.
- If the group is full, nobody can enter.
- A person who opens the link sees a preview before he or she enters. The preview shows the group name, the name of the person who made the link, the names of the members, the number of members and the current or next challenge. The preview does not show e-mails.
- If the link is expired, cancelled or does not exist, the message is the same: the invitation is not valid.
- A person with an account enters the group with "Aceitar convite". A person without an account creates an account on the same page.

## 6. Challenges

### 6.1. Rules

- A challenge sets the weekly goal of the group: a minimum number of sessions each week (1 to 7) and a minimum duration of each session (1 minute to 24 hours).
- A challenge has a name (1 to 80 characters) and an optional description (maximum 500 characters).
- A challenge has only full weeks. It starts on a Monday and stops on a Sunday, in the time zone of the group. It has a maximum of 52 weeks. The start date cannot be in the past.
- The creation page shows the next 12 Mondays as start dates and asks for the number of weeks.
- A group has only one challenge at a time. The dates of two challenges cannot overlap. A cancelled challenge does not use its dates.
- Nobody can change the goal or the dates after the system creates the challenge. The owner and the administrators can change the name and the description while the challenge is scheduled or active.
- The owner and the administrators can cancel a scheduled or active challenge. Nobody can make a cancelled challenge active again. A cancelled challenge stays visible.

### 6.2. Status

The system calculates the status from the dates. No person and no scheduled task changes the status.

| Status | Condition |
|---|---|
| `Agendado` | The start date is in the future. |
| `Ativo` | Today is between the start date and the end date. |
| `Encerrado` | The end date is in the past. |
| `Cancelado` | An owner or administrator cancelled the challenge. |

### 6.3. Participants

- All active members of the group participate. There is no "join" function and no "leave" function for a challenge.
- A week counts for a member only if the week started on or after the day when the member entered the group. Example: a person enters on a Tuesday. The first week that counts starts on the next Monday.
- For the weeks before the entry, the history shows "não participou".
- A member who leaves the group is not in the challenge results.

### 6.4. Featured challenge

The group pages and the home page show one challenge. It is the active challenge. If there is no active challenge, it is the next scheduled challenge.

## 7. Activities

### 7.1. Record an activity

An active member records an activity with these data:

- Type: `Musculacao`, `Corrida`, `Caminhada`, `Ciclismo`, `Natacao`, `Esporte`, `Yoga`, `Calistenia` or `Outro`.
- Start date and time. It cannot be more than 1 hour in the future.
- Duration: 1 minute to 24 hours.
- Description: optional, maximum 500 characters.
- Evidence: 0 to 3 images.

The proof method comes from the evidence:

- No image: `RegistroManual`.
- One or more images: `Fotografia`.
- The code also has `CapturaDeTela`. The interface does not use it.

The form shows "Conta para a meta" when the duration is equal to or more than the minimum duration of the challenge.

Nobody can edit or delete an activity.

### 7.2. Evidence

- The browser makes each image smaller before it sends it: a maximum of 1600 pixels on the longest side, JPEG quality 0.82.
- The browser also makes a thumbnail: 240 pixels, JPEG quality 0.6. The browser removes the image metadata, for example the location.
- The server accepts JPEG, PNG and WebP files. The maximum size is 5 MB for each file and 100 KB for each thumbnail. The server examines the real content of the file.
- Only the author can attach evidence. The author can attach evidence only while the activity has the status `AguardandoFeedback`.
- Nobody can replace or delete evidence.
- Only active members of the group can see the evidence.
- The feed shows the thumbnail. The full image loads only when the user opens it.
- The full-screen gallery shows the images of one activity. The user can swipe or use the arrow keys to go to the next image. The user can tap two times to zoom. The "back" button of the phone closes the gallery.

### 7.3. Week of an activity

All weeks in the application start on Monday and stop on Sunday, in the time zone of the group.

The week of an activity is the week of its start date and time, in the time zone of the group. The system does not divide an activity between two weeks.

### 7.4. Lists of activities

- Group feed ("Atividades"): all the activities of the group, the most recent first. Filters: "Todas", "Minhas", "Pendentes", "Verificadas".
- The member page shows the activities of one member.
- The feed loads 20 activities at a time. "Carregar mais" loads the next 20.
- Each item in the feed shows the author, the type, the duration, the date, the thumbnail, the status and the reactions.

## 8. Community verification

### 8.1. Reviewers

- When a member records an activity, the system makes a list of reviewers. The list has all the active members of the group at that time, but not the author.
- The list does not change when members enter or leave the group.
- The author cannot react to his or her own activity.
- If the list is empty (the author is the only member):
  - An activity without evidence becomes `Verificada` immediately.
  - An activity with evidence becomes `Verificada` when the author attaches the first image.

### 8.2. Reactions

- Each reviewer can give one reaction: `Kudos`, `Suspeito` or `Rejeitar`.
- The reaction window is 48 hours from the time when the author recorded the activity.
- A reviewer can change his or her reaction while the window is open. A reviewer cannot remove a reaction.
- The `Rejeitar` reaction asks for a confirmation.
- The number of rejections necessary to reject an activity is:

  ```text
  rejections = min(reviewers, max(2, ceil(reviewers / 2)))
  ```

  Examples: 1 reviewer → 1. 2 reviewers → 2. 5 reviewers → 3. 9 reviewers → 5.

- The interface shows this number while the activity is `EmRevisao`.

### 8.3. Verification status

After each reaction, the system calculates the status again:

| Condition | Status |
|---|---|
| The number of `Rejeitar` reactions is equal to or more than the necessary number. | `Rejeitada` |
| There is a minimum of one `Suspeito` or `Rejeitar` reaction, but less than the necessary number. | `EmRevisao` |
| There is no negative reaction, and all reviewers reacted. | `Verificada` |
| There is no negative reaction, and some reviewers did not react. | `AguardandoFeedback` |

- `Verificada` and `Rejeitada` are final. After these statuses, the system does not accept reactions.
- When a reviewer changes the only negative reaction to `Kudos`, the activity leaves `EmRevisao`.
- A background task runs approximately each 10 minutes. When the window closes, an activity in `AguardandoFeedback` becomes `Verificada`.
- When the window closes, an activity in `EmRevisao` stays in `EmRevisao`. Only an administrative review can change it.
- The code has the status `Contestada`. The application does not use it.

### 8.4. Administrative review

- The owner and the administrators can decide an activity in `EmRevisao` at any time, also while the window is open.
- The decision is `Verificada` or `Rejeitada`. A justification is necessary (maximum 500 characters).
- The system keeps a record of the person, the date and the justification. Nobody can change the decision.
- The group page shows a list "Disputas para decidir" to the owner and the administrators.

### 8.5. Review comments

- Active members, also the author, can write comments only while the activity is `EmRevisao`.
- A comment has a maximum of 500 characters.
- After the review, the comments stay visible but nobody can add more.
- Nobody can edit or delete a comment.

### 8.6. Review queue

- The "para avaliar" list shows the activities that wait for a reaction from the current user, in the open window.
- The group pages show a message when the user has activities to examine.
- The review page ("Avaliar atividade") shows one activity. After the reaction, it shows a link to the next activity in the queue.

## 9. Progress and competition

### 9.1. Weekly progress

- Only activities with a duration equal to or more than the minimum duration of the challenge count.
- One activity counts as one session. A long activity does not count as two sessions.
- Additional sessions in one week do not count for other weeks.
- Definitive progress: activities with status `Verificada`.
- Provisional progress: activities with status `AguardandoFeedback`, `EmRevisao` or `Contestada`.
- A member completes a week when the definitive progress is equal to or more than the goal.

### 9.2. Group goal for the week

The group page shows the total of verified sessions of all members in the current week. The target is the goal multiplied by the number of members who count in this week. A new member does not decrease the group result.

### 9.3. Ranking

The challenge page shows the ranking. The order is:

1. Number of completed weeks.
2. Total number of verified sessions in the challenge.
3. Current streak: the number of completed weeks one after the other, from the most recent week.
4. Display name.

The application does not use points.

### 9.4. History

- The challenge page shows a chart with the history of the weeks of the current user.
- The group summary shows the featured challenge, the weekly progress of the user and the first 3 members of the ranking. Each ranking item has the `usuarioId` of the member.
- Each `Avatar` that has a user ID shows the portrait (row 0, column 0) of a default sheet. The sheet comes from a hash of the user ID, out of 6 sheets in the app bundle. If the ID is missing, the `Avatar` shows the initials.

## 10. Profile, member page and achievements

### 10.1. Profile ("Perfil")

The profile shows the data of the current user in all his or her groups:

- Total of verified activities.
- Total time.
- Best streak.
- Chart of the last 8 weeks, from Monday to Sunday. The current week comes from the time zone of the groups of the user. If the groups have different time zones, the first time zone that starts a new week is used. The chart does not show a goal line, because each group can have a different goal.
- Achievements.
- List of groups.

### 10.2. Member page

- Only active members of the group can open it.
- It shows only data from that group: name, role, progress in this week, completed weeks, a chart and the activities of the member.
- It does not show the e-mail or the achievements.

### 10.3. Achievements

The system calculates the achievements each time. It does not keep them in the database.

| Achievement | Condition |
|---|---|
| Streak of 4, 8 and 12 weeks | The best streak in one challenge. The system does not add streaks from different challenges. |
| 10, 50 and 100 activities | Number of verified activities. |
| Madrugador | 10 verified activities that started before 07:00 in the time zone of the group. |

- Achievements do not change the ranking.
- Only the user sees his or her achievements.
- When a user leaves a group, the user keeps the achievements from that group.

## 11. Notifications

The system makes a notification for these events:

| Event | Recipients | Code name |
|---|---|---|
| A member records an activity. | The reviewers. | `NovaAtividade` |
| A reviewer gives his or her first reaction to an activity. A changed reaction does not make a new notification. | The author. | `Reacao` |
| A member writes a comment. | The author, the administrators and the persons who wrote comments, but not the writer. | `Comentario` |
| The activity becomes `Verificada`, `Rejeitada` or `EmRevisao`. | The author. | `SituacaoAlterada` |
| The activity becomes `EmRevisao`. | The administrators, but not the author. | `Disputa` |

- The system joins unread reactions and comments of one activity into one notification with a total.
- The bell on the home page and on the profile page shows the number of unread notifications.
- The notifications page shows the 50 most recent notifications. When the user opens a notification, it becomes read. "Marcar todas como lidas" marks all as read.
- The bell and the list update when the window becomes active again. The application does not poll the server.
- The user does not see notifications from groups that he or she left.
- The system deletes notifications 90 days after the last change.
- There are no push notifications, no e-mails, no reminders and no notification settings.

## 12. Pages and navigation

| Page | Path | Content |
|---|---|---|
| Login | `/entrar` | E-mail and password. |
| Create account | `/criar-conta` | Account data and invitation link. |
| Invitation | `/convite#token` | Preview of the group and "Aceitar convite". |
| Reset password | `/redefinir-senha#token` | New password. |
| Root | `/` | Opens the home page of the first group. If the user has no group, it shows the options to create a group or to use an invitation. |
| Home ("Início") | `/grupos/:id/inicio` | Greeting, bell, weekly progress ring, days of the week, message about activities to examine, recent activities. |
| Group ("Grupo") | `/grupos/:id` | Group goal for the week, progress of each member, disputes (administrators), members, feed, "Sair do grupo". |
| Activities | `/grupos/:id/atividades` | Group feed with filters. |
| Record activity | `/grupos/:id/atividades/nova` | Activity form. |
| Activity | `/atividades/:id` | Details, evidence, reactions, administrative review, comments. |
| Review | `/grupos/:id/revisar/:atividadeId` | One activity to examine and a link to the next. |
| Member | `/grupos/:id/membros/:usuarioId` | Member page and the administration functions. |
| Invite | `/grupos/:id/convidar` | Invitation link. |
| Create challenge | `/grupos/:id/desafios/novo` | Challenge form. |
| Challenge | `/desafios/:id` | Goal, period, weekly progress, ranking, history, edit, cancel. |
| Create group | `/grupos/novo` | Group form. |
| Profile | `/perfil` | See 10.1. |
| Settings | `/perfil/configuracoes` | See 3.3. |
| Notifications | `/notificacoes` | See 11. |

- On a phone, a tab bar at the bottom shows "Início", "Atividades", "Grupo", "Perfil" and a button to record an activity.
- On a large screen, a side bar shows the same items.

### 12.1. Appearance

- The application uses the Wrexercise visual identity (see `docs/decisoes/0027-identidade-visual-wrexercise.md`).
- The application has a light theme and a dark theme. It uses the theme of the system.
- When the user completes the weekly goal, the progress ring changes to green and shows a short burst one time.
- After the user records an activity, the page shows "Boa! Tá registrado." and a short burst.
- When the user has reduced motion on in the system, the application does not show these animations.

## 13. PWA and offline use

### 13.1. Installation

- The user can install the application on the home screen of the phone.
- On Chromium and Android, the Settings page shows "Instalar aplicativo".
- On iOS, the Settings page tells the user to use "Compartilhar" and "Adicionar à Tela de Início".
- In the browser, a notice at the top of all pages also tells the user to install the application. On Chromium and Android, the notice has an "Instalar" button. On iOS, it shows the same instruction as the Settings page.
- The notice shows only when these conditions are true: the user is logged in, the connection is available, there is no "Nova versão disponível" notice, and the application is not installed.
- The notice does not close automatically. The user can close it with the "X" button. Then it does not show again for 30 days. A cookie keeps this choice.
- Firefox and Safari on a computer do not show the notice, because they do not let the application start an installation.

### 13.2. Offline use

- The service worker keeps only the files of the application. It does not keep data from the API, images or thumbnails.
- Without a connection, the application shows "Você está sem conexão" on all pages.
- Without a connection, the user can open only the pages that he or she opened before in the same version.
- Without a connection, write operations fail immediately. The application does not keep them for later.
- Without a connection and without a login session in memory, the application shows a full "no connection" page.

### 13.3. Updates

- The application looks for a new version when the page becomes visible again.
- When a new version is available, the application shows "Nova versão disponível" and an "Atualizar" button. The page reloads only when the user pushes the button.

## 14. Operation

These functions are for the operator. They are not in the interface.

- `migrate`: applies the SQL migrations.
- `reset-senha <email>`: makes a link to reset a password (see 3.4).
- `GET /health` and `GET /ready`: show if the application and the database operate.
- Backup: the `backup` service makes a copy of the database and of the evidence files. It keeps the copies for 7 days. The `restore` command puts a copy back. The operator must stop the application before a restore.
- `run.bat` starts the application on Windows. `run.bat --tunnel` makes the application available on the internet at `wx-dev.jpgmaraujo.com.br` through the Cloudflare tunnel `wx-dev`. This is for tests only.
- `run.bat --ssh` puts the application on an Android phone through SSH. It asks for the host, the port (8022 by default), the Termux user name from `whoami` and the SSH key. It saves the answers in `%LOCALAPPDATA%\Wrexercise\ssh`. The phone runs the database, the application and its own Cloudflare tunnel as native Termux processes, with no `proxy`, no Docker and no virtual machine. Open sign-up stays off. The phone starts the processes again after a restart. The operator needs an SSH key without a password and `sshd` running in Termux. Termux is the only target. The Galaxy A14 is the production environment. It serves `https://wx.jpgmaraujo.com.br` through the Cloudflare tunnel `wx`.
- `run.bat ssh-backup` makes a backup on the phone. Then it copies the backup folder of the phone to `BACKUP_DIR` on Windows. It does not delete old copies on Windows.
- `stats.bat` shows how the phone runs the application. It shows CPU and memory of the `app`, the database and the tunnel. It also shows the load, memory, battery and temperature of the phone, the result of `/health` and `/ready`, database size and connections, disk use and the last backup. `stats.bat --watch` repeats the report until Ctrl+C. It reads the SSH profile of `run.bat --ssh`. If the profile is missing or the phone does not answer, it asks for the host, port, user and key, and saves the answers. It only reads data.
- `deploy/remote-termux.sh reset-senha <email>` makes the reset link of a password on the phone.

## 15. Security rules that have an effect on features

- Each private operation makes sure that the user is logged in and is an active member of the group.
- The application does not show e-mails to other users.
- Invitation and password reset tokens are in the fragment of the URL (after `#`). The browser does not send the fragment to the server.
- Write operations from a different origin fail.
- The application does not load fonts, icons or code from external servers.

## 16. Features that the application does not have

These features are not in the application. Some are out of scope in `docs/arquitetura.md` §19.

- Edit or delete an activity, evidence or comment.
- Edit the goal or the dates of a challenge.
- Change the name or the time zone of a group. Delete a group.
- Change the e-mail of an account. Delete an account.
- Reset a password without the operator.
- More than one challenge at a time in one group. Different goals for different members.
- Points based on intensity, calories, GPS, steps, heart rate, wearables, Strava.
- Public groups, user search, private messages.
- Push notifications, e-mail, scheduled reminders.
- Automatic validation of images.
- Write operations without a connection.
