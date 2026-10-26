# Changelog

## 1.0.0 (2026-10-26)

First release.

- Three frameless poster boards (1x1, 2x2, 3x2), craftable from wood and paper.
- Press E on a placed poster, paste an image link, done: the image downloads on each player's machine, fills the board and is lit by the world. No server-side image packs, no image cap.
- Smart links: imgur pages, galleries and albums resolve to their image; giphy links show the GIF's still frame; anything else that returns a page is resolved through its og:image tag, one hop, allowlist checked.
- Fit modes: the image fills the board by default (centre crop); add the word "fit" or "stretch" after the link to change that.
- Tiling: identical 1x1 posters with the same link, forming a complete rectangle, show one image spread seamlessly across the wall.
- Safety: optional server-side domain allowlist (blocked URLs are rewritten in the sign text itself), per-machine "kposters off" toggle, 5 MB / 4096px / 15 s caps, session and disk caching, admin "kpadmin wipe" with radius.
- Raises the sign edit window's input cap from 50 to 300 characters (all signs), since image URLs need the room.
