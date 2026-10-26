# Store listing

> Template. The fenced blocks below are the **only** copy of this text.
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
- `art/screenshots/` — {{SHOT_COUNT}} frames, {{SHOT_SIZE}} each

## Default locale — {{LOCALE}}

### App name

```
{{STORE_NAME}}
```

### Short description

```
{{SHORT_DESCRIPTION}}
```

> One line: what the player does, plus the one thing that sets it apart.

### Full description

```
{{FULL_DESCRIPTION}}
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

## Release notes — {{LOCALE}}, {{VERSION_NAME}} ({{VERSION_CODE}})

The console presents this field already wrapped in `<en-US>` tags. Paste
the text between them and leave the tags alone; they are how the console
assigns notes to a language, not part of what players read.

Rewrite this block for each release — it describes one release, not the
app.

### Release notes

```
{{RELEASE_NOTES}}
```

No version number in the text. The console shows it beside the notes
already, and a number repeated by hand is a number that goes stale.

## Screenshots

Uploaded in this order:

| File | What is on the frame |
| --- | --- |
| `{{SHOT_FILE}}` | {{SHOT_DESCRIPTION}} |

Captured by code through the editor seams in `ScreenshotTool.cs`, not
played by hand. Each frame is a seeded game driven by real moves, so
every position shown is one the rules can actually produce. Regenerate
the whole set with **{{GAME_MENU}} → Art → Capture Screenshots**, or
headlessly with `-executeMethod {{NAMESPACE}}.Editor.ScreenshotTool.CI`.

Seeds and move counts are defined at the top of `CaptureAll()` — edit
there, not in the images.

**What the set does not show:** {{SHOTS_OMITTED}}. Store screenshots of
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

Default listing language **{{LOCALE}}**. The interface picks its language
from the system locale at startup and falls back to {{LANGUAGE}} for
anything not shipped, so releasing to all countries is safe — a player
on any locale gets a working interface rather than missing text.

Android does not localise the launcher label. It reads
**{{LAUNCHER_LABEL}}** in every locale, which is intended, not a bug.
