# Tribulation — Complete Art & Visual Asset Manifest

**Context:** 3D behind-the-back lane runner. Assets fall into 3D models+rigs+anims,
textures/materials, VFX (particles/shaders/trails), and 2D UI. Murim/xianxia dark-demon
theme. v1 scope = cultivation realms 1–6 climaxing at Dread Form.

Legend — Priority: **P1** = needed for the playable vertical slice / next phase,
**P2** = v1 polish, **P3** = later/meta. Source: PACK=asset pack (Quaternius/Kenney/Synty),
MIXAMO=rigged humanoid anims, AI=generative (skybox/textures/UI), DIY=author in Blender/Aseprite/Figma.

---

## A. PLAYER CHARACTER — the demonic cultivator  (replaces: gold box)
1. **Base demon model** — shirtless, muscular, white loose pants, black flowing cape, long black hair; rigged humanoid (Mixamo-compatible skeleton). [P1, PACK/MIXAMO base + DIY]
2. **Weapon: blade/sword model** — held in run/slash; realm variants (normal → blackened → corrupted-energy). [P1]
3. **Cape** — rigged or sim cloth; Dread Form swaps to 6–8 shadow tendrils. [P2]
### Player animations (3D clips)
4. Run cycle (loop) [P1]
5. Jump / launch [P1]
6. Fall / airborne [P1]
7. Slide / dash-roll (low) [P1]
8. Slash / forward strike [P1]
9. Lane-dash Left [P1]
10. Lane-dash Right [P1]
11. Hit / flinch (Iron-Body absorb) [P2]
12. Death [P1]
13. Idle (title/menu) [P2]
14. Levitate / float (realm 10 Tribulation, future) [P3]
### Player realm visual states (mostly material/shader overlays on the SAME run, per PRD)
15. Mortal Husk — ragged, barefoot, dull [P1]
16. Blood Awakening — darkened veins, red eyes (emissive) [P2]
17. Sinister Core — black blade + dark weapon trail [P2]
18. Demon Flesh — darker skin, writhing cape [P2]
19. Shadow Soul — trailing shadow double silhouette [P2]
20. **Dread Form** — ash-grey skin, glowing white pupil-less eyes, crimson glowing veins, cape→tendrils, distortion aura (THE marquee look) [P1]
### Player textures
21. PBR set: skin, pants, cape, hair, blade (albedo/normal/roughness/emission) [P1]
22. Emission masks for veins/eyes/weapon (drive realm glow) [P2]

## B. ENEMIES — sect disciples (righteous hunters)  (replaces: crimson capsule)
23. **Basic swordsman disciple** model — robe + sword, rigged [P1]
24. Spear/polearm disciple (longer reach variant) [P2]
25. Talisman caster (throws charms — ranged) [P2]
26. Armored/shielded disciple (needs 2 hits) [P2]
27. Sect Elder / mini-boss (elite encounter) [P3]
### Enemy animations
28. Run-toward-player [P1]
29. Attack / strike with telegraph wind-up [P1]
30. Death / dissolve [P1]
31. Hit / stagger [P2]
### Enemy projectiles & textures
32. Thrown talisman / paper-charm projectile (model or sprite) [P2]
33. Spirit dart / qi-bolt projectile [P2]
34. PBR texture set per enemy type [P1–P2]

## C. HAZARDS / OBSTACLES
35. **Heavenly Seal** (jump block) — stone stele/pillar with carved glowing rune [P1, replaces stone box]
36. Rune/glyph decal set for seals (variety) [P2]
37. **Formation array** (slide-under bar) — frame + glowing energy-beam plane (shader) [P1, replaces cyan beam]
38. Ground seal / net-trap (swipe-down ground hazard, PRD) [P3]
39. Breakable barrier (slashable wall) [P2]

## D. LIFE / DEATH GATES  (replaces: torii box frame + talisman box)
40. **Torii gate** model (ornate post + lintel) [P1]
41. Life Gate talisman/charm — green, calligraphy, lotus motif (readable "good" tell) [P1]
42. Death Gate talisman/charm — red, skull/blood motif (readable "bad" tell) [P1]
43. Gate energy-curtain texture/shader (green / red) [P1]

## E. ENVIRONMENT / WORLD  (replaces: flat tiles + fog)
44. **Running path/ground** — tiling texture + model (stone road / temple path) [P1]
45. Lane dividers — carved grooves or glowing lines [P1]
46. Path side edges — cliffs / walls / railings [P2]
47. **Skybox** — sky gradient day→dusk→blood-night [P1, AI]
48. **Blood moon** (large backdrop element) [P1]
49. Distant mountain range silhouettes (parallax) [P1]
50. Cloud / mist layers [P2]
### Recycled flyby scenery props
51. Pagodas / temple silhouettes [P2]
52. Sect banners / war flags [P2]
53. Stone lanterns + hanging lanterns [P2]
54. Bamboo / dead trees / cherry-blossom trees [P2]
55. Guardian-lion / Buddha / temple statues [P2]
56. Floating rocks / debris (late realms) [P3]
57. Fluttering torn talismans [P3]
### Per-realm world theming (6 presets: lighting + fog + color grade + ground accent)
58. Realm 1–2 — desaturated earth tones [P1]
59. Realm 3–4 — darker, dust, embers [P2]
60. Realm 5–6 — cold blue/black/red, scorched cracked ground (Dread Form grade) [P1]

