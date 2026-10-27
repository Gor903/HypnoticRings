# Store listing

> The fenced blocks below are the **only** copy of this text.
> `check-listing.py` finds them by the `### Heading` immediately above a
> fence, so keep that shape — rename a heading and the check silently
> stops measuring that field.

Draft for the console. Every text block is copied verbatim into the
console — they stay inside fenced blocks so they can be diffed
programmatically rather than retyped.

Character counts in this document are **measured**, not estimated.
Re-run `python3 docs/publishing/check-listing.py` after any edit.

## Platform limits

| Field | Limit | Required |
| --- | --- | --- |
| App name | 30 characters | yes |
| Short description | 80 characters | yes |
| Full description | 4000 characters | yes |
| Release notes | 500 characters per language | yes |
| App icon | 512×512 PNG, no real transparency | yes |
| Feature graphic | 1024×500 PNG or JPEG | yes |
| Phone screenshots | 2–8, side 320–3840 px, ratio at most 2:1 | yes |
| Video | YouTube link | no |

Read the current limits off the console if anything here is refused —
they move.

## Files

- `art/store-icon-512.png` — 512×512
- `art/feature-graphic-1024x500.png` — 1024×500
- `art/store-screenshots/` — 5 frames, 1080×1920 (the review set lives in `art/screenshots/`) each

## Default locale — en-US

### App name

```
Hypnotic Rings
```

### Short description

```
Turn the rings, slip the ball through the gaps, keep the tunnel from closing.
```

> One line: what the player does, plus the one thing that sets it apart.

### Full description

```
Concentric rings close in on you. A ball rests on the one below it. Every ring has a gap, and your finger turns them all.

Swipe in a circle to bring a gap under the ball. It drops through, you score, and the next ring is already tightening. Miss, and the tunnel keeps shrinking until there is nowhere left to stand.

That is the whole game. No tutorial, no currency, no timers — one gesture you learn in four seconds and spend much longer getting good at.

WHAT IT ASKS OF YOU
One thumb. A few minutes, or an hour.

WHAT IT DOES NOT DO
No ads. No purchases. No accounts, no sign-in, no data collected, nothing sent anywhere — the game has no internet permission at all, so it cannot phone home even by accident. Your best score lives on your phone and nowhere else.

AS YOU GET BETTER
The tunnel compresses faster, the gaps narrow, and the rings begin to turn against you. Five visual themes unlock as your record climbs, from stark black-and-white to neon and deep midnight blue.

MADE TO BE PLAYED ANYWHERE
Works offline, always. Under two hundred kilobytes of sound, no streaming, no downloads after install. English and Russian, picked up from your phone's language.

ACCESSIBILITY
Rotation can be inverted, gesture sensitivity adjusts, and every effect can be turned down if motion bothers you. Sound and music have separate switches and separate volumes.
```

> Shape that has worked twice: what the game is in plain words; how it
> plays in two or three sentences of verbs, not adjectives; what it does
> *not* do — no account, no network, no ads — if true; then a CAPS
> heading per feature with a short paragraph each; then a closing
> paragraph on privacy and permissions that matches the privacy policy in
> substance. If those two disagree, one of them is a false declaration.
>
> Keep any genre keyword to **at most one** mention. Repeating a
> well-known game's name through a listing is what triggers impersonation
> takedowns, and `KEYWORD_CAPS` in `check-listing.py` counts it.

## Release notes — en-US, 1.0.0 (1)

The console presents this field already wrapped in `<en-US>` tags. Paste
the text between them and leave the tags alone; they are how the console
assigns notes to a language, not part of what players read.

Rewrite this block for each release — it describes one release, not the
app.

### Release notes

```
First release.

Endless tunnel, five unlockable themes, English and Russian. Plays offline with no ads, no purchases and no data collection.
```

No version number in the text. The console shows it beside the notes
already, and a number repeated by hand is a number that goes stale.

## Screenshots

Uploaded in this order:

| File | What is on the frame |
| --- | --- |
| `store-screenshots/monochrome-9x16.png` | The default theme — black rings on white, mid-run |
| `store-screenshots/midnight-9x16.png` | Midnight: the first theme a record unlocks |
| `store-screenshots/neon-9x16.png` | Neon — four ring colours in rotation |
| `store-screenshots/earth-9x16.png` | Earth, the last unlock, at a higher difficulty |
| `store-screenshots/menu-9x16.png` | The menu, showing the record and the theme in use |

Captured by code through the editor seams in `ScreenshotTool.cs`, not
played by hand. Each frame is a seeded game driven by real moves, so
every position shown is one the rules can actually produce. Regenerate
the whole set with **Hypnotic Rings → Art → Capture Screenshots**, or
headlessly with `-executeMethod HR.Editor.ScreenshotTool.CI`.

Seeds and move counts are defined at the top of `CaptureAll()` — edit
there, not in the images.

**What the set does not show:** the settings and About screens, and the pause and game-over cards. They are interface, not play, and a store listing that leads with a settings screen is selling the wrong thing. Store screenshots of
menus sell nothing.

Editor captures have no status bar, no cutout and no gesture bar. That
does not violate store rules, and it is not a substitute for the device
run in Phase 13.

## Additional locales

> Delete this section when shipping one locale. Otherwise repeat the
> four `###` blocks per locale, same headings, same fences. The icon and
> feature graphic are shared across locales; screenshots are per-locale
> when the interface carries text.

## Countries and languages

Default listing language **en-US**. The interface picks its language
from the system locale at startup and falls back to English for
anything not shipped, so releasing to all countries is safe — a player
on any locale gets a working interface rather than missing text.

Android does not localise the launcher label. It reads
**Hypnotic Rings** in every locale, which is intended, not a bug.
