# Tribulation — Social Launch Posts

> Voice: murim / cultivation for player channels; craft-forward for r/godot. Replace `[App Store link]` and `[handle]`. All claims verified against the project files.
> Hashtag note: don't dump 30 tags. The sets below are curated.

---

## X / Twitter

### Launch post (pinned)
> The Heavenly Net is closing behind you.
>
> Run the cultivator's road. Read every strike — leap, slide, sidestep, slash — break through six realms, and survive the Tribulation.
>
> No ads. No IAP. Out now on iOS 🗡️
> [App Store link]

[Pair with the 25s trailer or a 6s loop of a high-slash + lane-volley dodge.]

### Thread (post the launch tweet, then reply down the chain)

**1/** I spent the last weeks building Tribulation — an endless runner where the obstacles aren't walls. They're martial techniques, and they're swinging at you. 🧵

**2/** Every hazard telegraphs its plane with a colored qi-line.
Amber low → leap.
Cyan high → slide.
Red lane → sidestep.
White flash → slash it or die.
You learn to *read* attacks, not memorize a track.

**3/** You cultivate as you run. Six realms, Qi Condensation to Ascension — and each breakthrough awakens a new verb: a second jump, a glide, sword-flight (御剑), and finally a lightning Tribulation you have to survive to ascend.

**4/** Fail the Tribulation and you suffer Qi Deviation: your realm holds, but the climb resets. Ascension is meant to take weeks. The road is long and bitter on purpose.

**5/** No ads. No in-app purchases. No data collected. One dev, built in Godot. The whole road is in the box.
Walk it: [App Store link]

### Standalone hook tweets (space these across launch week)
- "Most runners give you walls. This one gives you a sword aimed at your neck. Slide. 🗡️ [link]"
- "Six realms. Each one doesn't buff a stat — it teaches you a new way to move. [link]"
- "The Heavenly Net closes from every edge. Standing still is death. Every kill buys you a breath. [link]"
- "It will take you weeks to Ascend. That's not a bug. That's the road. [link]"

---

## Reddit

### r/iOSGaming (and r/AndroidGaming once shipped there)
**Title:** Tribulation — a cultivation endless runner where every obstacle is a martial attack you have to read (no ads, no IAP, no data collected)

**Body:**
> I just launched my first game on the App Store and wanted to share it with the people most likely to get it.
>
> **Tribulation** is a three-lane endless runner built around one idea: the road doesn't put dead walls in front of you, it *attacks* you. Each hazard telegraphs with a colored qi-line — amber low (jump), cyan high (slide), red lane (sidestep), white (slash) — so it's about reading techniques, not memorizing a track. Late runs stack two planes at once.
>
> You cultivate as you run through six realms, and each breakthrough unlocks a new verb — double jump, glide, sword-flight, and a lightning Tribulation gauntlet you have to survive to ascend. Fail it and you keep the realm but lose the climb.
>
> It's a deliberately long road (Ascension is a weeks-to-months goal), with daily rewards, trials, achievements, and a permanent upgrade hall — but no ads, no IAP, and no data collected. One developer, made in Godot.
>
> Free on iOS: [App Store link]. Honest feedback welcome — especially on the difficulty curve.

### r/godot (craft angle — this community rewards build detail)
**Title:** Shipped my first Godot 4.6 mobile game — a cultivation runner with procedural art, object pooling, and data-driven balance. Some notes.

**Body:**
> Tribulation just hit the App Store. A few things other Godot devs might find useful:
>
> - **No artist.** Player, foes, hazards, and the Heavenly Net "suppression formation" are all built from primitives + shaders + CC0 assets (Poly Haven HDRIs, Poly Pizza props). I tried Mixamo/GLB characters and reverted to a procedurally-animated primitive swordsman — it read better and shipped lighter.
> - **Object pooling** the dense spawn end (orbs, pills, shell-pooled hazards) killed the GC hitches. `call_deferred` on signal-context retires to dodge the physics-flush "can't change state" error.
> - **Data-driven balance** via a `balance.json` loaded over code defaults — retune the whole difficulty curve with no recompile.
> - **Local-only telemetry** (JSONL on device) so I can validate the curve from the Xcode container while keeping App Store privacy at "No Data Collected."
> - Trimmed the iOS build from ~79MB to ~21MB with an export exclude filter + OGG music instead of PCM WAV.
>
> Happy to answer anything about the murim re-theme or the realm-gated ability system. [App Store link]

### r/incremental_games / r/cultivationfics (long-grind + lore fit)
**Title:** Made a cultivation runner where you climb the realms the slow, bitter way — Ascension takes weeks

**Body:**
> If you like the "the road is long and you are weak" fantasy of xianxia, Tribulation might be your thing. You run, dodge telegraphed martial techniques, gather Qi, and break through Qi Condensation → Foundation → Golden Core → Nascent Soul → Spirit Severing → Ascension. Each realm awakens a real new mechanic, and breakthroughs are gated behind a Heavenly Tribulation lightning gauntlet — fail and it's Qi Deviation. Persistent realm, resettable climb, permanent upgrade hall. No ads, no IAP. [App Store link]

---

## TikTok / YouTube Shorts / Reels

**Concept:** screen-record one clean run with a near-death dodge and a breakthrough. Let the gameplay carry it. Text-on-screen does the talking (most watch muted).

**On-screen text beats:**
1. (0-2s) "POV: the obstacles fight back"
2. (2-6s) "amber = jump / cyan = slide / red = dodge"
3. (6-12s) show a stacked dodge → "read two attacks at once"
4. (12-18s) breakthrough flash → "every realm = a new power"
5. (18-22s) Tribulation lightning → "survive this to ascend"
6. (end) "Tribulation — free on iOS. no ads. no IAP."

**Caption:**
> the obstacles fight back 🗡️ a cultivation runner where you read martial techniques instead of memorizing a track. free, no ads, no IAP. link in bio. #xianxia #cultivation #indiegame

**Hashtag set (rotate, ~5-8 per post):**
`#xianxia #cultivation #wuxia #murim #indiegame #godotengine #mobilegame #endlessrunner #manhua #gamedev`

**3 alt hook lines (test which retains):**
- "this isn't a runner, it's a duel you're losing"
- "named every obstacle after a real martial technique"
- "it takes WEEKS to beat this on purpose"
