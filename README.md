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

Admin command `kpadmin` (permission 0, runs on the server):

- `kpadmin wipe <x> <y> <z> [radius]` clears poster text at that spot or in a cube around it.
- `kpadmin info <x> <y> <z>` shows a poster's text and its tiling group.
- `kpadmin place / text / load` are testing helpers for driving posters from the server console.

## Tiling

Adjacent posters (same block, same rotation, same URL) forming a complete rectangle show ONE image spread across them, seamlessly: each tiled cell fills its whole block face. An L-shape or ragged group falls back to each poster showing the full image. Groups are computed from world state on every client, so nothing extra syncs; capped at 64 blocks. Editing or wiping one poster re-renders its neighbours and their groups.

## Install

DLL mod: EAC off, and the mod goes on the server **and every client**. Clients without it see a plain sign with the URL as text.

## Status: prototype

- One block: `kitsunePosterWood1x1` (craftable, 8 wood + 4 paper), reusing the vanilla 1x1 wood sign model. Frameless poster models and more sizes come later.
- Untested in game. First playtest checklist:
  - Quad visible and facing outward? If it is inside the board, flip `FaceOffset` in `PosterSurface.cs`. If the image is mirrored, negate `localScale.x`.
  - Shader pick (watch the log for `Using shader '...'`).
  - Sign edit window accepts a full CDN URL (text length cap).
  - Reload: leave the chunk, come back, poster should re-apply from cache.
  - Tiling in the flesh: build a 2x3 poster wall with one URL, check the image spreads, isn't mirrored or reversed per facing (fix = the UDir table in `PosterTiling.cs`), and that seams line up.
- Verified on a live dedi via telnet: Settings.xml parsing, `kposters` and `kpadmin`, allowlist matching (subdomains pass; lookalikes, suffix spoofs and raw IPs blocked), the SetText prefix firing on a real server (disallowed URL rewritten to `[blocked: <host>]`), 2x2 group detection with correct cells, L-shape fallback, and radius wipe.
- Planned before release: frameless custom model, more sizes.

## Build

```
dotnet build
```

References the game's own assemblies from `7d2d-v3.4-exp-ref` (override with `-p:GameManaged=...`). The DLL lands in `KitsunePosters/` next to `ModInfo.xml`; that folder is the installable mod.
