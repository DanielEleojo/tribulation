# Tribulation — Phase 0 Runbook (Android-first)

**Version 1.0 · June 2026 · Companion to PRD v1.1, Workflow v1.2, Critique v1.0**

> **This is the primary Phase 0 runbook for v1.** The separate *iOS runbook* is parked for the eventual iOS port (later, on the borrowed Mac) — ignore it for now.

**Goal of Phase 0 (unchanged):** prove an *empty* Godot project builds and runs on a real Android target **before** you write a line of gameplay. The win of going Android-first: every step happens on your CachyOS box. No borrowed hardware, no 7-day signing clock, no waiting on anyone.

---

## The setup, in one mental model

| Machine | Job |
|---|---|
| **CachyOS box** | Everything — dev, build, *and* deploy. This is the whole point of Android-first. |
| **Git / GitHub** | Version control + offsite backup. |
| **Android emulator (now) → cheap Android phone (before launch)** | The test target. |
| *Friend's Mac + your iPhone* | *Parked. They come back for the iOS port after v1 ships.* |

---

## Part A — Project scaffold  *(the Claude Code session already running)*

Already in motion and **platform-agnostic** — the Mobile renderer, folder structure, minimal scene, and git init are identical for Android. Nothing changes here. When it finishes you'll have `~/tribulation` with an empty "it lives" scene under version control.

---

## Part B — Android toolchain prerequisites  *(one-time, on CachyOS)*

1. **JDK 17 — exactly 17, not newer.** Godot 4.x's Android pipeline is tested on JDK 17; JDK 21 breaks Gradle.

```bash
paru -S jdk17-openjdk
sudo archlinux-java set java-17-openjdk
java -version          # must report 17
```

2. **Android SDK + emulator.** Simplest path on CachyOS is Android Studio — it installs the SDK, platform-tools (adb), build-tools, a system image, and the AVD manager in one go:

```bash
paru -S android-studio
# Launch it once; let the setup wizard install the SDK (default path: ~/Android/Sdk)
```

*(Leaner CLI-only alternative if you don't want the IDE: `paru -S android-sdk-cmdline-tools-latest` then use `sdkmanager` to pull platform-tools, build-tools, and a platform. More fiddly — only if you're allergic to Android Studio.)*

3. **Confirm adb sees the world:**

```bash
adb --version
```

---

## Part C — Godot Android configuration  *(one-time)*

1. **Export templates:** Godot → *Editor → Manage Export Templates → Download* (must match your Godot version).
2. **Point Godot at the toolchain:** *Editor → Editor Settings → Export → Android* — set **Java SDK Path** (your JDK 17) and **Android SDK Path** (`~/Android/Sdk`).
3. **Debug keystore** (Android refuses to install an unsigned APK, even debug). Generate one once and register it in Editor Settings so you never think about it again:

```bash
keytool -keyalg RSA -genkeypair -alias androiddebugkey \
  -keypass android -keystore ~/debug.keystore -storepass android \
  -dname "CN=Android Debug,O=Android,C=US" -validity 9999
```

Then set it in *Editor Settings → Export → Android → Debug Keystore* (user `androiddebugkey`, password `android`). With it set globally, the per-project keystore fields can stay blank.

4. **Install the build template:** *Project → Install Android Build Template.*
5. **Add the export preset:** *Project → Export → Add → Android.* Set **Package → Unique Name** to `com.yourname.tribulation` (reverse-domain, must be unique).

---

## Part D — Run "it lives" on a device

**Emulator (now):** open Android Studio → *Device Manager* → create a virtual device (any modern phone profile + a recent system image) → start it.

**Or a physical Android phone (better):** on the phone, *Settings → About → tap Build Number 7×* to unlock Developer Options, enable **USB debugging**, plug in via USB, authorize the prompt.

Then deploy from Godot one of two ways:

- **One-click deploy:** with the emulator running or the phone connected, click the little Android/remote icon in the top-right of the Godot editor. It builds, installs, and launches in one shot.
- **Or make an APK:** *Project → Export → Export Project* → `tribulation.apk`, then `adb install ~/tribulation.apk`.

When **"Tribulation — it lives"** shows on the emulator or phone → **Phase 0 core goal: DONE**, and you did it without touching anyone else's hardware.

> ⚠️ The emulator is fine for "does it launch," but Android emulators are janky for *game-feel* — GPU quirks, and touch is faked with a mouse. Before you're tuning the actual run (Phase 1+), grab a **cheap used Android phone (~$50)**. Instant deploy, real touch, and it's where ad bugs and frame drops actually show up.

---

## Part E — The ad-SDK spike  *(stretch — and easier on Android)*

Same plugin family: **Poing Studios `godot-admob-plugin`** (one API across Android + iOS) or **`godot-sdk-integrations/godot-admob`**. Add it, wire one button to a **rewarded test ad** using **Google's official TEST ad-unit IDs** (never live IDs in dev), deploy, confirm the test ad renders. Android ad integration is generally smoother than iOS, so this is a low-pain way to prove the monetisation pipeline early. Timebox to a day; if it fights you, log it and move on — the empty-app-on-device is the must-have.

---

## Definition of Done — Phase 0 (Android)

- [ ] JDK 17 + Android SDK + adb installed, versions confirmed
- [ ] Godot Editor Settings: Java SDK, Android SDK, and debug keystore configured
- [ ] Android Build Template installed; export preset with a unique package name
- [ ] Empty app **builds and launches on the emulator** (or a physical Android device)
- [ ] (Stretch) a rewarded **test** ad renders on the device
- [ ] Private GitHub repo live; `devlog.txt` has its first entry

Tick everything but the stretch and Phase 0 is complete — and unlike the iOS path, you can complete it today, alone, on your own machine.

---

## What NOT to do in Phase 0

- **No gameplay.** No movement, obstacles, or realms. Pipeline only — the temptation to "just add a jump" is the scope creep your own anti-patterns name.
- **Don't burn days fighting the emulator.** A $50 phone solves it permanently.
- **Don't touch iOS.** That's the post-v1 port; the iOS runbook waits for a focused Mac stretch. One platform at a time.

---

*Sources: Godot Engine official "Exporting for Android" docs; Android Developers "Export Godot projects to Android"; Godot JDK 17 / build-template / debug-keystore requirements. Links provided alongside this runbook.*
