# KitsunePosters

Craftable poster blocks for 7 Days to Die that display any image from a URL. Press E on a placed poster, paste an image link, and the poster shows it. No server-side image packs, no 69-image vanilla pool, no 255-paint cap: every player decorates with their own images and the server owner does nothing.

## How it works

The poster block is a sign under the hood (`TEFeatureSignable`), so the URL rides vanilla's own sign text: synced to every client and saved with the world, with zero custom netcode. A Harmony postfix on `TEFeatureSignable.RefreshTextMesh` spots an http(s) URL in the sign text on a `kitsunePoster*` block, downloads the image client side, hides the text mesh, and parents a textured quad to the text anchor. Ordinary signs are untouched.

- Session + disk cache (`<user game data>/KitsunePosters/`), keyed by URL hash. Each image downloads once per machine, ever.
- Caps: 5 MB per image, 4096px max dimension, 15 s timeout, PNG/JPG only. Failures show the sign text again so the URL can be fixed, and the URL is not retried that session.
- Dedicated servers skip the rendering (no renderer there); the server syncs sign text like always and enforces the allowlist.

## Settings and commands

`Settings.xml` in the mod folder:

- `AllowedDomains`: comma-separated hosts posters may load from; `*` (the default) allows everything. An entry matches the host and its subdomains (`imgur.com` also allows `i.imgur.com`, but not `fakeimgur.com` or `imgur.com.evil.net`). The server's copy is authoritative: a disallowed URL is rewritten to `[blocked: <host>]` in the sign text itself before it ever syncs, and clients independently refuse to render URLs their own copy disallows (second gate, since the SetText patch rides a virtual method).
- `ShowRemoteImages`: per-machine default for whether poster images render at all.

Console command `kposters` (every player, runs on their own client):

- `kposters` shows the current settings, `kposters on|off` toggles images on this machine (saved, applies to loaded posters at once), `kposters reload` re-reads Settings.xml, `kposters allow <url>` tests the allowlist.

**Settings window** (for people who do not live in consoles): press E on any poster and pick "Poster settings", or type `kposters menu`. Everyone gets the poster-images on/off toggle for their own machine; the site allowlist shows for everyone and saves only for admins (the server checks permission level 0 on every change and the window says so otherwise). On a dedicated server the window talks to the server through a small Ask/Set/Tell package, so remote admins manage the list in game.

Admin command `kpadmin` (permission 0, runs on the server):

- `kpadmin domains` / `domains add <site>` / `domains remove <site>` / `domains allowall` shows and edits the allowlist in game; changes persist to Settings.xml and apply immediately. Adding the first real site drops a lone `*` (that is what starting an allowlist means); URLs pasted as the site are reduced to their host.
- `kpadmin wipe <x> <y> <z> [radius]` clears poster text at that spot or in a cube around it.
- `kpadmin info <x> <y> <z>` shows a poster's text and its tiling group.
- `kpadmin place / text / load` are testing helpers for driving posters from the server console.

## Tiling

Adjacent posters (same block, same rotation, same URL) forming a complete rectangle show ONE image spread across them, seamlessly: each tiled cell fills its whole block face. An L-shape or ragged group falls back to each poster showing the full image. Groups are computed from world state on every client, so nothing extra syncs; capped at 64 blocks. Editing or wiping one poster re-renders its neighbours and their groups.

## Install

DLL mod: EAC off, and the mod goes on the server **and every client**. Clients without it see a plain sign with the URL as text.

## Blocks

Three frameless poster boards from the mod's own bundle, craftable from wood + paper: `kitsunePoster1x1`, `kitsunePoster2x2` and `kitsunePoster3x2` (multi-block, one tile entity, no seams). The image fills the board's face. Tiling across neighbours is for the 1x1 only; bigger walls, use the bigger posters.

