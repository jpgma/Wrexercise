1. Running it on the phone (biggest risk)

- Turn on the uptime monitor (decided in 0051). Create the free UptimeRobot account and the /ready monitor, then do the stop/start test (README, "Monitor externo"). Until then, nothing tells you when it goes down.
- No real-load numbers yet (Q025). Memory, query speed and disk use on the A14 were only measured at startup (about 25 MB). Login timing was measured on x64, not ARM64 (0006). Watch stats.bat for the first couple of weeks, especially how fast the evidence folder grows against phone storage and the Drive quota estimate in 0046.
- Reading errors. Logs only go to stdout in Termux. Make sure there's an easy way to see recent errors remotely, because "it gave me an error" will be the first bug report.

2. Gaps people will hit in the first week

- Nobody can edit or delete an activity (funcionalidades.md §… "Nobody can edit or delete an activity", "Nobody can replace or delete evidence"). The wrong type, a typo in the duration, or a double submit stays forever, and the only fix is getting the group to reject it. I expect this to be the first complaint. A short owner/admin "void" action, or a few minutes where the author can delete their own entry, would help.
- Duplicate submissions (Q020, still open). The create endpoints have no idempotency key, so a retry on a flaky connection can post the activity twice. Evidence sha256 is stored but nothing reads it.
- Location data can leak (Q030). comprimirImagem.ts removes EXIF only when re-encoding through the canvas works. If it fails (unsupported format, decode error), the original file is uploaded with its EXIF/GPS intact, and the server doesn't strip it. That's a real privacy hole for photos taken at home.
- The review queue blocks new entries. 409 AVALIACOES_PENDENTES stops anyone with pending reviews from recording an activity (0041). That's a strong push; check whether it feels annoying or helpful once real people use it.

3. Rules to adjust with real data

These were decided before anyone used the app, and several ADRs say to look again once real use starts:
- The consensus formula and the 48 h review window (0012, 0015). Is anything ever rejected, or does everything sit in EmRevisao?
- Daily and proportional goals (0040, 0043, 0045). 0045 notes the rule changed after only two days with no real use.
- Jogo: the early-morning bonus may encourage training at dawn instead of consistency (gamificacao §12.3). The Enfeite clutter with 10 people on a 360 px screen still needs checking (§12.4). Achievement limits of 10 per type should be revisited with real data (§12.5).
- gamificacao §13 lists success criteria (people log as often as before, Energy gets spent weekly, cards spark conversation). Decide now how you'll judge them, even if it's just a monthly query.

4. Update the open-questions doc

questoes-abertas.md is behind the code. Several questions marked open or proposed are already handled in the code:
- Q017 and Q018: 5 MB limit, JPEG/PNG/WebP validation, random file names, authenticated access.
- Q023: resizing and compression in the browser.
- Q027: rate limiters for login, activities, reactions and Jogo.
- Q006 and Q007 are probably settled in practice too.

Q001's text still talks about the Oracle VM. Marking these decided, with links to the code, would leave a clear list of what's actually still open.

5. Can wait

Q016 (abuse of reactions) and Q026 (query tuning, only if something gets slow), G5 Missões, and Q029 (retention and account deletion). Q029 can wait until someone asks to leave and have their data removed, but keep in mind there's currently no way to delete an account or a group.

My suggested order: turn on the uptime monitor, the EXIF fallback fix, then activity correction/void. I can start on any of them, or update questoes-abertas.md first.