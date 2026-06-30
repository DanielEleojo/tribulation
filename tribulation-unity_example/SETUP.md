# Tribulation — Unity tracer bullet (Phase 0–2)

Goal of this slice: a runnable endless runner (auto-run, 3 lanes, jump, slide, dodge blocks)
to confirm on an iOS device that the framerate problem is gone. **Do not build out further
until the device run looks good** — that's the whole point of the tracer bullet.

This folder holds the parts that can be authored without the Unity Editor: the C# scripts,
the package manifest, and the shared `balance.json`. The Editor generates everything else
(`.meta` files, `ProjectSettings/`, the URP pipeline asset) — don't hand-create those.

## One-time setup

1. **Create the project**: Unity Hub → New → **Universal 3D (URP)** template, current LTS.
2. **Copy these in**: drop this folder's `Assets/` and `Packages/manifest.json` into the new
   project (merge — let the Editor's manifest keep its own URP version if newer). The scripts
   land in `Assets/Scripts/`, `balance.json` in `Assets/Resources/`.
3. **Input**: Player Settings → Player → Other Settings → **Active Input Handling = Both**
   (the tracer uses legacy `UnityEngine.Input`; "Both" keeps it working if URP pulled in the
   new Input System package).
4. **iOS Player Settings** (from the Godot `export_presets.cfg`):
   - Bundle Identifier: `com.vellicade.tribulation`
   - Version `1.0.0`, Build `1`, Minimum iOS `13.0`, target iPhone + iPad
   - Resolution & Presentation → **Default Orientation = Portrait** (lock the others off)
   - Color space Linear; texture compression ASTC

## Run the tracer (in-editor)

1. New empty scene. Create an empty GameObject, name it `Bootstrap`, add the **Bootstrap**
   component. That's it — `Bootstrap.cs` builds player, ground, camera, spawner, light at Play.
2. Press **Play**.
   - Desktop test: A/D or ←/→ change lanes, Space/↑ jump, S/↓ slide. Red blocks kill you;
     console logs distance + "Tap / Space to restart". Space/Enter restarts.
   - The capsule auto-runs forward, speed ramping over ~90s.
3. Save the scene as `Assets/Scenes/Tracer.unity` and add it to Build Settings.

## Build to device (on the Mac)

1. File → Build Settings → iOS → Build → open the generated Xcode project.
2. Sign + run on device per the existing `SHIP_iOS.md` workflow (same Apple account/profile).
3. **Verdict check**: stable framerate while blocks spawn and you dodge. If yes → the engine
   swap is validated, proceed to Phase 3 (full game/spawner/player/UI). If no → the bottleneck
   wasn't the engine; stop and re-diagnose before porting more.

## What's intentionally NOT here yet (Phase 3+)
Powerups, sword-flight, dread form, particles, slash combat, realms/Qi/Heavenly-Net,
the rigged `.glb` models + Animator, the uGUI HUD/menu/shop, and the net-overlay shader.
The tracer uses primitive capsules/cubes on purpose — it tests the engine, not the art.

## Asset import (Phase 1, when you proceed past the gate)
Copy from the Godot repo and let Unity import:
- `Models/PC animation/warrior_wuxia_animated.glb`, `Models/Enemy Animation/ninja_zombie_animated.glb`
  (glTFast handles `.glb`; set rig Humanoid/Generic, confirm Run/Jump/Slide/Slash/Death clips)
- `assets/props/*.glb` + textures, `assets/backgrounds/*.hdr` (Lighting → Environment)
- `assets/music/theme.ogg` (loop in importer), `assets/sfx/*.wav`, `assets/icon.png`, splash PNG
