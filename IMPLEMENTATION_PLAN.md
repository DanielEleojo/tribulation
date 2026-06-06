# Tribulation — "Best Endless Runner" Implementation Plan (Phase 5+)

Wuxia-first. Every increment: build → headless test → commit → one devlog line. Data-driven
config in `data/` where sensible. New HUD uses the glass system. Order chosen so moment-to-moment
feel lands before retention systems before polish.

Status legend: ✅ done · ▶ next · ⏳ planned.

---

## 5.A — Spirit Orbs + Combo + Best-li  ✅ (done)
Collectible orb trails (+souls/+Qi), combo multiplier (≤5×, breaks on hit/wrong-gate), persistent best "li".

---

## 5.B — Power-Up Talismans (pickups)  ▶
**Fantasy:** rare glowing **talismans** on the track grant a fleeting forbidden art.
**Types (data table `data/powerups.json`: name, glyph, color, duration):**
| Talisman | Effect | Dur |
|---|---|---|
| 摄魂 Soul-Attraction | auto-pulls nearby Spirit Orbs to you | 8s |
| 掠 Sword-Qi Dash | forward invulnerability; touching enemies/hazards slays/plows them (huge combo) | 3s |
| 气 Qi Surge | instantly fills Qi → triggers a Qi Burst | instant |
| 魂 Soul Doubling | ×2 soul gain | 10s |
| 铁 Iron Aegis | +1 temporary Iron-Body charge | until hit |
**Mechanics:** spawner drops ONE talisman occasionally (rarer than orbs), often in the risky lane.
Area3D pickup → `game.activate_powerup(id)`. `game` holds `_active := {id: time_left}`, ticks them in
`_process`, applies/*removes* effects. Player gets a `dash` state (invuln + contact-kill + speed) and a
`magnet` pull (attract `orb`-group nodes within radius each frame).
**Files:** spawner.gd (spawn + Area3D), game.gd (timers/effects/`activate_powerup`), player.gd (dash, magnet pull),
hud.gd (active-talisman chips with radial countdown), data/powerups.json.
**Test:** each activates, effect applies, expires cleanly; magnet pulls orbs; dash plows a disciple.

## 5.E — Orb-guided design + Near-miss  ⏳ (pairs with 5.B)
**Orb placement intent:** trails that **arc over a jump-block**, **curve into the Life-Gate lane**, or sit
**in the risky lane beside a hazard** (risk/reward) — coordinate orb spawns with the hazard just spawned.
**Near-miss (惊险):** detect a hazard passing within a small margin un-hit → bonus combo + Qi + a "险!" spark.
**Files:** spawner.gd (orb/hazard coordination), game.gd or player.gd (near-miss check on despawn).
**Test:** orbs align to hazards; a narrow dodge grants the bonus.

## 5.C — Run Missions / Goals  ⏳
**Fantasy:** the demon's vows — three objectives per run.
**Pool (`data/missions.json`):** "Slay N disciples", "Flee N li", "Devour N Spirit Orbs", "Reach realm X",
"Hold a combo of N". Complete → bonus souls + checkmark; reroll next run.
**Mechanics:** `scripts/missions.gd` node subscribes to game signals (kills/orbs/distance/realm/combo),
tracks 3 active missions, rewards on completion. Glass HUD list (top-left under Qi or a slide-in).
**Files:** missions.gd, hud.gd (mission list), data/missions.json, game.gd (emit the needed signals).
**Test:** progress increments, completes, rewards, rerolls.

## 5.D — Meta Progression / Persistence  ⏳ (the retention spine — 3 sub-steps)
**Save schema (`user://save.cfg`):** `total_souls`, `best_li`, `realm` (persistent cultivation), and an
`upgrades` map. Realm becomes META (grows from *total* souls across sessions, per the PRD months-long climb);
each run you start AT your cultivation realm with its power tier.
- **5.D1 Save manager** — `scripts/save_manager.gd` autoload: load/save souls, best, realm, upgrades.
  Death banks the run's souls into `total_souls`; recompute realm from total vs `_realms` thresholds.
- **5.D2 Cultivation menu** — `scenes/cultivation.tscn` (glass): spend souls on permanent upgrades
  (`data/upgrades.json`): +start shield, longer magnet, +base soul mult, faster Blood Sprint, start realm.
  Reached from the title screen.
- **5.D3 Apply on run start** — player/game read upgrades + start realm at `begin_run`; in-run breakthroughs
  become "advancing toward your next cultivation realm".
**Files:** save_manager.gd (autoload via project.godot), cultivation.tscn + cultivation.gd, data/upgrades.json,
game.gd (bank souls, start realm), player.gd (apply upgrades).
**Test:** souls bank on death; buy upgrade → persists → applies next run; realm carries over.

## 5.F — Set-Piece: Sect Elder Boss-Chase  ⏳
**Fantasy:** at realm milestones a **Sect Elder** (flying-sword master) hunts you — a gauntlet.
**Mechanics:** scripted event (`scripts/boss_event.gd`): the Elder looms; attacks intensify; survive a timer
OR land N slashes on him → big souls + strong Net relief; failure spikes the Net. HUD boss bar + portrait.
**Files:** boss_event.gd, spawner/game integration, hud.gd (boss bar), a procedural Elder figure (our style).
**Test:** event triggers at milestone, win/lose paths resolve.

## 5.G — Audio  ⏳
**Music:** looping **erhu/guqin** track; intensity rises with speed and Net closeness (layer or pitch/tempo).
**SFX:** drop files into `assets/sfx/` (hooks already wired: start/slash/kill/jump/slide/gate_*/burst/death/orb/
breakthrough/powerup). Add `scripts/music.gd`.
**Files:** music.gd, assets/music/, assets/sfx/. **Test:** music loops + ducks; SFX fire.

## 5.H — Leaderboard  ⏳ (post-launch)
Local best already shipped. Later: local top-10, then online (e.g. SilentWolf) once the game ships.

---

## Build order & dependencies
`5.B (+5.E)` → `5.C` → `5.D1 → 5.D2 → 5.D3` → `5.F` → `5.G` → `5.H`.
Power-ups + near-miss sharpen the second-to-second loop first; missions + meta drive retention;
boss + audio add depth/polish; leaderboard last.

## Cross-cutting conventions
- Config JSON in `data/` (powerups, missions, upgrades) so balancing is data, not code.
- All new currencies/effects route through `game.gd` signals so HUD/missions/save stay decoupled.
- Characters/props stay in the **primitive low-poly + glowing-qi** house style (see enemy below).
- Per increment: `godot --headless --fixed-fps 60 ... --quit-after N` verification + commit + devlog.
