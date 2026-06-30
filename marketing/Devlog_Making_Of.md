# The Long Road: Making *Tribulation*

*A making-of for a cultivation endless runner where the obstacles fight back. Built solo in Godot 4.6, shipped on iOS. Everything below actually happened — it's pulled straight from the dev log.*

---

## The one idea worth keeping

Most endless runners put walls in front of you. You memorize the track and your thumbs do the rest. I wanted the opposite: a road that *attacks*, where every obstacle is a martial technique and every input is the counter to it. Jump isn't "clear the box," it's "leap the low sweep." Slide isn't "go under the bar," it's "duck the blade-qi at your neck." The whole game had to be **readable, not just fast** — you should learn to read an attack, not grind a layout into muscle memory.

That one sentence survived from the first design notes all the way to the App Store. Almost nothing else did.

## It started in 2D, and that was a mistake I'm glad I made

The first prototype was a flat 2D runner: auto-running character, recycling ground, jump, slide, an obstacle that kills you, a restart. It worked in about a day. It also felt like a hundred other games, and crucially it couldn't express the thing I actually cared about — reading attacks coming *at* you down a road.

So I tore it down and rebuilt it as a 3D behind-the-back lane runner. Three lanes, a chase camera locked to height, a character auto-running into the distance. The 2D version wasn't wasted — it taught me the timing windows for jump and slide that I carried straight into 3D. But the pivot is the first real lesson: **build the cheapest thing that can prove your core idea, then be willing to throw it away the moment it can't.**

## The grammar: three lanes and four verbs

Everything sits on a tiny, fixed control grammar: swipe to change lane, jump, slide, slash. Four verbs, and that's it. The richness doesn't come from more buttons — it comes from layering threats onto those four answers.

Each hazard telegraphs its plane with a color. Amber at ground level means a low sweep: leap it. Cyan at head height means a high slash: slide under it. Red locked to a lane means a lane strike: step aside. White means destructible: slash it or be caught. Once those four reads are in your hands, I can start *stacking* them — a high slash over a lane volley, a charging foe behind a ground sweep — and difficulty becomes "read two planes at once" instead of "react faster." The grammar never grew. The sentences did.

## Game feel before content

Before I added a single new mechanic past the core loop, I spent a whole pass on feel: trauma-based screen shake that scales with the event (a death shakes hard, a kill barely twitches), hitstop that freezes time for a few frames on death and on a Qi Burst, particle bursts on every slash and kill, and a camera FOV that widens with speed while the fog thickens — so acceleration *reads* as faster and tenser even when the number only moved a little.

None of that is content. All of it is why the content feels good. If the core verb doesn't feel great with zero enemies on screen, no amount of features will save it.

## Art with no artist

I don't have an artist. I had to make a 3D game look intentional anyway, so the rule became: **primitives, shaders, and CC0 assets, composed with care.** The player is a caped swordsman built from boxes and a glowing blade, procedurally animated (footfall bounce, forward lean, cape flap, a blade sway that flares mid-air). The Heavenly Net is a shader — a daoist suppression formation of concentric rings, radial spokes, and a bagua core that contracts toward the center as it closes. The skies are CC0 HDRIs from Poly Haven; the roadside trees and lanterns are CC0 props.

The honest detour: I integrated a real rigged Mixamo warrior, fought its baked-in root motion for a full day, got it working — and then reverted to the primitive figure, because it read better against the rest of the procedural world and shipped far lighter. **Killing that darling was the right call**, and it's the kind of thing that only becomes obvious once both versions are running side by side.

## From "fleeing demon" to "the cultivator's road"

The original premise was a demonic cultivator fleeing the righteous sects — fun, but it framed the player as prey running away. The pivot that made the game *mine* was reframing the whole thing as a pilgrimage: **the cultivator's road**, a long and bitter climb through the realms. Qi Condensation, Foundation Establishment, Golden Core, Nascent Soul, Spirit Severing, and at the far end, Ascension.

That reframing unlocked the best design decision in the project: **gate the verbs behind the realms.** A mortal at Qi Condensation can only endure and dodge — slash is locked, the Qi meter is hidden. Reach Golden Core and you can finally cut foes down. Foundation grants a second jump (Qi Leap). Nascent Soul grants a glide (Cloud Tread). Spirit Severing grants sword-flight — you ride a flying sword up into aerial lanes. And Ascension brings the Heavenly Tribulation. The progression doesn't just hand you bigger numbers; it teaches you a new way to move, exactly when you've earned it. The fantasy and the mechanics became the same thing.

## Breakthroughs should hurt

Filling a realm's span doesn't quietly tick you up a level. It *summons* a Heavenly Tribulation: a survival gauntlet of lightning columns with a Heart-Demon looming over the road. Survive it and you ascend. Fail it and you suffer Qi Deviation — you keep your realm, but the layer-climb resets and you walk again.

This is the spine of the whole balance philosophy: **survival is the wall, not grind-rate.** Your major realm is saved forever; the climb within it is earned anew each life. There was a version where a skilled player could farm a plateau forever and reach Ascension in a weekend — I found it, hated it, and removed the plateau. Now the road keeps crowding in: speed keeps creeping, spawns keep tightening, and every run eventually ends. Ascension is deliberately a goal measured in **weeks to months.** The road is supposed to be long and bitter. That's the genre, and it's the point.

## The unglamorous 80%

The core loop was maybe three days. The game took weeks. The difference is everything around it: a daily Qi reward with a streak, randomized Cultivation Trials each run, twelve achievements, a Cultivation Journal of lifetime stats, a permanent upgrade hall (Spirit Gathering, Talisman Mastery, Qi Sea, Iron Body Refining), power-up talismans, a generative ambient soundtrack I rebuilt from scratch when the placeholders grated (a slow-breathing Am9 pad and sparse pentatonic bells, ninety-six seconds seamless so it never fatigues), a full UI rewrite into a jade-Qi glassmorphism theme, settings, pause, and an onboarding system I rebuilt from a modal tutorial into **in-play coach-marks** that teach one verb at a time and retire the instant you perform it.

Then ship prep, which is its own season: a real app icon, music shipped as a 973KB OGG instead of a 17MB WAV, an export filter that took the build from ~79MB down to ~21MB, removing a "watch ad — coming soon" line because reviewers reject vaporware, and wiring telemetry as **local-only** JSONL so the App Store privacy label can honestly say *No Data Collected.*

I will admit one thing humbling: I added a studio splash screen, removed it, fought the iOS launch storyboard falling back to Godot's default logo, and re-added the splash — across several days. Small polish eats time out of all proportion to its size. Budget for it.

## Where it landed

*Tribulation* is on the App Store now: free, no ads, no in-app purchases, no data collected. One developer, one engine, and one idea I refused to let go of — a road that fights back, and a climb that's supposed to be hard.

The realms are saved. The road resets. Walk it again.

— *Vellicade Games*

> *Editor's note: the studio name appears as both "Vellicade" and "Vallicade" across the project, and the iOS bundle ID is `com.vellicade.tribulation`. Settle on one spelling before publishing this post.*