## F. VFX — particles / trails / shaders
61. **Slash blade-arc** sweep trail [P1]
62. Slash impact spark [P1, have placeholder]
63. Slash whoosh / wind streak [P2]
64. **Enemy death** dissolve + ink/blood burst [P1, have placeholder puff]
65. **Soul-wisp** that flies from kill to the Souls counter [P1]
66. Soul collect pulse [P2]
67. **Run dust** (realm-progressive: dust→energy→ground-crack bursts) [P1, have placeholder]
68. Jump / land puff [P2]
69. Qi-gain motes streaming into player [P2]
70. **Qi Burst** shockwave ring (+ dark lightning) [P1, have placeholder sphere]
71. Qi-charged aura around player at full Qi [P2]
72. **Heavenly Net** closing-lattice shader (golden grid encroaching) + pulse + closure flash [P1, have placeholder gold edges]
73. **Dread Form transformation** combo: background flicker, vein-light glow, cape→tendril burst, color-temp shift, heat-haze distortion, ground-shatter footsteps [P1, partial placeholder]
74. Realm breakthrough burst / light rays / screen pulse [P2, have flash]
75. Gate pass flash (green/red) + wrong-gate shatter [P2, have flash]
76. Speed lines / wind streaks at high speed [P2]
77. Iron-Body shield ripple / hit flash [P2]
78. Per-realm aura trails (Blood Sprint streak, Shadow Step afterimage, Void dark-energy under feet) [P3]
79. Damage / low-state screen vignette [P2]

## G. UI / HUD — 2D sprites & layout
80. **Logo / title wordmark** — "TRIBULATION" murim calligraphy [P1]
81. Main-menu background art [P2]
82. Distance readout styling (icon optional) [P1]
83. **Demon Souls** icon + counter [P1]
84. **Qi bar** frame + fill (meridian/orb motif) [P1]
85. **Iron Demon Body** shield icon(s) [P1]
86. Realm name plate / scroll banner frame [P1]
87. Breakthrough banner graphic [P2]
88. **Realm portrait** of the demon (per realm; PRD HUD half-face portrait) [P2]
89. Death-screen panel/scroll + stat layout [P1]
90. Buttons: Play, Pause, Resume, Retry, Settings, Sound on/off [P1]
91. "Watch ad to continue" button + Share button [P2]
92. Pause menu panel [P2]
93. Gesture tutorial icons (swipe ↑↓←→, tap) [P1]
94. Cultivation/upgrade screen (realm tree, soul costs, buttons) — Phase 4 meta [P3]
95. Toast / notification frame [P3]
96. Loading screen art [P2]
### Fonts
97. Display/calligraphy font (title + banners) [P1]
98. Clean UI font (HUD/menus; + CJK/Hangul if localizing) [P1]

## H. ICONS / STORE  (meta — mostly captured, not authored)
99. App icon (all required sizes) [P1]
100. Play Store feature graphic / banner [P2]
101. Store screenshots (Dread Form, gate, death) — captured [P2]
102. App preview video (open on Dread Form) — captured [P2]

## I. TEXTURES / MATERIALS (cross-cutting)
103. PBR texture sets per model (albedo/normal/roughness/metallic/emission/AO) [P1–P2]
104. Tiling library: stone, dirt, wood, cloth, rune-energy [P1]
105. Rune / talisman / calligraphy decal sheet [P2]
106. Toon/gradient ramp textures (if stylized shading) [P2]

---

## Minimum art set to make the game look "real" (next phase, P1 only)
Player (model + run/jump/slide/slash/death + Dread Form material), 1 sect-disciple
(model + run/attack/death), Heavenly Seal, formation array, torii + green/red talismans,
ground texture + lane lines, skybox + blood moon + mountains, slash-arc + soul-wisp +
Net-lattice + Dread-Form VFX, and the HUD set (logo, Qi bar, souls, shield, realm plate,
death panel, buttons, gesture icons, 2 fonts).

## Sourcing notes
- **Humanoid anims:** Mixamo (free, retarget run/jump/slash/death to the demon & disciples).
- **3D props/characters:** Quaternius, Kenney, Synty (low-poly packs fit a mobile runner).
- **Skybox / textures / UI flourishes:** AI generation (frame-coherence doesn't matter here — the Critique's safe zone for AI).
- **Cleanup/authoring:** Blender (models/materials), Figma/Krita (UI), Aseprite (any 2D bits).
- **Out of scope here (separate track):** audio/SFX/music — hooks already exist in `sound_manager.gd` (drop files in `assets/sfx/`).
