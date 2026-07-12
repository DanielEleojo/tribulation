# Shipping Tribulation to the App Store (Godot 4.6.3 → Xcode → App Store)

This is the hand-off runbook for taking the Linux-developed project to a Mac and
shipping it. The Linux side is already prepped (see "What's already done"); the
remaining steps **must** happen on macOS because iOS code-signing, the iOS export
templates, and Xcode only run there.

---

## What's already done (on Linux)

- **App identity:** name `Tribulation`, `config/version = 1.0.0`, description set.
- **App icon:** opaque 1024×1024 `assets/icon.png` (no alpha, no pre-rounded
  corners — App-Store-valid), wired as `config/icon`. Regenerate with
  `python3 tools/gen_icon.py`.
- **Mobile renderer + ASTC** texture compression (`import_etc2_astc=true`).
- **Portrait lock** (`display/window/handheld/orientation=1`).
- **Vallicade Games splash** — the iOS launch storyboard uses
  `assets/Vallicade_Games_Splash_screen.png` (Scale-to-Fill), which flows into a 2s
  in-game splash scene (`splash.tscn`, `main_scene`) that fades into the title. The
  launch storyboard is baked at export time, so **re-export from Godot** after changing it.
- **iOS export preset** (`export_presets.cfg`): bundle id
  `com.vellicade.tribulation`, version 1.0.0 / build 1, min iOS 13, iPhone & iPad,
  launch storyboard on, tracking disabled, and an `exclude_filter` that strips
  ~43 MB of dead weight (disabled `Models/`, the unused net image, docs, tools)
  from the build.
- **Ads now included (Unity LevelPlay/ironSource), configured non-personalized** —
  an interstitial shows roughly every 3rd death/restart, plus a rewarded "watch ad
  to revive" option on the death card. No IAP. No ATT prompt and no IDFA use:
  `LevelPlayPrivacySettings.SetGDPRConsent(false)` + `SetCCPA(true)` are set before
  SDK init, so ads are non-personalized only — that's the actual reason tracking
  stays off, not the absence of ads. The only other persistence is a local save
  file (`user://tribulation.cfg`).
  **TODO:** re-verify the App Store Connect Data Collection questionnaire (see §7)
  — Apple's privacy-label rules require disclosing data an ad SDK collects (e.g.
  "Advertising Data") even when ads are non-personalized; don't assume the old
  "No" answer still holds without checking LevelPlay's documented data collection.

> Signing fields in the preset are intentionally **blank** — fill them on the Mac.

---

## 0. Prerequisites (one-time)

1. **Apple Developer Program** membership ($99/yr) — https://developer.apple.com/programs/
2. A **Mac** with the latest **Xcode** (from the App Store) + run `xcode-select --install`.
3. **Godot 4.6.3** (stable) for macOS — must match the version used on Linux.
4. **iOS export templates** for 4.6.3: in Godot → *Editor → Manage Export Templates → Download*.
5. (Recommended) **CocoaPods**: `sudo gem install cocoapods` — Godot's iOS export uses it.

---

## 1. Apple-side setup (one-time per app)

1. **App ID / Bundle ID:** In the Developer portal (or let Xcode auto-create),
   register `com.vellicade.tribulation` (or your own reverse-DNS id — change it in
   the preset *and* App Store Connect to match).
2. **App Store Connect record:** https://appstoreconnect.apple.com → *My Apps → +* →
   New App. Pick the bundle id, name "Tribulation", primary language, SKU.
3. **Signing:** Easiest path is **Automatic signing** in Xcode (just pick your Team).
   No manual certs/profiles needed for a first release.

---

## 2. Get the project onto the Mac

```bash
git clone <your-private-repo-url> tribulation
cd tribulation
```

Open it in Godot 4.6.3 (macOS), let it import, and press Play once to confirm it
runs and the icon/splash look right.

---

## 3. Finalize the iOS preset (in the Godot editor on Mac)

*Project → Export → iOS*:

- **App Store Team ID:** set to your 10-char Team ID (Developer portal → Membership).
- **Bundle Identifier:** confirm it matches the App Store Connect record.
- **Targeted Device Family:** confirm "iPhone & iPad" (or set "iPhone" only).
- **Version / Short Version:** `1`/`1.0.0` for the first build (bump per release, see §7).
- **Min iOS Version:** 13.0 is fine; raise if you want.
- Confirm **Privacy → Tracking Enabled = off** and no usage descriptions are needed
  (the game uses no camera/mic/photos/location).

---

## 4. Export the Xcode project

In the iOS export dialog → **Export Project** (not "Export PCK/Zip") → choose an
empty folder, e.g. `build/ios/`. Godot generates a full Xcode project there
(`Tribulation.xcodeproj`) including the `Assets.xcassets` AppIcon set, the launch
storyboard, and a generated `PrivacyInfo.xcprivacy`.

CLI alternative:
```bash
godot --headless --export-release "iOS" build/ios/Tribulation.xcodeproj
```

---

## 5. Build & sign in Xcode

1. `open build/ios/Tribulation.xcodeproj`.
2. Select the **Tribulation** target → *Signing & Capabilities*:
   - Check **Automatically manage signing**, pick your **Team**.
3. Verify in *General* / Info:
   - **Deployment target** ≥ 13.0.
   - **Device Orientation** = Portrait only (Godot sets this; double-check).
   - **App Icons** show the jade sword (Assets.xcassets → AppIcon, all slots filled).
   - `PrivacyInfo.xcprivacy` is in the target (required-reason APIs declared by Godot).
4. Plug in an iPhone, pick it as the run destination, **⌘R** — smoke-test on device:
   portrait, touch swipes (lane/jump/slide), tap-to-slash, menu/settings/pause,
   audio, save persistence across relaunch.

---

## 6. Archive & upload

1. Set the run destination to **Any iOS Device (arm64)**.
2. *Product → Archive*. When it finishes, the Organizer opens.
3. **Distribute App → App Store Connect → Upload**. Let Xcode handle signing.
4. Wait for processing in App Store Connect (a few–30 min), then the build appears
   under **TestFlight** (test it there first) and is selectable for a release.

---

## 7. App Store Connect: metadata & submit

Fill in the app record before submitting for review:

- **Screenshots:** required for 6.7" iPhone (e.g. 1290×2796) and, if you ship iPad,
  12.9" iPad. Capture portrait gameplay from the simulator/device.
- **Description, keywords, promo text, support URL, marketing URL.**
- **Age rating:** answer the questionnaire (likely 9+ for mild fantasy violence —
  glowing sword combat, no blood/gore).
- **Privacy → Data Collection:** ads (Unity LevelPlay/ironSource) are configured
  non-personalized — no IDFA use (`SetGDPRConsent(false)` + `SetCCPA(true)` set
  before init), which is why no ATT prompt is needed, independent of ads existing.
  **TODO: verify the Data Collection answer directly in App Store Connect** — Apple
  requires disclosing data an SDK collects (e.g. "Advertising Data" /
  "Identifiers") even for non-personalized ads, so don't assume "No, we do not
  collect data" still applies without checking LevelPlay's privacy documentation.
- **Pricing:** Free (or set a tier).
- **Export Compliance:** uses no non-exempt encryption → answer "No".
- Select the uploaded build, then **Submit for Review**.

---

## 8. Releasing updates (each new version)

1. Bump in `project.godot` → `config/version` and in the export preset →
   `application/short_version` (marketing, e.g. 1.0.1) and **always** raise
   `application/version` (build number, e.g. 2) — App Store rejects duplicate builds.
2. Re-export → Archive → Upload → submit the new build.

---

## Gotchas / checklist

- [ ] Godot version on Mac **exactly** matches (4.6.3) — mismatched export templates fail.
- [ ] App icon has **no alpha / no transparency** (already true for `assets/icon.png`).
- [ ] Build number incremented for every upload.
- [ ] Bundle id identical across preset ↔ App ID ↔ App Store Connect.
- [ ] Tested on a **real device** (simulator can't validate touch feel / performance).
- [ ] `PrivacyInfo.xcprivacy` present (Godot generates it; Apple now requires it).
- [ ] No `Models/` or dev files in the build (handled by `exclude_filter`).

*(Supersedes the Phase-0 `Tribulation_Phase0_iOS_Runbook_v1.0.md`, which only covered
getting an empty project onto a device.)*
