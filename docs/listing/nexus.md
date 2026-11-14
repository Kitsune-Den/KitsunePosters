# Kitsune Posters

**Hang any image on your wall, from a link or straight from your PC. No image packs, no cap, no server setup.**

Place a poster, press E, paste an image link or type the name of a picture on your PC. The poster shows it, lit like any painting. Every player on a server decorates with their own pictures, the server owner installs nothing but the mod, and there is no limit on how many different images a world can hold.

## What you get

- **Eight frameless poster boards**: 1x1, 2x2 and 3x2 blocks, plus movie-poster shapes: 2:3 in three sizes (0.5 x 0.75 m, 1 x 1.5 m and 2 x 3 m), 3:4 and 9:16. Craftable from wood and paper, placeable on any wall. A thin dark rim of board shows round the image.
- **Graffiti in three sizes too**: decal blocks with no board at all, 1x1, 2x2 and 3x2. Paste a link to a transparent PNG and the art sits straight on the wall, transparency kept, like spray paint. The faint placement ghost disappears once the image loads.
- **Your own pictures.** Drop a PNG or JPG in a folder and type its name on a poster. On a server everyone sees it, no hosting needed. See below.
- **Paste almost any link.** Direct PNG/JPG links work, and so do the links people actually copy: imgur pages, galleries and albums resolve to their image automatically, giphy links show the GIF's still frame, and other pages are resolved through their preview image. Fragments, crop suffixes and other link noise are handled.
- **Fill, fit or stretch.** The image fills the board by default (centre cropped like a printed poster). Type `fit` after the link to show the whole image with nothing cut off (handy for graffiti), or `stretch` to squash it to the board's shape.
- **Poster walls.** 1x1 posters with the same link, forming a complete rectangle of any size, merge into ONE image spread seamlessly across the whole wall: 2x1, 4x3, 8x8, up to 64 posters. Different links stay separate pictures, and a gap or an L shape means each poster shows the whole image on its own. Works for 1x1 graffiti too.
- **Cached.** Each image downloads once per machine, ever. Chunk reloads come from disk.

## Use pictures from your own PC

No hosting needed. You need version 1.5.0 or newer.

1. Start the game once with the mod installed. That creates the folder.
2. Open File Explorer, paste `%APPDATA%\7DaysToDie\KitsunePosters\Images` into the address bar and press Enter.
3. Drop your PNGs or JPGs in there. No spaces in the file names: `my-cat.png`, not `my cat.png`.
4. Press E on a poster and type the file name where you'd normally paste a link: `my-cat.png`. `fit`, `stretch` and poster walls work the same as with links.

Added a file while the game is running? Open the console (F1) and type `kposters reload`. Picture not showing up? `kposters images` shows the exact folder and every file the mod can see in it.

Big photos are fine: files up to 40 MB, and anything over 4096px is scaled down for you.

Playing on a server? Everyone sees it. When you save the poster your game sends the picture to the server, the server keeps it with the world, and every other player gets it once when they walk up to it. The text on the poster changes to something like `my-cat.png@3f9a0c12be47`. That code is the picture's fingerprint, so editing or deleting your file later doesn't touch posters you already put up.

Big pictures get shrunk to 2048px before they're sent, so a 30 MB phone photo turns into a few hundred KB and nobody sits waiting on it.

Want pictures everyone has from the start? A server mod pack can include them in an `Images` folder inside the mod folder.

## Where to host your images

Prefer links? Any site that gives you a direct image link works. These are good ones:

- **Imgur**: page, gallery and album links all work as copied.
- **Postimages**: use the "Direct link".
- **Catbox**: its links are already direct.
- **ImgBB**: pick the direct link from the embed codes.
- **Flickr**: right click the photo, "Copy image address".

Rule of thumb: if the link opens just the picture, it works. If a server limits which sites are allowed, ask the owner which ones.

## For server owners

- **A settings window, no files, no console**: press E on any poster and pick "Poster settings". Admins see the site allowlist and change it with buttons; changes save and apply at once, for remote admins too. The console works as well (`kpadmin domains add imgur.com`, `remove`, `allowall`), and so does `Settings.xml`. A blocked URL is rewritten to `[blocked: domain]` in the sign text itself, server side, before it ever syncs.
- **Players' own pictures**: on by default. Turn sharing off with the "Players' own pictures" switch in the settings window or `kpadmin images off` (pictures already up stay up). `kpadmin images list` shows every shared picture and who shared it; `kpadmin images remove <code>` deletes one, takes it off every poster and blocks it from coming back. They're kept with the world save and capped at 500 MB all together (`UploadSpaceMB` in Settings.xml).
- **`kpadmin wipe <x> <y> <z> [radius]`**: clear any poster's link from the console, with an area option for cleaning up after someone.
- **Hard caps** on links, baked in: 5 MB per image, 4096px max, 15 second timeout, PNG/JPG only.
- Images are fetched by each client directly, so the site hosting an image sees the IP of each player whose game loads it, the same as clicking a link. Server owners never see anyone's IP from a poster. The risk is a player linking to a server they run and reading its logs; locking the allowlist to known image hosts (say, imgur.com) shuts that down. Pictures shared from players' PCs go through your server, so they don't expose anyone's IP.

## For players

- `kposters off` hides all poster images on YOUR machine (saved across sessions), including pictures other players shared; `kposters on` brings them back. Instant, no reload.
- Clear a poster's text to get a blank board back.

## Install

DLL mod: **EAC off**, and the mod goes on the **server and every client**. Single player works too.

Everyone needs the same version. A player still on 1.4.0 sees a shared picture as text (`my-cat.png@3f9a...`) instead of the image.

The folder should be `Mods/KitsunePosters/ModInfo.xml`. Unzipping can add an extra folder (`Mods/KitsunePosters-1.5.0/KitsunePosters/...`), and the game silently skips the mod when it's nested like that.

**Can't join a friend?** "Unexpected data received. Server is either running an incompatible version or requires the client to install mods" means the mod isn't running on the joining PC, even if it's in the Mods folder. Check that EAC is off and the folder is right on both machines. Your newest log in `%APPDATA%\7DaysToDie\logs` (`output_log_client...`) has `[MODS]` lines near the top that say whether KitsunePosters loaded.

## Notes

- The mod raises the sign edit window's input limit from 50 to 300 characters (image URLs need the room). This applies to all signs and writable crates.
- GIFs show their still frame. Animated posters are on the roadmap, not in this release.
- Albums show their cover image. For a specific picture out of an album, copy the image's own address (right click, "Copy image address").

Built for 7 Days to Die V3.4. Part of the Kitsune Den mod family.
