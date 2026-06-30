# In-run HUD prototype — verdict

**Question:** what should the in-run HUD + attack-telegraph layer look like?

**Answer (2026-06-26): Variant A — Ink & Talisman.**
Calligraphic wuxia-scroll direction: warm parchment/ink base, qi-jade + cinnabar
accents, vertical kanji realm column, **Sky-Net as a closing concentric seal ring**
(not a bar), brush-streak telegraphs with seal-style names.

Fold A into the real UI during UI-4 (HUD) and UI-2 (telegraph renderer).
Losing variants (B Cultivator HUD, C Blood-Qi Calamity) kept in `in-run-hud.html`
only as a bit-stealing reference until UI-4 lands, then delete.

Carry-overs to fix when folding in:
- add shield pips (contextual, was omitted in the mock)
- telegraph color language: amber=low/jump, cyan=high/slide, red=lane, white=slash/destructible