The graffiti blocks (paper + glue) are the decal variant, in the same three sizes: `kitsuneGraffiti1x1`, `kitsuneGraffiti2x2` and `kitsuneGraffiti3x2`. Their board is a faint transparent ghost, and once an image loads the DLL hides the board entirely and keeps the image's PNG transparency, so the art sits straight on the wall like spray paint. The ghost's Fade-mode Standard material is what the image quad clones (color reset to white), so the one material drives both looks. Graffiti tiles with graffiti (same block type, 1x1 only), never with posters.

## Model pipeline (HouseCat-style)

`Workspace/UnityProject` is a Unity 2022.3.62f3 project whose editor script builds everything procedurally - thin board (scaled cube, flush against the block volume's +Z face, front facing -Z), TextMesh child for `TEFeatureSignable`, `T_Mesh` tag on the collider object (the project's TagManager defines the 7D2D tag order: Item, T_Mesh, B_Mesh, T_Mesh_B, T_Block, because bundles store tags as indexes):

```
"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -quit -projectPath Workspace\UnityProject -executeMethod BuildPosters.Build -logFile Workspace\unity-build.log
```

The bundle lands in `KitsunePosters/Resources/KitsunePosters.unity3d`; blocks reference it as `#@modfolder:Resources/KitsunePosters.unity3d?poster_1x1`. The game puts the prefab pivot at the block volume's centre (multi-block: the whole volume's centre), so prefabs are modelled centred. If the board hangs on the wrong face in game, flip `BoardSide` in `BuildPosters.cs`.

## Status: release candidate

Confirmed in game (2026-10-26 playtest): board renders frameless and faces the player, E/focus works (collider root tagged `T_Block`), images download, fill the board (centre crop) and shade with world lighting, imgur page/gallery/album links and giphy links resolve, long URLs fit the edit window.

Link handling, in order: fragment strip, imgur `imgur.com/<id>` to the direct image, giphy (page / media / file forms) to the CDN's JPG still, and for anything else that downloads as HTML, one hop through the page's `og:image` with the allowlist re-checked. GIF/WebP cannot decode (Unity limit): giphy gets a still; animation is a post-1.0 feature by choice.

Found and fixed after the first settings-window playtest: the window's XML
controller attribute lacked the namespace (`controller="KitsunePosters.KitsunePosterSettings, KitsunePosters"`
is the required form; the game inserts `XUiC_` after the last dot and resolves
via `Type.GetType`). Without it the game logs ONE error at XUi parse time and
silently runs the window on a base controller: it opens and draws, but every
button is dead. Every button press now also logs a `[KitsunePosters]` line, so
a dead window is diagnosable from the log alone.

Confirmed in game (2026-10-28, Kitsune Cat Test server): graffiti ghost board, decal transparency and board hiding all work; the bundled Fade material survived the build. Dark art reads lighter in daylight because the decal is lit like the world (by design; it goes properly dark at night).

Still to see in game before release:
- The settings window's buttons (toggle, add/remove, allow all, close) after the controller fix.
- 2x2 / 3x2 boards and graffiti (render, hit/pickup, one image, no seams).
- Tiling wall of 1x1s: image spread, direction per facing (fix = the UDir table in `PosterTiling.cs`), seams.
- `kposters off/on` live re-render; chunk reload re-applying from cache.
- Allowlist block on a real multiplayer edit (dedi-verified already; belt and braces).

Verified on a live dedi via telnet: Settings.xml parsing, `kposters` and `kpadmin`, allowlist matching (subdomains pass; lookalikes, suffix spoofs and raw IPs blocked), the SetText prefix firing on a real server (disallowed URL rewritten to `[blocked: <host>]`), group detection with correct cells (incl. that multi-block posters don't try to tile), L-shape fallback, radius wipe, and all three blocks + the bundle parsing and placing clean.

Note for server owners: the mod raises the sign edit window's input cap from vanilla's 50 characters to 300 (URLs need it); this applies to all signs and writable crates.

## Build

```
dotnet build
```

References the game's own assemblies from `7d2d-v3.4-exp-ref` (override with `-p:GameManaged=...`). The DLL lands in `KitsunePosters/` next to `ModInfo.xml`; that folder is the installable mod.
