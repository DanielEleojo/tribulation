# Tribulation — Murim Design Bible (everything correlated to murim)

**Premise.** You are a **dark martial artist (demonic cultivator)** fleeing a coalition of
righteous sects through their own lands. They do not place inert obstacles — they **attack**.
Every jump, slide, lane-change and slash is **dodging or countering a real technique**.
You harvest the souls of those who get close, cultivate mid-flight, break through realms,
and turn from hunted prey into an apex calamity. The grammar (3 lanes + jump/slide/slash)
is unchanged; only the *fiction and feedback* change.

---

## 1. The defense language (input = a specific martial response)
Every hazard is an **attack on a plane**, and the matching input is the dodge. Each telegraphs
with a colored qi-line so players learn to *read* attacks, not memorize them (per the Critique's
"readable, not just fast" rule).

| Input | Murim meaning | Defends against (plane) | Telegraph color |
|---|---|---|---|
| **Swipe ↑ / Jump** | Leap over | **LOW** ground-level qi/sweeps | amber ground-glow |
| **Swipe ↓ / Slide** | Duck / dive under | **HIGH** head/torso slashes & beams | cyan head-height line |
| **Swipe ←/→ / Lane** | Sidestep / footwork | **LANE-locked** strikes & projectiles | red lane-streak |
| **Tap / Slash** | Cut / deflect | **DESTRUCTIBLE** foes, wards, projectiles | white flash |

## 2. Hazard catalog (martial techniques, by required response)
### LOW — jump
- **Earth-Splitting Sweep** — a crescent of sword-qi skims the ground (full-width).
- **Serpent Chain** — a weighted chain whipped low across the path.
- **Talisman Mine-Line** — a row of ground charms igniting (a minor formation).
- **Root Snare** *(forest)* — gnarled roots lash up from the soil.

### HIGH — slide
- **Heaven-Cleaving Slash** — horizontal blade-qi at neck height (full-width).
- **Spear Wall** — leveled spears thrust across at chest height.
- **War-Banner Array** — a sect banner/rope strung across, humming with qi.
- **Suspended Sword Formation** — flying swords hovering at head height.

### LANE — side-swipe (or slash)
- **Charging Disciple** — a swordsman barrels down your lane.
- **Spear Lancer** — holds one lane with a long thrust.
- **Flying Daggers / Needle Volley** — projectiles locked onto your lane.
- **Qi-Pillar Eruption** — a column of flame/qi bursts in a lane.

### DESTRUCTIBLE — tap/slash (or die)
- **Blocking Disciple** — cut them down for souls.
- **Sealing Ward** — a glowing barrier-talisman wall; shatter it or be stopped (caught by the Net).
- **Deflectable projectile** — slash to bat a dagger/talisman aside.
- **Formation Core** — a glowing array node; sever it to collapse the trap.

### COMBINATIONS (mid/late realms — the difficulty curve)
Two planes at once: a **high slash + a lane volley** → slide *and* be in the safe lane; a
**charging disciple behind a low sweep** → jump, then slash on landing. This is where mastery shows.

## 3. The pursuers — sects with identities (active, telegraphed)
The attacks come from named pursuers, so it reads as a *fight*, not a course:
- **Verdant Sword Sect** *(forest act)* — blade-qi sweeps & slashes (low/high).
- **Iron Spear Garrison** — spear walls & lane lancers.
- **Talisman / Formation Hall** *(sect home turf)* — wards, mine-lines, qi-pillars, the Net itself.
- **(later) Sky-Sword Elders** — suspended sword formations, the hardest combos.

Each act's **environment = that sect's turf**, and its signature attacks dominate there — tying
the world progression directly to the hazard set. Every attacker **winds up** (raises weapon /
qi flares) before striking, on the color-coded plane = fair, learnable, dramatic.

## 4. Existing systems, re-fictionalized (already murim — naming + framing)
- **Heavenly Net → "Sky-Net Suppression Formation."** Closing edges = talisman anchors tightening.
  Slaying disciples shatters anchors (pushes it back). Full closure = sealed and executed.
- **Qi → demonic qi.** Qi Burst = a named **Demonic Art** that scales with realm
  (e.g. *Blood-Qi Eruption* → *Corrupt Sword Domain* → *Calamity Wave*), clearing the field.
- **Demon Souls** — severed souls of slain righteous cultivators; the fuel of forbidden cultivation.
- **Cultivation realms** — breakthroughs mid-flight; power creeps from hunted (Mortal Husk) to
  apex (Dread Form). Each realm unlocks/strengthens a Demonic Art and durability (Iron Demon Body).
- **Life/Death Gate → "Fate Gate."** A fork between the true path and a killing formation; the safe
  gate bears a distinct omen/talisman color (the learnable tell). Wrong = caught in the formation.
- **Distance → "li fled"** / depth driven into the Heavenly Net.

## 5. The narrative arc (world + difficulty as one)
- **Act I — Forest Flight** (Mortal Husk–Blood Awakening): outnumbered, mostly **dodging**; few
  openings to kill. You are prey. Verdant Sword Sect harries you through the woods.
- **Act II — Onto Their Turf** (Sinister Core–Shadow Soul): you push **into** sect grounds —
  more enemies, denser formations, but now strong enough to **cut through**. The hunt turns.
- **Act III — Dread Form & beyond** (Dread Form+): you stop fleeing in fear. World becomes a
  blood hellscape; enemies **hesitate** (Terror Aura); you harvest at will. The Tribulation.

## 6. Redesign map (current placeholder → murim) + build order
| Current | Becomes | Input |
|---|---|---|
| Full-width red block | **Earth-Splitting Sweep** (low qi crescent) | jump |
| Full-width purple bar | **Heaven-Cleaving Slash** (high blade-qi) | slide |
| Enemy wall (capsules) | **Blocking Disciples** (charge + wind-up) | slash/dodge |
| — (new) | **Flying Daggers** (lane projectile) | side-swipe/slash |
| — (new) | **Sealing Ward** (must-slash barrier) | slash |
| Torii gate | **Fate Gate** (omen-coded fork) | lane |

**Suggested build order (each a tested increment, keeps the grammar):**
1. **Telegraph system** — color-coded attack planes (amber low / cyan high / red lane) + wind-up flash. The backbone of "reading attacks."
2. **Re-skin the 3 hazards** into Sweep / Slash / Disciples using the telegraphs.
3. **Lane projectile** (Flying Daggers) — new dodge-or-slash threat.
4. **Combat weight** — disciples charge, wind up, blade-arc on slash, souls fly to counter.
5. **Sect-themed attack sets per act** (forest vs sect turf).
6. **Rename systems in HUD/text** (Sky-Net, named Demonic Arts, Fate Gate, "li").
7. *(stretch)* a visible **Pursuer** behind you launching the attacks.

Nothing here throws away working code: hazards keep their collision/lane/timing logic; we change
meshes, add telegraphs, and add two new hazard types. Difficulty still rides the existing
spawn-ramp + Heavenly Net.
