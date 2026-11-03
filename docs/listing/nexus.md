# Kitsune Posters

**Hang any image from the internet on your wall. No image packs, no cap, no server setup.**

Place a poster, press E, paste an image link. The poster downloads the image on each player's machine and shows it, lit like any painting. Every player on a server decorates with their own pictures, the server owner installs nothing but the mod, and there is no limit on how many different images a world can hold.

## What you get

- **Eight frameless poster boards**: 1x1, 2x2 and 3x2 blocks, plus movie-poster shapes: 2:3 in three sizes (0.5 x 0.75 m, 1 x 1.5 m and 2 x 3 m), 3:4 and 9:16. Craftable from wood and paper, placeable on any wall. A thin dark rim of board shows round the image.
- **Graffiti in three sizes too**: decal blocks with no board at all, 1x1, 2x2 and 3x2. Paste a link to a transparent PNG and the art sits straight on the wall, transparency kept, like spray paint. The faint placement ghost disappears once the image loads.
- **Paste almost any link.** Direct PNG/JPG links work, and so do the links people actually copy: imgur pages, galleries and albums resolve to their image automatically, giphy links show the GIF's still frame, and other pages are resolved through their preview image. Fragments, crop suffixes and other link noise are handled.
- **Fill, fit or stretch.** The image fills the board by default (centre cropped like a printed poster). Type `fit` after the link to show the whole image with nothing cut off (handy for graffiti), or `stretch` to squash it to the board's shape.
- **Poster walls.** 1x1 posters with the same link, forming a complete rectangle of any size, merge into ONE image spread seamlessly across the whole wall: 2x1, 4x3, 8x8, up to 64 posters. Different links stay separate pictures, and a gap or an L shape means each poster shows the whole image on its own. Works for 1x1 graffiti too.
- **Cached.** Each image downloads once per machine, ever. Chunk reloads come from disk.

## Where to host your images

Any site that gives you a direct image link works. These are good ones:

- **Imgur**: page, gallery and album links all work as copied.
- **Postimages**: use the "Direct link".
- **Catbox**: its links are already direct.
- **ImgBB**: pick the direct link from the embed codes.
- **Flickr**: right click the photo, "Copy image address".

Rule of thumb: if the link opens just the picture, it works. If a server limits which sites are allowed, ask the owner which ones.

## For server owners

- **A settings window, no files, no console**: press E on any poster and pick "Poster settings". Admins see the site allowlist and change it with buttons; changes save and apply at once, for remote admins too. The console works as well (`kpadmin domains add imgur.com`, `remove`, `allowall`), and so does `Settings.xml`. A blocked URL is rewritten to `[blocked: domain]` in the sign text itself, server side, before it ever syncs.
- **`kpadmin wipe <x> <y> <z> [radius]`**: clear any poster's link from the console, with an area option for cleaning up after someone.
- **Hard caps** baked in: 5 MB per image, 4096px max, 15 second timeout, PNG/JPG only.
- Images are fetched by each client directly, so the site hosting an image sees the IP of each player whose game loads it, the same as clicking a link. Server owners never see anyone's IP from a poster. The risk is a player linking to a server they run and reading its logs; locking the allowlist to known image hosts (say, imgur.com) shuts that down.

## For players

- `kposters off` hides all poster images on YOUR machine (saved across sessions); `kposters on` brings them back. Instant, no reload.
- Clear a poster's text to get a blank board back.

## Install

DLL mod: **EAC off**, and the mod goes on the **server and every client**. Single player works too.

The folder should be `Mods/KitsunePosters/ModInfo.xml`. Unzipping can add an extra folder (`Mods/KitsunePosters-1.4.0/KitsunePosters/...`), and the game silently skips the mod when it's nested like that.

**Can't join a friend?** "Unexpected data received. Server is either running an incompatible version or requires the client to install mods" means the mod isn't running on the joining PC, even if it's in the Mods folder. Check that EAC is off and the folder is right on both machines. Your newest log in `%APPDATA%\7DaysToDie\logs` (`output_log_client...`) has `[MODS]` lines near the top that say whether KitsunePosters loaded.

## Notes

- The mod raises the sign edit window's input limit from 50 to 300 characters (image URLs need the room). This applies to all signs and writable crates.
- GIFs show their still frame. Animated posters are on the roadmap, not in this release.
- Albums show their cover image. For a specific picture out of an album, copy the image's own address (right click, "Copy image address").

Built for 7 Days to Die V3.4. Part of the Kitsune Den mod family.
