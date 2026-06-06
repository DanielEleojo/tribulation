# Tribulation — Rework: The Cultivator's Road

## New premise
You are a **cultivator** walking the endless, merciless **road of cultivation (修行路)**. The run
*is* the dao: there is no finish line, only higher and harder stretches of road and the constant
threat of falling. You ascend realm by realm — and **the way you travel changes as you cultivate**:
the mortal *runs and stumbles*; the immortal *treads the void on a flying sword*. Hardship is the
content — beasts, rival cultivators, inner demons, qi deviation, and finally Heavenly Tribulation.

**Endless-runner spine kept:** auto-forward, 3 lanes, dodge/act, collect, combo, escalate, "one more
run," best-distance chase. **The twist that makes it ours:** the *verbs and the world evolve per realm*.

## System reframes (current -> cultivation)
| Now | Becomes |
|---|---|
| Demon fleeing | **Cultivator ascending** the road |
| Heavenly Net (closing formation) | **Tribulation gathering / Qi-Deviation** closing in if you stall |
| Sect disciples | **Rival cultivators · demonic beasts · inner demons** (by realm) |
| Slash | **Sword-qi** strike |
| Spirit Orbs | **Spirit Qi motes** (gather to cultivate) |
| Demon Souls (currency) | **Spirit Stones** |
| Qi meter / Qi Burst | **Qi / a cultivation technique** (e.g. Sword Domain) |
| Life/Death Gate | **Karma Gate** — a dao/heart-demon choice |
| Combo | **Dao Heart** (flawless streak = deeper insight, multiplier; breaks on a blow) |
| Realms (Mortal Husk..Dread Form) | **Cultivation stages** (below) |
| Distance "li" | li walked on the road (kept) |
| Dread Form apex | **Ascension / Tribulation** — radiant, not demonic |

## Cultivation stages + EVOLVING playstyle (the headline)
Each realm enables new verbs and shifts the threats/world. Driven by a per-realm **playstyle profile**.
| Realm | Feel | New verb / change | Threats | World |
|---|---|---|---|---|
| 1 Qi Condensation 炼气 | mortal, fragile, slow | base run / jump / slide | rocks, beasts, fatigue | mortal forest/village |
| 2 Foundation 筑基 | steadier, faster | **Qi Dash** (burst across a gap / through a foe) | bandits, traps | mountains |
| 3 Golden Core 金丹 | empowered | **Sword-qi slash** + Qi meter | rival cultivators | sect peaks |
| 4 Nascent Soul 元婴 | airborne moments | **Glide / brief levitation** (hold to float over hazards) | aerial beasts | cloud cliffs |
| 5 Spirit Severing 化神 | flight | **Sword-flight (御剑)** segments — lane-dodge in the air | sky-tribulations | spirit realm |
| 6 Ascension 渡劫 | transcendent | **Heavenly Tribulation** gauntlet; time-dilation technique | lightning, heart-demon | heavenly void |
Implementation: a `playstyle` dict per realm `{speed, abilities:[...], hazard_set, world, aura}`. The
game enables only the abilities the current realm grants; spawner picks that realm's hazard set; the
world theme + player aura swap. So minute-1 (running, dodging rocks) genuinely differs from late-game
(sword-flight through lightning).

## Reusable vs new (meticulous, no wasted work)
**Reuse (re-framed):** lane runner, jump/slide/slash, Spirit Orbs, combo, the daoist closing formation,
realm/breakthrough system, environment themes, glass HUD, spawner, gates, save/best, juice/VFX.
**New:** per-realm playstyle profiles + ability gating; the new verbs (dash, glide, sword-flight,
tribulation gauntlet); realm-specific hazard sets (ground→aerial→lightning); evolving player aura/visual;
all the renaming/UX.

## Phased rework plan (each: build -> headless test -> commit -> devlog)
- **R1 Premise re-theme** *(now)* — rename/recolor realms, foe ranks, HUD (Spirit Stones, Dao Heart,
  Qi Deviation), apex (Ascension, radiant). Pure reframe; no mechanics change.
- **R2 Playstyle-profile framework** — data table per realm; game/spawner read it; abilities gated by realm
  (slash only from Golden Core, etc.). Refactor so the move-set is realm-driven.
- **R3 Evolving verbs** — Qi Dash (R2 realm), Glide/levitation (Nascent Soul), Sword-flight aerial
  segments (Spirit Severing), Tribulation-lightning gauntlet (Ascension). One increment each.
- **R4 Realm hazards + world + aura** — ground beasts/rocks → aerial → heavenly lightning per realm;
  environment + player aura evolve per stage; reskin enemies as rival cultivators / beasts / inner demons.
- **R5 Runner systems** (from IMPLEMENTATION_PLAN, re-themed) — pills/talismans (power-ups), cultivation
  trials (missions), persistent cultivation (meta progression + save), tribulation boss, audio.
- **R6 Feel-test + balance** — tune the curve so each realm feels like a distinct, harder stretch.

## Cross-cutting
Config in `data/` (realms.json playstyle, hazards, upgrades). All effects route through `game.gd` signals.
House style stays primitive/glowing-qi until an art pass. The closing formation art already fits a
heavenly tribulation perfectly.
