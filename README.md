# Tribulation

A 3-lane mobile endless runner built in **Godot 4** where every obstacle is an attack. You play a dark martial artist fleeing righteous sects through their own lands — each jump, slide, lane-change, and slash is a read-and-counter against a telegraphed technique, not a hop over scenery. **In active development.**

## Design core: readable, not just fast

Every hazard attacks on a plane, telegraphed by a colored qi-line, and one input answers it:

| Input | Counters | Telegraph |
|---|---|---|
| Swipe ↑ (jump) | LOW ground sweeps & chains | amber ground-glow |
| Swipe ↓ (slide) | HIGH slashes & beams | cyan head-height line |
| Swipe ←/→ (lane) | lane-locked strikes & projectiles | red lane-streak |
| Tap (slash) | destructible foes, wards, projectiles | white flash |

Players learn to *read* attacks rather than memorize patterns. Mid-run you harvest souls, cultivate, and break through realms — turning from hunted prey into the calamity they feared.

## Built so far

Phased devlog-driven development ([`devlog.txt`](devlog.txt)):
- Auto-run player (CharacterBody2D), recycling infinite ground, camera follow
- Touch gesture layer (SwipeDetector → jump/slide/tap signals) alongside keyboard input
- Obstacle spawner with ramping frequency, death/restart loop, distance HUD

## Docs

The design lives in written specs before it lives in code: [`MURIM_DESIGN.md`](MURIM_DESIGN.md) (design bible), [`BALANCE.md`](BALANCE.md), [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md), plus iOS/Android ship runbooks.

---

By [Daniel Baba](https://linkedin.com/in/baba-daniel)
