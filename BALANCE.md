# Tribulation — Balance & Feel (R6)

The road of cultivation is meant to be **long and bitter**: reaching Ascension is a
weeks-to-months goal, not a weekend. This document records the tuning model, the
measured rates behind it, and the knobs to turn after feel-testing.

## The core idea: survival is the wall, not grind-rate

Cultivation advances per **attempt**: you must fill `REALM_SPAN[realm]` of *Qi
progress this life* to summon a Heavenly Tribulation, then survive it to break
through. Death keeps your **major realm** but resets the layer climb. So each realm
is a skill wall — you must put together a single run good enough to fill its span
before the road kills you.

The single most important fix in R6: **the difficulty no longer plateaus.** Before,
speed capped at 22 and spawn interval floored at 0.7s by ~60s, so a competent player
could farm forever and reach Ascension in a few long runs. Now the road keeps
crowding in until a brutal (but bounded, so it stays controllable) soft-cap, so every
run eventually ends.

## Difficulty curve (per run)

| Knob | Where | Start | Ramp | End (soft cap) |
|------|-------|-------|------|----------------|
| Forward speed | `player.gd` `base_speed`/`max_speed`/`speed_ramp_time` then `speed_creep`(0.07)/`speed_creep_cap`(16) | 12 | →22 by 90s | creeps to **38** by ~5 min |
| Spawn interval | `spawner.gd` `start_interval`/`min_interval`/`ramp_time` then `endless_ramp`(200)/`hard_min_interval`(0.42) | 1.4s | →0.7s by 60s | tightens to **0.42s** by ~4.3 min |
| Foe rank | `game.gd` `TIER_DIST` | Mortal | rises with li | Nascent Soul by 1303 li |

**Per-realm difficulty offset** (`DIFFICULTY_PER_REALM = 12s`): higher realms begin
that many seconds *deeper* into the curve, so Golden Core opens at ~24s of ramp,
Spirit Severing at ~48s (near max speed immediately). Low realms stay gentle; each
ascent is harder from the first step and can't be cheesed at a trivial difficulty.

## Economy

Qi progress (`run_progress`) comes from **orbs** (`on_orb_collected`) and **kills**
(`on_enemy_killed`), both scaled by the Dao-Heart combo (×1 → ×5 cap, +0.1/streak)
and the `spirit_gathering` shop upgrade (+8%/level, up to +80%).

- Slash/kills unlock at **Golden Core (realm 2)**. Realms 0–1 are orbs-only — so
  there gathering Qi also gently eases the Heavenly Net (`-0.012`/orb), since you
  have no kills to push it back.
- Measured (headless god-mode, magnet on = near-full collection, combo at cap, no
  upgrades) — an **upper bound** on orb income: ~4–8 Qi/s. A real run (partial
  collection, combo broken by hits) is roughly 25–40% of that.

## REALM_SPAN — the climb

`[50, 120, 300, 750, 1800, 999999]` (last = Ascension, terminal).

- **Qi Condensation → Foundation (50):** ~20–40s. First-session hook; teaches dodge.
- **Foundation → Golden Core (120):** ~1–1.5 min. Qi Leap (double jump) in hand.
- **Golden Core → Nascent Soul (300):** a few runs. Slash + Qi Burst now help.
- **Nascent Soul → Spirit Severing (750):** a strong multi-minute run / many attempts.
- **Spirit Severing → Ascension (1800):** the long grind — needs maxed upgrades **and**
  a near-perfect survival deep into the soft-cap. Intended as weeks-to-months.

## Knobs to turn after feel-testing

- Too easy / farmable late → raise `speed_creep`, lower `hard_min_interval`, or
  *remove* `speed_creep_cap` (watch for collision tunneling above ~60 u/s).
- Top realms impossible → lower `REALM_SPAN[3..4]` or raise `spirit_gathering` gain.
- Each realm not distinct enough → raise `DIFFICULTY_PER_REALM`.
- Early game too punishing → lower `start_interval`/`base_speed` or `REALM_SPAN[0..1]`.
