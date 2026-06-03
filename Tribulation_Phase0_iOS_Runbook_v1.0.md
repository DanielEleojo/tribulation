# Tribulation — Phase 0 Runbook (iOS-first)

**Version 1.0 · June 2026 · Companion to PRD v1.1, Workflow v1.2, Critique v1.0**

**Goal of Phase 0:** prove that an *empty* Godot project builds and runs on your physical iPhone, with Git live — **before** you sink three months into gameplay. That's it. No runner, no slash, no realms. You are de-risking the pipeline, not building the game.

**Why this matters:** the single most dangerous wall in this whole project is "the iOS build doesn't deploy." If that wall exists, you want to hit it in week 1 with an empty app, not in week 19 with a finished game and a deadline.

---

## The setup, in one mental model

You have three machines and they each have one job. Do not blur the lanes.

| Machine | Job | Never |
|---|---|---|
| **CachyOS box** | Daily development — Godot editor + Claude Code write the game | Build for iOS — it can't, no Apple toolchain |
| **Git / GitHub** | The bridge — moves code from Linux to Mac | Hold secrets or large binaries |
| **Mac** | Export, sign, deploy to the iPhone via Xcode | Be your daily dev machine — you prefer CachyOS, keep it there |
| **iPhone** | The test bench — real-device truth | Be trusted to "probably work" without testing |

Code flows one direction in Phase 0: **write on CachyOS → push to GitHub → pull on Mac → export + run on iPhone.**

---

## Part A — CachyOS dev box  *(Claude Code can automate most of this)*

Your Workflow doc v1.2 sections 2.1–2.8 already cover the installs. Condensed and current:

```bash
# Toolchain (paru is your AUR helper)
paru -S godot nodejs npm git imagemagick
npm install -g @anthropic/claude-code   # if not already present

# Confirm everything answers
godot --version && node --version && npm --version && git --version
```

Then, the project itself:

1. **Create the Godot project** `tribulation`. Renderer = **Mobile** (not Forward+ / Compatibility — this is for iOS performance). **Do not** enable C#/.NET; GDScript only.
2. **Folder structure** (from Workflow doc 2.7): `scenes/ scripts/ assets/{sprites,backgrounds,sfx,music} ui/ shaders/ data/` plus `devlog.txt` and `prompts.txt`.
3. **A minimal "it lives" scene** — a `Label` reading *"Tribulation — it lives"* on a dark `ColorRect`. Something you can visually confirm on the phone. This is the ENTIRE app for Phase 0.
4. **Git from commit one:**

```bash
cd ~/tribulation
git init
curl -o .gitignore https://raw.githubusercontent.com/github/gitignore/main/Godot.gitignore
git add . && git commit -m "Phase 0: empty project + structure"
# Create a PRIVATE GitHub repo called 'tribulation', then:
git remote add origin <your-repo-url> && git push -u origin main
```

> Godot project files (`project.godot`, `.tscn`, `.gd`) are plain text, so Claude Code can scaffold all of Part A directly — you just open Godot to eyeball it. Setting the Mobile renderer and the minimal scene are the two things to verify by eye.

---

## Part B — Prerequisites before you touch the Mac  *(you, ~30 min, mostly accounts)*

- **Apple ID:** a **free** Apple ID is enough for Phase 0. You do **NOT** need the $99/year Apple Developer Program yet — that's only for TestFlight and the App Store (Phase 8). Don't pay Apple a cent until you're actually shipping.
- **Xcode** on the Mac (free, via the Mac App Store) + its command-line tools: `xcode-select --install`.
- **Godot** on the Mac — the **same 4.x version** as on your Linux box. Version mismatch = export template errors.
- **Add your Apple ID to Xcode:** Xcode → Settings → Accounts → **+** → sign in. This registers your free **Personal Team** and gives you the **Team ID** you'll need in Godot's export preset.

---

## Part C — Mac: export, sign, deploy  *(you, hands-on GUI — this is the real Phase 0 test)*

