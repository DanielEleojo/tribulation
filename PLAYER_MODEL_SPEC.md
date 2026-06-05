# Tribulation — Player Model Spec, SketchUp build (the Demonic Cultivator)

Base form **"Mortal Husk"** — a hunted, ragged demon-path swordsman. Built ONCE here; later
cultivation realms (to Dread Form) are layered in-engine via materials, not remodeled. SketchUp's
flat-faceted look is the intended stylization — keep it low-poly and crisp.

> ⚠️ SketchUp reality: it's a surface modeler, not an organic sculptor. For Mixamo to auto-rig it,
> the body must end up as **one watertight solid**, **faces oriented front-side-out** (no blue
> back-faces), and **exported as FBX/glTF**. So: model in Components, validate with **Solid Inspector²**,
> **Orient Faces**, and export with a glTF/FBX extension. Expect light cleanup.

---

## 0. SETUP (do this first)
- **Window ▸ Model Info ▸ Units:** Millimeters, precision 1 mm.
- **Axes:** red = X (side), green = Y (front/forward), **blue = Z (up)**. Build the figure **standing on
  the red/green ground plane**, blue pointing up, **facing the green +Y axis** (that's "front").
- **Origin:** place the model so SketchUp's origin sits **between the heels on the ground** (drag the
  Axes tool there if needed).
- **Pose: full T-pose** — arms straight out along the **red axis**, palms down, legs straight, feet a
  shoulder-width apart pointing green. Symmetric.
- **Total height: 1850 mm.** Work in **Components** (not loose geometry) so symmetry mirrors cleanly.
- Tools you'll live in: Rectangle **R**, Push/Pull **P**, Line **L**, Circle **C**, Move **M**,
  Rotate **Q**, Scale **S**, Offset **F**, **Follow Me**, Eraser+Ctrl to soften, **Make Component G**,
  **Flip Along** to mirror, Paint Bucket **B**.

## 1. PROPORTIONS (mm — heroic-feral, ~7.5 heads)
| Part | Height/Length | Width | Depth | Notes |
|---|---|---|---|---|
| Head | 240 | 170 | 200 | tall gaunt skull |
| Neck | 75 | Ø110 | — | corded |
| Torso (chest→waist) | 520 | 480 → 330 | 250 | V-taper to waist |
| Pelvis/hips | 200 | 360 | 230 | |
| Upper arm | 320 | Ø110 | — | horizontal (T-pose) |
| Forearm | 290 | Ø95 → 80 | — | taper to wrist |
| Hand | 190 | 100 | 45 | R hand closed (grip) |
| Thigh | 470 | Ø175 → 130 | — | |
| Shin | 430 | Ø120 → 80 | — | taper to ankle |
| Foot (barefoot) | 80 tall | 110 | 270 | toes forward |

## 2. BODY — build it part by part
- **Torso:** draw a 480×250 mm **Rectangle** on the ground, **Push/Pull** up 520. Select the top face,
  **Scale** it to ~0.7 (→ ~330 wide) to carve the V-taper to the waist. Pull the chest face forward a
  touch for the pecs; **Move** the lower edges in for the cinched waist. Soften the vertical corners.
  Carve the **eight-pack** by drawing shallow rectangles on the belly face and Push/Pulling them in
  ~6 mm. Lean, cut musculature — wiry, not bulky.
