# Test: poster rotation, uprighting and `flip`

Covers the upside-down 3:4 report (Nexus, mrgreaper, 2026-11-02), the `flip` keyword, advanced rotation on multi-block posters, and the rotation-mismatch warning. Needs a client; a dedicated server alone renders nothing.

## Setup

- Build (`dotnet build`) and install `KitsunePosters/` on the server and the client, EAC off. Single player works too.
- Test image: `https://i.postimg.cc/MpD9gwYz/image.png`. It's the reporter's own screenshot, and sky above / floor below make "upright" obvious. If the allowlist isn't `*`, run `kpadmin domains add postimg.cc` first.
- Pick an origin `X Y Z` on open, flat ground and stand back from it. Coordinates below are relative to it.
- Keep the client log open: `%APPDATA%\7DaysToDie\logs\` on Windows, newest `output_log_client` file. Grep it for `[KitsunePosters]` after each section.

## 1. Every wall rotation, forced (admin console)

`kpadmin place` now takes a rotation (0-27), so this covers the rotations R can't easily reach. Place a 3:4 at each of these, 3 blocks apart along X, then set its text:

```
kpadmin place <X+3i> <Y+1> <Z> kitsunePoster3by4 <rotation>
kpadmin text  <X+3i> <Y+1> <Z> https://i.postimg.cc/MpD9gwYz/image.png
```

The rotations are grouped by the way the poster faces. Every rotation in a group should look identical: image upright, and the sideways ones (`*`) hang landscape with a 4:3 image.

| Board faces | Rotations | Upright | Landscape | Warning in log? |
|---|---|---|---|---|
| -Z | 0, 4, 12*, 20* | | | |
| -X | 1, 7, 11*, 17* | | | |
| +Z | 2, 6, 14*, 22* | | | |
| +X | 3, 5, 9*, 19* | | | |
| 45° | 24, 25, 26, 27 | | n/a | |

Expected: all upright, no `renders needing` warning.

- **Any upside-down poster here** means the uprighting itself is wrong for that rotation. Note the rotation.
- **Any warning** means the stored and rendered rotations disagree: copy the line, since it holds the block, rotation and both rolls.

Repeat a few rotations (0, 4, 12) with `kitsunePoster1x1` and `kitsunePoster9by16` to check the fix isn't specific to one board.

## 2. The report, by hand

This is what the reporter did. Hold a 3:4 poster and place it flush on a real wall, once on each of the four walls of a room. On each wall, try both the default placement and placement after cycling R; with advanced rotation back, the radial menu should now offer it for this block.

| Wall | Rotations tried (`kpadmin info` shows it) | Upright? | Warning? |
|---|---|---|---|
| N | | | |
| E | | | |
| S | | | |
| W | | | |

If any comes out upside down, that's the bug reproduced. Note the wall and rotation, and grab the warning line if there is one.

## 3. `flip`

On any upright single poster from above:

- [ ] Text `<url> flip`: the image turns 180°.
- [ ] Text `<url> fit flip` and `<url> flip fit`: fit mode applies and the image is flipped, whatever the order.
- [ ] Text `<url> FLIP`: also flips (case-insensitive).
- [ ] Remove `flip`: the image goes back upright.
- [ ] On a sideways (landscape) poster: `flip` turns it 180° and it stays landscape.
- [ ] A second client sees the flip too (the text syncs).
- [ ] Leave the area and come back, or relog: the flipped poster is still flipped.

## 4. `flip` doesn't break murals

- [ ] Place a 2x1 row of `kitsunePoster1x1` on one wall, both with the same URL: one image spreads across them.
- [ ] Add `flip` to one of them: the mural stays intact and correctly ordered, since `flip` is ignored on a tiled cell.
- [ ] Break the group (remove the other poster): the remaining one now shows the full image, flipped.

## 5. Advanced rotation on the other multi-blocks

- [ ] `kitsunePoster2x2`, `kitsunePoster3x2`, `kitsunePoster2by3Large` and `kitsuneGraffiti3x2` each place sideways and upside down on a wall without overlapping neighbouring blocks, and the image reads upright.
- [ ] Picking one up with E > Take removes the whole multi-block (no orphan child blocks left behind).

## Report back

The filled tables, any upside-down rotation numbers, and every `[KitsunePosters]` warning line from the log.