1. **Pull the repo** on the Mac: `git clone <your-repo-url>` (or `git pull`).
2. **Install iOS export templates** in Godot: *Editor → Manage Export Templates → Download* (must match your Godot version).
3. **Add the iOS export preset:** *Project → Export → Add… → iOS.* Two fields are **required** or the export errors out:
   - **Bundle Identifier** — e.g. `com.yourname.tribulation` (must be unique, reverse-domain style)
   - **App Store Team ID** — paste the Personal Team ID from Xcode → Settings → Accounts
4. **Export** the project → Godot produces an **Xcode project** (`.xcodeproj`), *not* a finished `.ipa`. This is expected for iOS.
5. **Open the `.xcodeproj` in Xcode.** Go to *Signing & Capabilities* → set **Team = your Personal Team (your Apple ID)** and let Xcode **automatically manage signing**.
6. **Plug in the iPhone** via USB → on the phone, tap **Trust This Computer**. Select the iPhone as the run destination in Xcode's top toolbar.
7. **Press Run (⌘R).** First launch only: the phone shows *"Untrusted Developer"* — go to **Settings → General → VPN & Device Management → trust your developer cert**, then run again.
8. **The app launches on your iPhone** and you see *"Tribulation — it lives."* → **Phase 0 core goal: DONE.**

> ⚠️ **The 7-day clock.** A free Apple ID signs debug builds that **expire after 7 days** — the app stops launching and you re-build from Xcode to renew. One app per bundle ID, no sharing to others. This is fine for solo dev; it only becomes a reason to pay the $99 when you want TestFlight or the store (Phase 8). Don't let the weekly re-sign surprise you.

---

## Part D — The ad-SDK spike  *(stretch — attempt it, but timebox it)*

**Why it's in Phase 0:** ad SDKs add **native iOS dependencies** through a Godot iOS plugin, and native plugins are the #1 place a first iOS build breaks. Better to find that wall now than at Phase 5 with a finished game.

- **Plugin:** use a maintained Godot 4 plugin — **Poing Studios `godot-admob-plugin`** (v4.1+, identical API on Android + iOS, supports rewarded) or **`godot-sdk-integrations/godot-admob`** (the consolidated successor to the cengiz-pz plugin). Either is fine; Poing Studios has the friendlier docs.
- **The spike:** add the plugin, wire one button to show a **rewarded test ad** using **Google's official TEST ad-unit IDs** (never live IDs in dev — you can get your account flagged), export, and confirm a test ad renders on the iPhone.
- **Timebox it to a day.** The must-have for Phase 0 is the empty app on the device (Part C). If the ad plugin fights you, log it in `devlog.txt` and let it slip to "Phase 0.5" — but *attempt* it now so you know what you're dealing with.

---

## Definition of Done — Phase 0

- [ ] Toolchain installed, versions confirmed on CachyOS
- [ ] `tribulation` Godot project: **Mobile** renderer, GDScript only, full folder structure
- [ ] **Private** GitHub repo live, initial push done
- [ ] Empty app **exports on the Mac and launches on your physical iPhone**
- [ ] (Stretch) A rewarded **test** ad renders on the device
- [ ] `devlog.txt` has its first entry

When every box except the stretch is ticked, Phase 0 is complete and you've proven the riskiest part of the entire project. Then — and only then — Phase 1 (the actual running character) begins.

---

## What NOT to do in Phase 0

- **Don't build gameplay.** No movement, no obstacles, no realms. The temptation to "just add a jump" is exactly the scope creep your own anti-patterns warn about.
- **Don't pay Apple $99.** Free provisioning covers Phase 0 through Phase 7.
- **Don't develop on the Mac.** It's your export station. Your dev home is CachyOS — keep the muscle memory there.
- **Don't skip the iPhone test and assume it works.** The whole point of Phase 0 is that "assume it works" is how you lose week 19.

---

*Sources: Godot Engine official iOS export docs (stable/4.5); Apple Developer free-provisioning behaviour (7-day profile, personal team); Poing Studios and godot-sdk-integrations AdMob plugins for Godot 4. Links provided alongside this runbook.*