- **Pelvis:** box 360×230×200 under the torso; soften; angle the front for the hip line.
- **Limbs (use Follow Me for clean tapers):** draw a **Circle** profile (the limb's start diameter) and
  a straight **Line** path of the limb's length; **Follow Me** to extrude a cylinder; select the end
  face and **Scale** to taper (e.g., forearm Ø95→80). Make each limb its own **Component**.
  - Model **one left arm** (upper + fore + hand) and **one left leg** (thigh + shin + foot) as
    Components, then **copy + Flip Along red** to make the right side — perfect mirror, edit-once.
  - Arms exit the shoulders **horizontally along red** (T-pose), palms down.
- **Hands:** block the palm (190×100×45), Push/Pull four short finger stubs + a thumb; **right hand
  closed** into a fist-grip (leave a Ø35 hole through the grip for the sword); **left hand open**, fingers
  taut and slightly spread.
- **Feet:** box 270×110×80, soften the toe edge, taper the heel. **Barefoot** — suggest toes with two
  shallow Push/Pull grooves.
- **Neck + Head:** cylinder neck; head = a 170×200×240 box, Push/Pull the brow forward, scale the jaw
  in for **gaunt hollow cheeks**, notch the eye sockets (Push/Pull in ~10 mm), a sharp nose ridge.
  High cheekbones, strong jaw, cold fixed-forward stare.

## 3. SURFACE DETAIL (the demon, latent)
- **Old sword scars:** draw thin lines on the chest (one long diagonal), left shoulder, forearms,
  right ribs; **Push/Pull** them in 2–3 mm so they catch light as scars.
- **Latent qi-cracks:** faint hairline grooves at the spine base, collarbones, knuckles — model as
  thin recessed lines; assign them their **own material** ("QiVein") so the engine can make them glow
  crimson at higher realms. Keep them dim/dark now.
- Faint raised **veins** on the forearms/abs — a couple of soft ridges.
- **Soften/Smooth** all the body edges (Eraser **+Ctrl**, or Soften Edges panel, angle ~30–35°,
  "Smooth normals" + "Soften coplanar") so the faceting reads as a clean stylized form, not boxy.

## 4. HAIR — separate Component "Hair"
Chunky stylized **locks**: draw a few tapered fin shapes (Line + Push/Pull ~15 mm thick), draping from
the scalp to ~600 mm past the shoulder blades, a few strands across the brow. Overlap 6–8 locks. Keep
it one **Component** (separate from the body) so a bone-chain can sway it later. Material "Hair" (near-black).

## 5. CAPE — separate Component "Cape"
A **draped panel** ~520 wide × 800 long hanging off the shoulders to mid-thigh: draw the top edge, use
the **Line/Arc** tools for soft vertical folds, Push/Pull 8 mm thick. **Tear the hem** into uneven
tongues (Line + Eraser). **Divide it into ~6 horizontal segments** (so it can become the writhing
shadow-tendrils at Dread Form). Separate Component, attached at the collar, **arms left free**.

## 6. SWORD — separate Component "Sword" (in the right fist)
A **jian**: blade 900×45×8 mm (Rectangle + Push/Pull; taper the last 120 mm to a point by Moving the tip
edges together); round **guard** 110×30; **grip** Ø35 × 200 (Follow Me a circle along a line); small
pommel + a frayed **tassel** (a few thin strips). Slide it through the right-hand grip hole. **Separate
Component** so you parent it to the right-hand bone after Mixamo rigs the body. Give the **blade edge its
own material** ("BladeEdge") so it can blacken/glow later.

## 7. MATERIALS (Paint Bucket — name them; PBR comes in-engine)
Create + name distinct SketchUp materials so each maps to an engine slot:
`Skin`, `QiVein`, `Eyes`, `Hair`, `Pants`, `Wraps`, `Sash`, `Cape`, `Sword`, `BladeEdge`.
- **Skin** pale cool dust-grey · **Hair/Sash/Cape** charcoal-black `#14121a` · **Pants** dirtied
  off-white→grey `#cdc9bf` with mud/blood hems · **Wraps** dark cloth · **Sword** steel `#b8bcc6`,
  grip dark `#2a2622` · **QiVein/Eyes** deep crimson `#8a0d18` (these stay **dim now** — the engine
  drives the glow). Flat colors are fine; **no painted shadows/AO**.
- Clothing to add after the body: **loose torn martial pants** (box the legs, Push/Pull baggy folds,
  rip one knee), a **double-wound waist sash** with hanging ends, **half-unraveled forearm wraps**
  trailing a loose strip, optional **broken prayer-bead bracelet** (small spheres, a few missing).

## 8. MAKE IT RIGGABLE + EXPORT
1. **Outliner:** body parts → fuse into ONE solid. With SketchUp **Pro Solid Tools**, **Union** the
   torso/pelvis/limbs/head/neck into a single solid Component `Player_Body`. Keep `Hair`, `Cape`,
   `Sword` as their own Components (do NOT union them in).
2. Run **Solid Inspector²** on `Player_Body` → fix until "solid" (no holes, no internal faces).
3. **Orient Faces** (right-click ▸ Orient Faces) so all **white front-faces point outward** (no blue).
4. **Triangulate** on export; **Export ▸ 3D Model ▸ .fbx** (or use a **glTF exporter** extension).
   Confirm: T-pose, **~1850 mm tall**, origin at the heels, transforms clean.

## 9. DO NOT
No action pose (T-pose only). Don't union Hair/Cape/Sword into the body. Don't close the armpits or
cross fingers. No baked lighting in materials. **Don't model the Dread Form** (grey skin, glowing eyes,
tendrils) — that's an in-engine overlay; base = ragged, dangerous, still-human Mortal Husk.

---

### After export → Mixamo (auto-rig → Running/Jump/Slide/Sword-Slash/Death/Idle) → re-attach sword to
the right-hand bone + add cape/hair sway → export one `.glb` with all actions → hand it over and I wire
it into the player slot (scale-normalized to ~2 units, oriented to our −Z forward, clips mapped).
