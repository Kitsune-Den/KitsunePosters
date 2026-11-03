# Changelog

## Unreleased

- Add the word "flip" after a poster's link to turn its image upside down. It's for the 3:4 poster that was reported showing its image upside down on a wall: the uprighting makes every rotation of a poster on that wall look the same, so rotating it again couldn't put it right. "flip" works on any poster that isn't part of a tiled mural.
- The multi-block posters and graffiti now have advanced rotation like the 1x1 (2x2, 3x2 and the movie-poster shapes). Turned sideways, a portrait poster hangs landscape: the 3:4 board shows a 4:3 image the right way up.
- When a poster's model is turned differently from the rotation stored for its block, a likely cause of an upside-down image, the client log now gets one warning for that poster with both rotations, to help track the cause down.
- imgur image addresses copied by right-clicking an image (`i.imgur.com/<id>_d.webp`) now load: they were a small WebP preview the game can't decode, and are now swapped for the full image. imgur's GIF and video links get a still the same way.

## 1.3.0 (2026-11-01)

- 2:3 posters, the classic movie one-sheet ratio, in three sizes: small (0.5 x 0.75 m, fits in one block), regular (1 x 1.5 m, one block wide and two tall) and large (2 x 3 m). A 2:3 image fills them with nothing cropped. The small one doesn't tile.

## 1.2.0 (2026-11-01)

- Movie-poster shapes: two new poster boards at 3:4 and 9:16, one block wide and two tall. A 9:16 or 3:4 image fills them with nothing cropped.
- The 3x2 poster's and graffiti's image face is now exactly 3:2 (it was a hair wider), so 3:2 images fill it uncropped too.

## 1.1.0 (2026-10-31)

- Posters and graffiti always read right side up on a wall, whichever way they were rotated when placed. Rotating a poster onto a wall used to leave the image upside down (or sideways), and the blank board gave no way to tell beforehand.
- Posters can be picked up: E > Take returns the block to your inventory. Works anywhere you could place one; locked posters only for players allowed to edit them.
- Tiled murals on two of the four wall directions had their columns in reverse order; fixed. Posters placed upside down now tile with upright ones.

## 1.0.1 (2026-10-28)

- Fixed sign text (URLs and plain sign messages on poster and graffiti blocks) drawing on top of everything and showing through walls. Text now respects world geometry like any other object.

## 1.0.0 (2026-10-28)

First release.

- Three frameless poster boards (1x1, 2x2, 3x2), craftable from wood and paper.
- Press E on a placed poster, paste an image link, done: the image downloads on each player's machine, fills the board and is lit by the world. No server-side image packs, no image cap.
- Smart links: imgur pages, galleries and albums resolve to their image; giphy links show the GIF's still frame; anything else that returns a page is resolved through its og:image tag, one hop, allowlist checked.
- Fit modes: the image fills the board by default (centre crop); add the word "fit" or "stretch" after the link to change that.
- Tiling: identical 1x1 posters with the same link, forming a complete rectangle, show one image spread seamlessly across the wall.
- Safety: optional server-side domain allowlist (blocked URLs are rewritten in the sign text itself), per-machine "kposters off" toggle, 5 MB / 4096px / 15 s caps, session and disk caching, admin "kpadmin wipe" with radius.
- Graffiti blocks (1x1, 2x2, 3x2): near-invisible decals that show a transparent PNG straight on the wall, no board behind it. A faint ghost panel marks the empty block and disappears once the image loads. The 1x1 tiles into murals like the posters do.
- A clickable settings window on every poster's E menu: the image toggle for everyone, the allowlist with add/remove buttons for admins, working for remote admins over a small sync package.
- Raises the sign edit window's input cap from 50 to 300 characters (all signs), since image URLs need the room.
