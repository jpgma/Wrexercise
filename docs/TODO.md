1. Running it on the phone (biggest risk)

- Uptime monitor is on (0051): UptimeRobot checks /ready every 5 min.
- SSH from outside home. erros.bat (0056) reads recent errors, and an internal error now shows "Código: XXXXXX" that matches the log line. But SSH to the A14 only works over USB (adb forward) or the home Wi-Fi, so away from home you can't read the log. The Cloudflare SSH route is ready in the Environment repo (`ssh-a14.jpgmaraujo.com.br`, commit ec308c8); finish it with the one-time steps in Environment README, "SSH fora de casa" (Access app first, then `run.bat implantar` and `run.bat dns`, then cloudflared on Windows and the `a14-fora` profile). After that: `erros.bat --perfil a14-fora`.
- No real-load numbers yet (Q025). Memory, query speed and disk use on the A14 were only measured at startup (about 25 MB). Login timing was measured on x64, not ARM64 (0006). Watch stats.bat for the first couple of weeks, especially how fast the evidence folder grows against phone storage and the Drive quota estimate in 0046.
- app.log is trimmed to its last 1 MB only when the app restarts. Check its size in stats.bat after a few weeks; if it grows a lot, trim it in the daily backup task too.

2. Gaps people will hit in the first week

- Activity correction shipped (0054): the author deletes within 15 minutes while nobody contested; owner/admin can mark a pending activity `Anulada`. Watch whether 15 minutes is enough and whether admins use `Anulada` for real suspicions instead of rejecting.
- Photo location is now stored (0052) but not shown. Decide whether to show it, and tell participants it is kept (a line on the record form). Deleting it belongs with Q029.
- The review queue blocks new entries. 409 AVALIACOES_PENDENTES stops anyone with pending reviews from recording an activity (0041). That's a strong push; check whether it feels annoying or helpful once real people use it.

3. Rules to adjust with real data

These were decided before anyone used the app, and several ADRs say to look again once real use starts:
- Avisos (0052): count each type after the first month. Check `FotoParecida` false positives (gym mirror selfies; the 6-bit limit), how many flagged activities end in EmRevisao waiting for an admin, and whether the 48 h backdating limit blocks honest late logging.
- The consensus formula and the 48 h review window (0012, 0015). Is anything ever rejected, or does everything sit in EmRevisao?
- Daily and proportional goals (0040, 0043, 0045). 0045 notes the rule changed after only two days with no real use.
- Jogo: the early-morning bonus may encourage training at dawn instead of consistency (gamificacao §12.3). The Enfeite clutter with 10 people on a 360 px screen still needs checking (§12.4). Achievement limits of 10 per type should be revisited with real data (§12.5).
- gamificacao §13 lists success criteria (people log as often as before, Energy gets spent weekly, cards spark conversation). Decide now how you'll judge them, even if it's just a monthly query.

4. Open questions

questoes-abertas.md is up to date (0055). Q006, Q007, Q017, Q018, Q021, Q023 and Q032 are closed with links to the code. Q027 is partial: activity creation, reactions and the feed have no rate limit of their own. §12 now lists what is still open.

5. Can wait

Q016 (abuse of reactions) and Q026 (query tuning, only if something gets slow), G5 Missões, and Q029 (retention and account deletion). Q029 can wait until someone asks to leave and have their data removed, but keep in mind there's currently no way to delete an account or a group.

My suggested order: set up SSH from outside home through the Cloudflare tunnel, then decide on showing the photo location (Q030), then a first look at real-load numbers with stats.bat.