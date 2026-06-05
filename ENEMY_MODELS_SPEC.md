# Tribulation — Enemy Model Specs (the Righteous Sect Hunters)

Six varied martial-artist archetypes that pursue the demon. Each is its own generated model,
all share the rig/export rules below. They escalate by rank in-engine (Third-rate → Transcendent)
via an **emissive "rank-qi" accent mask** (weapon edge + eyes) that the engine tints — so build them
at a neutral base and let the engine push the glow/color.

---

## SHARED CONSTRAINTS (apply to ALL six — same as the player)
- **One watertight, manifold humanoid body mesh** (head/torso/arms/hands/legs/feet joined).
- **Clean T-pose:** arms horizontal, palms down, fingers slightly spread, legs straight, feet
  shoulder-width, toes forward, symmetric.
- **+Y up, feet at Y=0, origin between heels, facing +Z. Apply all transforms.**
- **~8k–16k tris** each (many on screen — leaner than the player). Single UV set, no overlaps.
- **Weapon = SEPARATE mesh** in the gripping hand (parent to the hand bone after Mixamo auto-rig).
  Long hair / loose sleeves / sashes that should sway = separate low-poly meshes too.
- **Material slots:** body/skin · robe/garment · armor (if any) · hair · **weapon** · **eyes**, plus a
  **"rank-qi" emissive accent mask** on the weapon edge + eyes (default ~off; engine drives color/energy
  per tier: grey→blue→green→violet→gold).
- **No baked lighting/AO in albedo.** Export glTF (.glb)/FBX, T-pose, Y-up.
- **Vary the heights/builds** (listed per archetype) for silhouette variety — Mixamo handles scale,
  but model the relative proportions in.

---

## 1. OUTER DISCIPLE — Swordsman  *(the common grunt; slash/dodge)*
**Sect:** Verdant Sword Sect.  **Build:** young, lean, ~1.75 m, average.
A fresh-faced cultivator: clean **blue-and-white martial robe** (crossed collar, knee-length, split
for movement), a simple cloth belt, dark trousers, cloth shoes. **Topknot** with a wooden pin; a few
loose strands. Earnest, determined face — barely more than a teenager. **Weapon:** a plain **jian**
(straight sword) held forward in a basic guard. Minimal ornament. *Reads as:* the disposable many.
**Palette:** sky-blue + off-white robe, dark trim, pale wood pin, steel blade.

## 2. SPEAR SENTINEL — Lane-holder  *(blocks/owns a lane; thrusts)*
**Sect:** Iron Spear Garrison.  **Build:** tall, broad, disciplined, ~1.9 m.
Partial **lamellar armor** — chest plate, shoulder guards, bracers, a tasset over a deep-crimson
gambeson; greaves on the shins. Stern, square-jawed, a **horsetail helm crest** or a cloth headband.
**Weapon:** a long **qiang (spear)** ~2.2 m with a leaf blade and a **red horsehair tassel** below the
head, gripped two-handed. *Reads as:* a wall you cannot push through. **Palette:** iron-grey lamellar,
crimson cloth, brass studs, crimson tassel.

## 3. TALISMAN ADEPT — Ranged caster  *(throws charms/projectiles; dodge or slash to deflect)*
**Sect:** Talisman & Formation Hall.  **Build:** robed, slight, ~1.75 m, stooped slightly.
A scholar-sorcerer in **layered saffron/ochre robes** with wide sleeves, a **hood** half-up, **bead
necklace**, and a **bandolier of paper talismans** + small scroll cases at the waist. Ink-stained
fingers. **Weapon:** a **peachwood sword** in one hand and a **fan of paper talisman charms** in the
other (the charms are the projectiles he hurls). Faint glyph-glow on the topmost talisman (emissive
accent). *Reads as:* keep moving or get hit from afar. **Palette:** saffron/ochre/brown robes, red-ink
talismans with gold glyphs, dark beads.

## 4. IRON-BODY MONK — Bruiser / tank  *(takes 2 hits; heavy, slow swing)*
**Sect:** Stone Bell Temple.  **Build:** **hulking, ~2.0 m, barrel-chested, thick limbs.**
A warrior monk: **shaved head**, bare muscular torso under a half-draped **kāṣāya sash** across one
shoulder, baggy tied trousers, **thick prayer beads** round neck and wrist, wrapped knuckles/forearms.
Calm, heavy-lidded face; ritual scars/burn dots on the scalp. **Weapon:** either **bare fists** (huge,
wrapped) or a heavy **monk's staff (gùn)** — pick fists for the unarmed bruiser. *Reads as:* a slab of
muscle that shrugs off a strike. **Palette:** earthen ochre/saffron sash, brown trousers, brass beads,
ruddy skin.

## 5. SWORD DANCER — Agile striker  *(fast, small dodge window)*
**Sect:** Falling Petal Pavilion.  **Build:** lithe, fast, ~1.68 m, light frame (female cultivator).
Flowing layered **silk hanfu** — pale jade-green and white, long trailing sleeves and a waist ribbon
that streams behind (separate sway meshes). **Long hair** in a high half-up style with a hairpin.
Sharp, cold-beautiful face. **Weapon:** **dual short swords (shuangjian)** or a single **ribbon-sword**
with a long silk streamer — go dual short swords, one per hand (two separate weapon meshes). *Reads as:*
a blur of silk and steel. **Palette:** jade-green + white silks, gold pin, bright steel.

## 6. SECT ELDER — Sword Saint  *(elite / mini-boss at Peak & Transcendent tiers; rare, deadly)*
**Sect:** Sky-Sword Summit.  **Build:** tall, upright, regal, ~1.85 m, ascetic-lean.
An old master in ornate **white-and-gold Daoist robes** with embroidered cloud/crane patterns, wide
sleeves, a jade belt-pendant, and a **long white beard and brows**, hair in a high crown topknot with
a jade guan. Serene, terrible authority. **Weapon:** a fine **jian** that **floats beside him**
(flying-sword — model it as a separate hovering blade, no hand grip needed) wreathed in qi; carries a
**horsetail whisk (fuchen)** in one hand. Strong **rank-qi emissive** (this one always glows). *Reads
as:* the one you should run from. **Palette:** white/ivory robes, gold embroidery, jade accents,
glowing sword-qi.

---

## Variety summary (so they never feel same-y)
| # | Archetype | Build/Height | Weapon | Role | Base palette |
|---|---|---|---|---|---|
| 1 | Outer Disciple | lean 1.75 | jian | grunt (slash/dodge) | blue-white |
| 2 | Spear Sentinel | broad 1.9 | tasseled spear | lane-holder | iron-grey + crimson |
| 3 | Talisman Adept | slight 1.75 | peachwood sword + talismans | ranged | saffron/ochre |
| 4 | Iron-Body Monk | hulking 2.0 | wrapped fists | tank (2 hits) | earthen ochre |
| 5 | Sword Dancer | lithe 1.68 | dual short swords | agile/fast | jade-green/white |
| 6 | Sect Elder | regal 1.85 | floating flying-sword + whisk | elite/boss | white-gold + jade |

### Pipeline (each model)
Export T-pose `.glb` (Y-up, transforms applied) → Mixamo auto-rig → apply **Running (toward camera),
Attack/strike (with a wind-up), Hit/stagger, Death**, plus Idle → Blender: parent weapon(s) to the
hand bone(s), add sway bones for sleeves/sash/hair → export one `.glb` with all actions →
drop in and I wire them into the disciple spawn slot (scale-normalized, oriented to face the player,
rank-qi tinted by tier).
