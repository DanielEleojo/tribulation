# Ponytail Debt Ledger & Paydown Plan

Every deliberate shortcut in the Unity port is marked with a `ponytail:` comment naming its
ceiling + upgrade path. This file is the durable ledger so deferrals can't silently rot.

- **Scan:** `grep -rnE '(#|//) ?ponytail:' tribulation-unity/Assets/Scripts`
- **Snapshot:** 72 markers (~19 are deliberate design decisions, not real debt).
- **Workflow:** every batch implemented by Sonnet sub-agents, reviewed by the Opus main agent
  with `/goal` (read real files + re-run the `coretest` harness, must stay **110 green**).

Status legend: ⬜ todo · 🟦 in progress · ✅ done

---

## Batch 0 — Close the false debt  ✅  (done: 72→58 markers, 110 green)
The `no-trigger` markers are deliberate design decisions, NOT debt. Reword their comments from
`ponytail:` to `note:` so they leave the ledger. No behavior change. Ledger 72 → ~53.

- AudioShaping.cs:36, Music.cs:4, SoundManager.cs:4 — linear volume, no AudioMixer (by design).
- GameCore.cs:33 (no RealmChanged event), :318/:345 (`_pill_bonus`=0 until shop), :368 (bus-apply is the MonoBehaviour's job).
- Telegraph.cs:24,:43 — static lookup maps / static Resolve (single source of truth).
- Gate.cs:11 — gate pooling skipped (low-freq, plain Instantiate/Destroy is fine).
- InkArt.cs:5,:6,:200 — texture-gen perf notes / only 128×16 cached.
- Spawner.cs:287 — primitive-cube rationale.
- TelegraphSystem.cs:172 — cache Camera.main (micro-perf, rare labels).
- MainMenu.cs:8 — load-order race note.

> Keep `_pill_bonus`=0 (GameCore:318/:345) flagged in **Batch 3** instead — it becomes real debt once the shop exists.

## Batch 1 — Wire-ups now possible  ✅  (done: death-distance + persist names, 56 markers, 111 green)
Gaps deferred "to Game.cs" that are wireable today because events/HUD/SaveData exist.
- Game.cs:64 — pass real death distance into `Die()` (distance hook on the player).
- GameCore.cs:193,:278,:467,:482,:489 — sfx / HUD flash / camera shake / banners → route to `SoundManager` + a HudOverlay flash+banner API.
- TelegraphSystem.cs:61 — **persist first-encounter technique names** to `SaveData` (Core add: `seenTechniques` list + load/save; +EditMode tests).

## Batch 2 — Remaining UI-5 screens  ⬜  (HITL, 4 slices)
Turn the disabled ghost buttons real, on the `InkArt` toolkit + existing EventSystem.
- MainMenu.cs:10,:11,:180 — **pause**, **settings** (audio/reset), **cultivation shop** (spend stones), **journal/achievements/daily**.

## Batch 3 — Gameplay completeness (#5–#8)  🟦  (in progress)
Deferred systems, mostly pure `GameCore`/`Spawner` logic.
- **3a powerups ✅** (113 tests) — double-on-orb, dash pass-through + 12.0 speed bonus, Iron Aegis → `Survivability.GrantShield` via `Game.ActivatePowerup`. Faithful to game.gd/player.gd.
- **3c difficulty offset ✅** (114 tests) — `GameCore.DifficultyOffset()=Realm*difficulty_per_realm(12)` seeds player speed-ramp (`_runTime`) + spawner `_elapsed` via `Game.BeginRun`. Higher realms start faster+denser.
- **3b trials ✅** (117 tests) — full Cultivation Trials in GameCore (5 templates slay/li/qi/combo/survive × 3 tiers, roll 3/run, TrialAdd/TrialMax, reward+`TrialFulfilled` event on completion; triggers wired; Game feeds "li" + plays sfx). Faithful to game.gd. NOTE: on-screen trial readout (`Trials` list + "Trial fulfilled" banner) now deferred to **Batch 2 HUD** (GameCore.cs:201, Game.cs).
- Remaining (3d/3e):
- GameCore.cs:318,:345 — `_pill_bonus()` shop upgrade bonus (real once shop exists).
- GameCore.cs:340 — Iron Aegis shield absorb.
- GameCore.cs:276 — free heart-demon / clear trib on death.
- Spawner.cs:7,:8,:9,:13,:28,:197,:207 — gate (done), lightning/tribulation hazards, aerial parity, dash pass-through, aerial telegraph.
- SwordFlight.cs:77,:78,:79,:86 — end-slide signal + mount/teardown FX hooks.
- PowerUps full behavior (magnet/double/dash/surge) across Spawner + PlayerRunner.

## Batch 4 — Animation + asset import  ⬜  (HITL, biggest visible leap)
- CharacterModel.cs:77 — warrior **Animator state machine** (Idle/Running/Jump/Slide/Slash/Death synced to PlayerRunner).
- Spawner.cs:11,:12 — **ninja-zombie GLB** enemy mesh + bob/sway (replaces cubes; pooled-spawner change).
- Props + skyboxes import (torii/lantern/pine/tree/rock .glb, sky_forest/sky_night .hdr).
- Depends on the glTF import compiling first (glTFast package resolve).

## Batch 5 — Art / VFX polish pass  ⬜  (HITL, consolidated)
- PlayerRunner.cs:100,:157,:183,:212,:264,:300 — Dread aura, sword-mount, Qi-Leap, glide, slash arc, absorb flash.
- TelegraphSystem.cs:6,:8,:101,:290 — brush-crescent for Low plane, glow second-pass (bloom), true brush/seal **TMP font** (real calligraphy).
- Gate.cs:6,:7,:131 (translucent talisman via URP transparent), Foe.cs:8, OrbPickup.cs:6, PillPickup.cs:6, Survivability.cs:95 — glow/aura/anim polish.
- MainMenu.cs:7 — wordmark art, parchment textures.

## Batch 6 — Device-ship items  ⬜  (HITL, with Phase 5)
- HudOverlay.cs:16,:113 — full `Screen.safeArea` (notch/home-indicator).
- iOS ship checklist (signing, ASTC, portrait, parity pass) — see SHIP_iOS.md.

---

## Sequencing
0 → 1 (free + cheap AFK, real gaps) → then choose 3 (gameplay) / 2 (screens) / 4 (assets+anim).
Device (6) last. Re-run this scan after each batch and update the status boxes.
