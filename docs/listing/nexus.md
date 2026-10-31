# Kitsune Posters

**Hang any image from the internet on your wall. No image packs, no cap, no server setup.**

Place a poster, press E, paste an image link. The poster downloads the image on each player's machine and shows it, lit like any painting. Every player on a server decorates with their own pictures, the server owner installs nothing but the mod, and there is no limit on how many different images a world can hold.

## What you get

- **Five frameless poster boards**: 1x1, 2x2 and 3x2 blocks, plus 3:4 and 9:16 movie-poster shapes, craftable from wood and paper, placeable on any wall.
- **Graffiti in three sizes too**: decal blocks with no board at all, 1x1, 2x2 and 3x2. Paste a link to a transparent PNG and the art sits straight on the wall, transparency kept, like spray paint. The faint placement ghost disappears once the image loads.
- **Paste almost any link.** Direct PNG/JPG links work, and so do the links people actually copy: imgur pages, galleries and albums resolve to their image automatically, giphy links show the GIF's still frame, and other pages are resolved through their preview image. Fragments, crop suffixes and other link noise are handled.
- **Fill, fit or stretch.** The image fills the board by default (centre cropped like a printed poster). Type `fit` or `stretch` after the link to letterbox or stretch instead.
- **Poster walls.** Identical 1x1 posters with the same link, placed in a rectangle, merge into ONE image spread seamlessly across the whole wall. Build a 4x3 mural out of twelve posters.
- **Cached.** Each image downloads once per machine, ever. Chunk reloads come from disk.

## For server owners

- **A settings window, no files, no console**: press E on any poster and pick "Poster settings". Admins see the site allowlist and change it with buttons; changes save and apply at once, for remote admins too. The console works as well (`kpadmin domains add imgur.com`, `remove`, `allowall`), and so does `Settings.xml`. A blocked URL is rewritten to `[blocked: domain]` in the sign text itself, server side, before it ever syncs.
- **`kpadmin wipe <x> <y> <z> [radius]`**: clear any poster's link from the console, with an area option for cleaning up after someone.
- **Hard caps** baked in: 5 MB per image, 4096px max, 15 second timeout, PNG/JPG only.
- Images are fetched by each client directly, so the URL's host sees each player's IP, the same as posting a link in chat. The allowlist exists for exactly this.

## For players

- `kposters off` hides all poster images on YOUR machine (saved across sessions); `kposters on` brings them back. Instant, no reload.
- Clear a poster's text to get a blank board back.

## Install

DLL mod: **EAC off**, and the mod goes on the **server and every client**. Clients without it see a plain board with the link as text. Single player works too.

## Notes

- The mod raises the sign edit window's input limit from 50 to 300 characters (image URLs need the room). This applies to all signs and writable crates.
- GIFs show their still frame. Animated posters are on the roadmap, not in this release.
- Albums show their cover image. For a specific picture out of an album, copy the image's own address (right click, "Copy image address").

Built for 7 Days to Die V3.4. Part of the Kitsune Den mod family.
