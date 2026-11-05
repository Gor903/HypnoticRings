# Hypnotic Rings — UI reference

What the player actually sees, with every colour, font size, dimension and
timing as it exists in the code today (branch `main`, commit `0c364af`).

Everything below was read out of the source, not out of the design docs.
Where the design docs disagree with the code, the code is what ships — those
mismatches are listed at the end.

> **Planned changes (not in the code yet):** §5.7 ads, §5.8 run-start countdown,
> the *planned* notes in §5.3, §5.4 and §5.6, the *planned* rows in §7.6, §12 and §13,
> and the sections marked *planned* — §15.0, §15.2, §15.2a, §15.3, §15.4, §15.6,
> §15.7, §17. Everything else is the shipped state of commit `0c364af`.
>
> **Revision 2 (5 Nov 2026):** reconciled with `HypnoticRings-Ads-Backend-Plan.md`.
> Pause / Game over re-budgeted (the Pause arithmetic was 16 units over), «Реклама»
> labels, ad ↔ card gaps raised to 64, load-state fallbacks, privacy-options row,
> About text, UMP consent step. **Read §5.7 first — it is the single source of truth
> for ad layout; §15.3 / §15.4 are its drawings.**
>
> **Revision 3 (5 Nov 2026):** new visual identity — one font pair, a new set of seven carousel
> themes (Minimal light/dark as one theme, Neon cosmos, Art deco, Sumi-e ink, Deep ocean, Bauhaus,
> Retro CRT) plus the Ember hardcore skin, backdrop effects, new unlock thresholds, save migration.
> **§18 is the single source of truth for fonts, palettes, effects and skins**; the *planned*
> notes in §1, §3, §5.5, §7.3, §8, §9, §10, §12, §13 and §15.5 only point to it. Where an older
> table (§7.3, §8, §9 thresholds) disagrees with §18, **§18 wins**.

---

## 1. Where each thing lives

| What you want to change | File |
|---|---|
| Font, sizes, touch target, control geometry | `Assets/_Project/Scripts/UI/UiBuilder.cs` |
| Screen layout (menu, HUD, pause, game over, themes, settings) | `Assets/_Project/Scripts/UI/GameUi.Screens.cs` |
| Theme application to UI, element roles, spacing constants | `Assets/_Project/Scripts/UI/GameUi.cs` |
| Theme colours | `Assets/_Project/Scripts/Core/ThemePalette.cs` |
| Per-theme UI shape (corners, outline, press, knob) | `Assets/_Project/Scripts/Core/UiSkin.cs` |
| Corner/outline/halo painting | `Assets/_Project/Scripts/UI/UiPainter.cs` |
| Switch, outline, press feedback behaviours | `Assets/_Project/Scripts/UI/UiControls.cs` |
| Accent "breathing" | `Assets/_Project/Scripts/UI/UiShimmer.cs` |
| Score pulse and screen fade | `Assets/_Project/Scripts/UI/ScreenJuice.cs` |
| Theme carousel snapping | `Assets/_Project/Scripts/UI/CarouselSnapping.cs` |
| Notch / gesture-bar handling | `Assets/_Project/Scripts/UI/SafeAreaFitter.cs` |
| All visible text (RU + EN) | `Assets/_Project/Scripts/Core/Strings.cs` |
| *(planned)* Fonts and licences | `Assets/_Project/Resources/Fonts/` (`Unbounded-Bold.ttf`, `Manrope-Medium.ttf`, OFL texts) — §18.2 |
| *(planned)* Font role → font mapping | `UiFonts` in `HR.UI`; `FontRole` enum in `HR.Core` — §18.2 |
| *(planned)* Backdrops (stars, sunburst, bubbles…) | new `Backdrop*.cs` in `HR.UI` — §18.4 |
| *(planned)* Legacy theme-id migration | `ThemeMigration` in `HR.Core` — §18.7 |
| Theme unlock thresholds | `Assets/_Project/Scripts/Core/ScoreKeeper.cs` |
| Contrast maths and thresholds | `Assets/_Project/Scripts/Core/Contrast.cs` |
| World-space numbers (ball, rings, effects) | `Assets/_Project/Config/GameConfig.asset` |

Nothing is a prefab or a scene asset — **the whole UI is built in code at
runtime**, so edits are C# edits, not Inspector edits.

---

## 2. Canvas and scaling

| Property | Value |
|---|---|
| Reference resolution | **1080 × 1920** (portrait) |
| Scale mode | Scale With Screen Size |
| Screen match | Match Width Or Height, **match = 0.5** |
| Render mode | Screen Space – Camera (plane distance 1) when a camera is passed; otherwise Screen Space – Overlay |
| Safe area | `SafeAreaFitter` reanchors a `SafeArea` child to `Screen.safeArea` every frame it changes; **all screens are children of it** |
| Event system | `InputSystemUIInputModule` |

All numbers in this document are in **canvas units at the 1080 × 1920
reference**, so on a 1080-wide phone 1 unit = 1 px.

---

## 3. Typography

**Font:** legacy Unity `UI.Text` with the built-in `LegacyRuntime.ttf`
(falls back to `Arial.ttf`). **Not TextMeshPro.** Cyrillic comes from the OS
font fallback.

| Role | Constant | Size | Auto label height (`ceil(size × 1.45)`) |
|---|---|---|---|
| Score (HUD + game over) | `ScoreSize` | **160** | 232 |
| Screen title | `TitleSize` | **86** | 125 |
| Button label | `ButtonSize` | **52** | 76 |
| Body / row label / hint / lock line | `BodySize` | **44** | 64 |

> **Planned:** two fonts replace `LegacyRuntime.ttf` — **Unbounded Bold** for score, titles, buttons and the countdown digit; **Manrope Medium** for body text, hints, labels and the ad label. Sizes and label bands in this table stay as they are; labels switch to best-fit so the wider font never changes the layout. See §18.2.

Defaults on every label: wrap horizontally, overflow vertically, colour white
before theming, `raycastTarget = false`, alignment `MiddleCenter` unless stated.

Text outline (`UnityEngine.UI.Outline`, drawn in the **theme background
colour**, `useGraphicAlpha = false`):

| Element | Outline distance |
|---|---|
| HUD score | **6** |
| HUD best | **4** |

---

## 4. Size and spacing tokens

| Token | Value | Used for |
|---|---|---|
| `TouchTarget` | **150** | minimum height of every button, toggle row, slider row, chip |
| Column padding | **64** on all four sides | menu, themes, settings, confirm, about columns |
| Column spacing | **28** menu / **24** themes / **16** settings / **28** about | vertical gaps |
| Row spacing (`CreateRow`) | **24**, height 150 | the two-button rows |
| `GroupGap` | **28** | spacer between settings groups |
| `RowInset` | **32** | left padding of a settings row label, right padding of its control |
| `CardMargin` | **56** | side margin of the pause / game-over card |
| `CardInset` | **72** | card lift off the bottom edge |
| Card padding | **40 / 40 / 48 / 48** (L/R/T/B), spacing **24** | |
| `CardOpacity` | **0.96** | card plate alpha |
| `CardWidth` × `CardHeight` | **600 × 700** | theme carousel card |
| `CarouselSpacing` | **32** | between theme cards |
| `PreviewSize` | **360** | ring preview on a theme card |
| `LevelWidth` | **420** | slider track width inside a settings row |
| `ChipWidth` | **320** | language chip |
| `TrackWidth` × `TrackHeight` | **132 × 72** | toggle switch track |
| `KnobInset` | **8** → knob **56 × 56** | toggle knob |
| `SliderTrackHeight` | **36** | |
| `SliderKnobSize` | **60** | |
| `SliderEndInset` | **32** | |
| `PauseButtonSize` / `PauseInset` | **150** / **28** | |
| `RoundedBorder` | **64** | 9-slice border of the generated rounded sprite |

Two sprites are generated procedurally at 128 × 128 and reused everywhere:
`HR.Disc` (antialiased filled circle) and `HR.Rounded` (same circle, sliced
with a 64-unit border so it stretches into a rounded rectangle).

---

## 5. Screens — what the player sees

Screens are all built up front and toggled by `GameUi.Show(state)`.
**Wireframes for every screen are in §15, control anatomy in §16, the
navigation map in §17.**

### 5.1 Main menu (`GameState.Menu`)

Full-screen plate at **alpha 0.88** in the theme background colour; the live
tunnel rotates behind it at **6 °/s**.

Centred vertical column, top to bottom:

| Element | Size | Notes |
|---|---|---|
| `Hypnotic Rings` title | 86 | theme ink colour |
| Best line (`Рекорд: N` / `Best: N`) | 44 | **hidden until best > 0** |
| **ИГРАТЬ / PLAY** | height **240** (150 × 1.6) | *primary* — accent fill, accent-text label |
| **ХАРДКОР / HARDCORE** | height 150 | surface fill; **disabled until normal best ≥ 30**, label alpha drops to **0.45** |
| Hardcore lock line | 44 | only while locked: "Рекорд в обычной игре 30" |
| Row: `Темы` \| `Настройки` | height 150, gap 24 | surface fill, equal widths |

### 5.2 HUD (`GameState.Playing`)

No background plate at all — the play field is never covered.

| Element | Position | Size |
|---|---|---|
| Score | pinned top, band height 220, inset **40** from the top | 160, theme ink, 6-unit background outline |
| Best | pinned top, band height 70, inset **270** | 44 at **alpha 0.6**, 4-unit outline; hidden when best is 0 |
| Pause button `II` | top-right, 150 × 150, centre at **−103, −103** from the corner (`150/2 + 28`) | label 52; its fill is forced to the **disc** sprite, so it is a circle in every theme |
| First-run hint | bottom band, **180 → 260** from the bottom edge | 44 |

The hint reads "Проведи пальцем по кругу" / "Swipe in a circle" in normal mode
and "Каждого кольца можно коснуться только раз" / "Each ring may be touched
only once" in hardcore. It disappears after the first scored ring and never
returns (persisted as `hintSeen` / `hardcoreHintSeen`).

### 5.3 Pause (`GameState.Paused`)

> **Planned:** the black 0.40 scrim becomes an opaque theme plate, the screen draws its own score, and a MREC + banner appear — see §5.7 and §15.3. The text below is the shipped state.

Frozen scene under a **flat black scrim at alpha 0.40** (this dim is *not*
themed). The HUD score stays visible underneath. Card anchored to the bottom,
56 margin each side, lifted 72:

`Пауза` (86) → `В меню` (150) → **`Продолжить`** (height **210** = 150 × 1.4, primary/accent).

### 5.4 Game over (`GameState.GameOver`)

> **Planned:** opaque plate, score and record move out of the card, MREC + banner + a rewarded-continue button appear — see §5.7 and §15.4. The text below is the shipped state.

Same black 0.40 scrim, same card:

| Element | Size |
|---|---|
| Final score | 160 |
| `НОВЫЙ РЕКОРД!` or `Рекорд N` | 44 |
| Unlock line `Открыта тема «…»` | 44, only when a theme unlocked this run — **never shown in hardcore** (`AwardsThemes = false`) |
| Row: `В меню` \| `Темы` | 150 |
| **`ЕЩЁ РАЗ`** | **210**, primary/accent |

### 5.5 Theme select (`GameState.ThemeSelect`)

> **Planned:** **seven** cards instead of six (Minimal, Neon cosmos, Art deco, Sumi-e ink, Deep ocean, Bauhaus, Retro CRT). The Minimal card is split into a light half and a dark half, each a tap target. Names, previews and thresholds: §18.3, §18.6, §18.7. The text below is the shipped state.

Plate at **alpha 0.92** in the theme background. Title `Темы` (86, forced to a
130-unit band), then a horizontal carousel **720 tall** (card 700 + 20) holding
**six cards, 600 × 700, 32 apart**.

Each card:

| Part | Geometry |
|---|---|
| Ring preview | 360 × 360, pinned top-centre, **40** down from the card top |
| Name label | bottom band **150 → 230**, side inset 16, size 52 |
| Unlock line | bottom band **60 → 130**, side inset 16, size 44 |

The preview is **static** and drawn in *its own* theme's colours, never the
current one: `rings.Length × 2 + 1` concentric discs, each scaled
`1 − i/bands`, alternating background / ring colour, plus a centre dot at
**14 %** of the card preview size in the ball colour.

Card states: chosen → fill = **Accent**, label = **AccentText**; otherwise fill
= **Surface**, label = ink. Locked → not interactable, label and lock line at
**alpha 0.45** / **0.7**; unlocked → lock line alpha 0.

Snapping: `CarouselSnapping` centres the card under the finger on release and
eases with `1 − e^(−16 Δt)`; side padding is recomputed so the first and last
card can centre. Opening the screen jumps straight to the current theme.

Below the carousel: `Назад` (150).

### 5.6 Settings (`GameState.Settings`)

Plate at **alpha 0.95**, scrollable column (spacing 16, padding 64, elastic,
scroll sensitivity 40). Rows top to bottom:

1. `Настройки` title (86)
2. `Звук` — switch
3. `Музыка` — switch
4. `Вибрация` — switch
5. *spacer 28*
6. `Чувствительность` — slider, **0.6 → 1.6**, default 1.0
7. `Инверсия вращения` — switch
8. *spacer 28*
9. `Снизить эффекты` — switch
10. `Язык` — chip, cycles **Авто → Русский → English → Авто**
11. *spacer 28*
12. Row: `Об игре` \| `Сбросить прогресс` — transparent link buttons, label 44
13. `Назад`

> **Planned (ads / consent):** a new row **`Параметры конфиденциальности` / `Privacy settings`**
> (`set.privacy`) — a *link* button (transparent fill, label 44), height 150, full row width,
> placed **between spacer 11 and the `Об игре | Сбросить прогресс` row**. It exists only when
> the UMP SDK reports `PrivacyOptionsRequirementStatus == Required`; otherwise it is not built
> into the layout at all (no gap left behind). Tap → `ShowPrivacyOptionsForm()`. Content height
> becomes 1879 → **2045** when the row is present (it scrolls even on 9:16); unchanged otherwise.
> The `Об игре` text also changes — see §12 and §15.7.

**Reset confirm** — full-screen plate at alpha **0.97**: the question
"Сбросить рекорд и настройки?" (44) then `Нет` then `Да`. `Нет` is listed
first deliberately.

**About** — full-screen plate at alpha **0.97**, scroll column: title (86),
`Application.version` (44), body text in a forced **340**-tall band, privacy
policy line, `Назад`.

### 5.7 Ads (planned)

Not in the code yet. Ads are **native views drawn over the Canvas** (never inside it).
Their real size comes from the SDK in pixels and is converted with
`units = px / canvas.scaleFactor`. At a 1080-px-wide screen (`scaleFactor` = 1) a
320 × 50 dp banner is 960 × 150 units and a 300 × 250 dp MREC is 900 × 750 units;
on other screens the numbers differ (a 20:9 phone has `scaleFactor` ≈ 1.12), so **every
height below that mentions an ad is a reference-screen figure — the layout is computed
from the real heights**.

| Slot | dp | Units (reference) | Shown in |
|---|---|---|---|
| Banner | 320 × 50, **fixed `AdSize.Banner`** (never adaptive) | 960 × 150 | **Paused, GameOver only** |
| MREC | 300 × 250 | 900 × 750, centred horizontally | Paused, GameOver |
| Rewarded | full screen | — | started from GameOver |

#### Rules

- **No ads while a run is live** (Countdown, Playing): AdMob discourages banners on a gameplay
  screen with continuous input, and a swipe that starts on a native view is swallowed.
  Menu, Themes, Settings never show ads either.
- **Gap between an ad and the card plate: 64** (`AdGap`) on both sides (MREC → card, card → banner).
  The nearest *button* is further still: 72 on Game over (card pad 8), 88+ on Pause.
  **Hard floor 48** (`AdGapMin`) — gaps shrink to it before the MREC is dropped (see *Squeeze order*).
- **Every ad slot carries a `Реклама` / `Ad` label** (`ad.label`, size 28 = `AdLabelSize`,
  band `ceil(28 × 1.45)` = **41**, ink colour at **α 0.6**, `MiddleLeft`, aligned to the **left
  edge of its ad**, `raycastTarget = false`). It sits **in the gap directly above the slot,
  touching the slot's top edge but never covering a single ad pixel**. No wording that invites
  a tap. The MREC label sits between the score block and the MREC; the banner label sits inside
  the 64-unit gap above the banner (it takes the lower 41 of that gap). The rewarded ad is
  full-screen and has no label of ours.
- **Pause and Game over plates are opaque** (theme background, alpha 1.0), not a black scrim
  over the frozen scene.
- **Banner position:** place it by explicit coordinates from the **bottom of the safe area**,
  *not* with `AdPosition.Bottom` (which hugs the physical screen edge, under the gesture bar).
  The layout frames in §15.3 / §15.4 are the safe area, so the banner is its last 150 units.
- **Rewarded continue:** one per run, normal and hardcore alike. The button is hidden when no ad
  is loaded or the continue was already used. On reward the run resumes with the **same score and
  the same tempo**, through the countdown in §5.8.
- **Layout is decided once, when the screen is shown.** A slot that is not loaded at that moment is
  treated as absent for this showing; a late load is ignored until the next showing (no layout
  jump under the player's finger).
- The Curtain fade does not dim native ads. The banner and MREC appear **after** the Curtain
  fade-in ends (0.35 s) and are hidden at the **start** of the fade-out.
- `about.body` currently says the game has no ads and works offline — it must be rewritten
  (§12), the privacy policy updated, and the UMP consent form added (§15.0, §17).

#### Planned tokens

| Token | Value | Meaning |
|---|---|---|
| `AdGap` | **64** | card-plate edge ↔ ad edge (target) |
| `AdGapMin` | **48** | hard floor, used only while squeezing |
| `AdLabelSize` | **28** (band 41) | `Реклама` label |
| `AdScoreBand` | **200** | forced height of the score label on Pause / Game over (natural 232; precedent: the 130-unit forced title band). Size stays 160; the text overflows vertically as always |
| Pause card padding | **24 / 24** (T/B) | was 48 / 48 |
| Game over card padding / spacing | **8 / 8**, spacing **16** | was spacing 24 |
| Game over flex minimum | **16** | smallest gap between the record line and the MREC label |

#### Layout algorithm (Pause and Game over)

The screen is stacked from both ends; whatever height is left over goes into **one flex gap**.

```
TOP BLOCK, pinned to the top      score band (200) [+ 24 + record line 64 on Game over]
FLEX GAP                          absorbs all spare height (Pause min 0, Game over min 16)
MREC BLOCK                        label 41 + MREC (real height)
AdGap (64)
CARD                              pinned to the BOTTOM block
AdGap (64)                        the banner label lives in the lower 41 of this gap
BANNER                            real height, last units of the safe area
```

- Banner **absent** (not loaded / ads off): the banner and its label vanish and the card's bottom
  edge sits `CardInset` (72) above the safe-area bottom, as today.
- Reward button **hidden** (Game over): the card shrinks by 150 + 16 = **166**; the extra height
  goes into the flex gap, the MREC stays 64 above the card.
- **Squeeze order** when the real heights do not fit the safe area: (1) flex gap → its minimum,
  (2) both `AdGap` → `AdGapMin` (48), (3) drop the MREC and use the **old layout** below.
  Never squeeze below 48, never shrink a button, never overlap a slot.
- **Old layout** (no MREC): the shipped card — score and record **inside** the card, `ЕЩЁ РАЗ` 210,
  card padding 40/40/48/48, spacing 24, plus the reward button when allowed (order top to bottom:
  score, record, [unlock line], reward, `В меню | Темы`, `ЕЩЁ РАЗ`). Pause: score band at the top,
  card with the shipped 629-unit anatomy. The banner and its label still apply.
- **Old layout is used when:** the MREC does not fit even at the floor gaps; the MREC is not
  loaded at show time (no network, no consent, no fill); ads are off; **or the run unlocked a theme**
  (the `Открыта тема «…»` line lives in the old card — Game over only).

Minimum safe-area heights at the reference scale: Pause **1890** (floor gaps: 1858);
Game over with the reward button **1911** (floor: 1879), without it **1745**.

### 5.8 Run start — countdown (planned)

Every run begins with a countdown, in **normal and hardcore**, and the same
sequence is replayed after a rewarded continue.

| Step | What the player sees |
|---|---|
| Rings | all rings **frozen** |
| Ball | circles **inside a ring with no gaps** (the *solid ring*) — it cannot be lost there |
| Solid ring | a **separate extra ring**, closer to the centre than every other ring (see geometry below) |
| Digit | `3` → `2` → `1`, **1.0 s** each, centred on the tunnel centre (y 538), size **240**, theme ink, outline in the background colour |
| End | digit disappears, the solid ring **dissolves (0.25 s)**, rings start moving, input goes live |
| HUD | score and best visible; **pause button hidden**; the first-run hint may show; **no ads** |
| Input | ignored during the countdown |

Geometry of the solid ring (given as an example in proportions, not final
numbers): the solid ring sits at radius **10**, the real rings start further
out at **15, 20, 25, …**. So the solid ring is roughly **⅔ of the nearest
ring's radius**, and nothing else lies between it and the centre. The final
ratio goes into `GameConfig.asset`.

After a **rewarded continue** the same sequence runs, but the score is the one
the run died with, the ring speed and spawn tempo are the ones it died with,
and the rings beyond the solid ring are where they were. The ring that killed
the player is gone — the solid ring takes the safe place near the centre.

*Reduce effects* swaps the digits with no scale animation. New token:
`CountdownSize` **240** (band `ceil(240 × 1.45)` = 348 → y 364–712, so it
clears the best line, which ends at 340).

**Decided:**
1. The solid ring is its own ring at a smaller radius than all the others
   (example above), not a re-use of the ring that killed the player.
2. Hardcore, after a continue: same logic — the fatal ring is removed, every
   other ring keeps its "already touched" flag exactly as at death; the solid
   ring is new and untouched.
3. Best / unlock: left as is, **no de-duplication**. `НОВЫЙ РЕКОРД!` and the
   unlock line are evaluated at every death, so after a continue they can
   appear again on the second game over.

**Still open:** the exact radius ratio of the solid ring (one number in
`GameConfig.asset`).

---

## 6. Controls

### Button
150 tall by default, label 52 centred and stretched over the fill.
Two variants:
- **primary** — fill = `Accent`, label = `AccentText`, gets a halo when the theme glows;
- **ordinary** — fill = `Surface`, label = theme ink.
`Link` is the third form: the same button with a **fully transparent fill** and
a 44 label.

### Toggle (switch)
Row 150 tall. Label is 44, left-aligned, inset 32 from the left and stopping
180 from the right. Track **132 × 72** pinned to the right edge at −32. Knob
**56 × 56**, snapped to the left or the right end of the track.
Off → `TrackOff`; on → `TrackOn` (= Accent). Knob is always the **background
colour**, so it reads as a hole in the track.

### Slider
Row 150 tall, track 420 + 2 × 32 wide, **36** tall, knob **60 × 60**.
Track = `TrackOff`, fill = `Accent`, knob = `Accent`.

### Chip
A 320 × 150 button pinned right in a settings row; shows the current value
(`Авто` / `Русский` / `English`) and cycles on tap.

### Card plate
White image at alpha **0.96**, recoloured to the theme background, corner
radius computed against a 150-unit reference height.

---

## 7. Colour system

### 7.1 What a theme declares

```
Background, RingColours[], Ball, BallOutline, Ui,
DepthFade, Glow, ShimmerSeconds, ShimmerDepth, TrailHot, TrailCool
```

### 7.2 What the UI derives from it

| Role | Rule |
|---|---|
| `Accent` | `RingColours[0]` |
| `AccentText` | whichever of Background / Ui contrasts better against Accent |
| `Surface` | Background mixed toward Ui from **14 %**, stepping **2 %** until contrast vs Background ≥ **1.25**, capped at 50 % |
| `TrackOff` | same, starting at **22 %** |
| `Outline` | same, starting at **45 %** |
| `SurfaceText` | = `Ui` |
| `TrackOn` | = `Accent` |

Contrast is WCAG relative luminance; the required ratio for a theme to be
valid is **3.0** and the surface-visibility floor is **1.25**.

### 7.3 Declared palettes

> **Planned:** the palette set below is replaced by the one in §18.3 (Monochrome and Midnight become Minimal light/dark, Neon becomes Neon cosmos, Pastel / Earth / Aurora are retired and mapped by §18.7, Ember is unchanged). The derived-colour rules in §7.2 stay exactly as they are.

| Theme | Background | Ring cycle | Ball | Ball outline | UI ink | DepthFade | Glow | Shimmer |
|---|---|---|---|---|---|---|---|---|
| **Monochrome** | `#FAFAFA` | `#111111` | `#111111` | `#FAFAFA` | `#111111` | 0.55 | 0.00 | — |
| **Midnight** | `#0B1020` | `#E8ECF4` | `#FFB703` | `#0B1020` | `#E8ECF4` | 0.60 | 0.15 | — |
| **Neon** | `#121A33` | `#2D7FF0`, `#FFEC6B`, `#2D7FF0`, `#8B3FF0` | `#C77DF2` | `#121A33` | `#FFFFFF` | 0.50 | 0.60 | 2.4 s, depth 0.10 |
| **Pastel** | `#FDF6F0` | `#B97E7E`, `#6F91A3`, `#76937C`, `#9E8B65` | `#5C5470` | `#FDF6F0` | `#5C5470` | 0.50 | 0.00 | — |
| **Earth** | `#EADDC8` | `#3F5E3A`, `#6B4F3A`, `#8A7A52` | `#C2562B` | `#EADDC8` | `#3B2F26` | 0.50 | 0.00 | — |
| **Aurora** | `#06121A` | `#3FE0A8`, `#5BC8F5`, `#9B6BF0` | `#F2F7FF` | `#06121A` | `#DFF5EC` | 0.55 | 0.45 | 3.6 s, depth 0.14 |
| **Ember** | `#140604` | `#FF7A1A`, `#FFB02E`, `#E8431F` | `#FFF4D9` | `#140604` | `#FFE4CC` | 0.50 | 0.70 | — (trail `#FFF4D9` → `#8A1403`) |

### 7.4 Derived UI colours (computed from the rules above)

| Theme | Surface | TrackOff | Outline | Accent | AccentText |
|---|---|---|---|---|---|
| Monochrome | `#D9D9D9` | `#C7C7C7` | `#919191` | `#111111` | `#FAFAFA` |
| Midnight | `#2A2F3E` | `#3C404F` | `#6E737F` | `#E8ECF4` | `#0B1020` |
| Neon | `#333A50` | `#464C60` | `#7D818F` | `#2D7FF0` | `#121A33` |
| Pastel | `#E3DCDC` | `#DAD2D4` | `#B5ADB6` | `#B97E7E` | `#FDF6F0` |
| Earth | `#D2C5B1` | `#C4B7A4` | `#9B8F7F` | `#3F5E3A` | `#EADDC8` |
| Aurora | `#243237` | `#364448` | `#687878` | `#3FE0A8` | `#06121A` |
| Ember | `#352520` | `#483730` | `#7E6A5E` | `#FF7A1A` | `#140604` |

(Pastel's Surface lands at 16 % rather than 14 % — the loop had to take one
extra step to clear the 1.25 floor against its near-white background. Every
theme's AccentText resolves to the background colour.)

### 7.5 Current contrast ratios against the background

| Theme | Ring 0 | Ball | UI ink |
|---|---|---|---|
| Monochrome | 18.09 | 18.09 | 18.09 |
| Midnight | 15.99 | 10.84 | 15.99 |
| Neon | 4.43 | 6.26 | 17.20 |
| Pastel | **3.09** | 6.63 | 6.63 |
| Earth | 5.45 | **3.36** | 9.67 |
| Aurora | 11.21 | 17.60 | 16.60 |
| Ember | 7.61 | 18.15 | 16.28 |

Pastel's rings and Earth's ball are the two values closest to the 3.0 floor —
if you darken a background or lighten a ring, those are the ones that will
trip the contrast test first.

### 7.6 Screen plate opacities

| Screen | Alpha | Tinted to theme? |
|---|---|---|
| Menu | 0.88 | yes |
| Themes | 0.92 | yes |
| Settings | 0.95 | yes |
| Reset confirm | 0.97 | yes |
| About | 0.97 | yes |
| Card plate (pause / game over) | 0.96 | yes |
| Pause & game over dim *(shipped; removed by the planned opaque plate)* | **0.40 black** | **no — always black** |
| Pause & game over plate *(planned)* | **1.0 (opaque)** | yes |
| HUD | none | — |

---

## 8. Per-theme UI skin

> **Planned:** new skins for the new theme set, including a chamfered shape (Art deco) and a hard drop shadow (Bauhaus) — §18.5. The tables below are the shipped state.

Shape is a theme property too, not just colour (`UiSkin.For`).

| Theme | Corner radius (× control height) | Outline width | Press effect | Knob |
|---|---|---|---|---|
| Monochrome | 0.10 | 3.0 | Sink | **Square** |
| Midnight | 0.25 | 2.0 | Flash | Round |
| Neon | 0.30 | 4.0 | Flash | Round |
| Pastel | 0.50 | **0.0** (no outline) | Sink | Round |
| Earth | 0.20 | 2.0 | Sink | Round |
| Aurora | 0.35 | 3.0 | **Ripple** | Round |
| Ember | 0.15 | 3.0 | Flash | Round |

Corner radius is a **fraction of the control's height**, clamped to half of it,
and anything below 1 unit falls back to a plain square sprite.

Resolved radii on a 150-tall button:

| Theme | 150-tall button | 72-tall toggle track | 36-tall slider track |
|---|---|---|---|
| Monochrome | 15 | 7.2 | 3.6 |
| Midnight | 37.5 | 18 | 9 |
| Neon | 45 | 21.6 | 10.8 |
| Pastel | **75** (pill, at the clamp) | **36** (pill) | **18** (pill) |
| Earth | 30 | 14.4 | 7.2 |
| Aurora | 52.5 | 25.2 | 12.6 |
| Ember | 22.5 | 10.8 | 5.4 |

**Press feedback** (`PressFeedback`, recovery **0.10 s** on unscaled time):
- **Sink** — the control moves **7 units** down and springs back.
- **Flash** — the fill lerps **35 %** toward white and back.
- **Ripple** — a disc grows from the exact touch point inside a `RectMask2D`,
  radius reaching `2 × |half-size + offset|`, at alpha **0.30** fading out.

**Edge and halo** (`UiChrome`, both `UnityEngine.UI.Outline` components):
- Edge: width = the skin's outline width, colour = `Outline`.
- Halo: only on accent graphics, only when *Reduce effects* is off —
  reach = `glow × 9`, alpha = `glow × 0.38`.

| Theme | Halo reach | Halo alpha |
|---|---|---|
| Midnight | 1.35 | 0.057 |
| Neon | 5.40 | 0.228 |
| Aurora | 4.05 | 0.171 |
| Ember | 6.30 | 0.266 |
| Monochrome / Pastel / Earth | none | none |

**Accent breathing** (`UiShimmer`): every accent graphic lerps toward the
**background colour** by `depth × (sin(2π t / seconds) + 1) / 2`, alpha
preserved. Only Neon (2.4 s / 0.10) and Aurora (3.6 s / 0.14) breathe.
Turning on *Reduce effects* sets the period to 0 and restores resting colours.

---

## 9. Themes: unlocking, and the hardcore look

> **Planned:** new thresholds (0 / 0 / 25 / 50 / 75 / 100 / 150) and the legacy-id migration are in §18.7. Ember stays the hardcore-only skin.

| Theme | Required best score |
|---|---|
| Monochrome, Midnight, Neon | 0 — available from the start |
| Pastel | **25** |
| Earth | **50** |
| Aurora | **100** |
| **Ember** | never by score |

`Ember` is **not in the carousel** — `ThemePalette.All` lists six themes,
`ThemePalette.Every` lists all seven. Ember is the **hardcore skin**: during a
hardcore run the entire look (world *and* UI) switches to Ember, and reverts to
the player's chosen theme the moment the run ends
(`GameBootstrap.LookFor`).

Hardcore itself unlocks at **normal best ≥ 30** (`GameUi.HardcoreUnlockBest`),
keeps a **separate record** (`hardcoreBest`), and awards no themes.

A theme switch applies live — mid-run is fine, nothing reloads, and no physical
parameter changes (enforced by test).

---

## 10. Motion and effects

> **Planned:** every theme gets a *backdrop* (decorative layer behind the rings) with strict motion limits — §18.4. *Reduce effects* switches the backdrop off completely.

| Effect | Value |
|---|---|
| Score pulse | scale **1.0 → 1.15 → 1.0** over **0.15 s** |
| Screen fade (menu ↔ game) | **0.35 s**, curtain in the **theme background colour**, smoothstep |
| Menu tunnel rotation | **6 °/s** |
| Carousel snap easing | `1 − e^(−16 Δt)`, unscaled |
| Ball trail | **8** points, each fading over **0.22 s**, radius **0.55 → 0.18** of the ball, colour `TrailHot → TrailCool`, max alpha 0.5 |
| Pass flash | **0.12 s**, disc at **1.6 ×** ball radius growing a further **1.0 → 1.8 ×** |
| Crush camera shake | amplitude **0.08** world units over **0.4 s** |
| Tunnel collapse on death | scale to **0.82** over **0.4 s** around the ball |
| Ring dissolve | **0.25 s** |
| Countdown digit *(planned)* | **1.0 s** each, 3 → 2 → 1, then the solid ring dissolves (0.25 s) |
| Haptics | pass **20 ms**, loss **60 ms** |

*Reduce effects* switches off: trail, flashes, shake, collapse, score pulse,
screen fade, halos and breathing — all in one flag.

---

## 11. What is behind the UI (world space)

| Thing | Value |
|---|---|
| Camera | orthographic, **static**, ortho size **10** |
| Tunnel centre on screen | **72 %** up from the bottom |
| Ring thickness | **0.18 u** — visual equals physical, in every theme |
| Ball | disc radius **0.27 u** (0.54 across) with a **0.06 u** outline in the background colour |
| Red centre circle | radius **0.75 u**, `#FF3B30` mixed **45 %** over the theme background |
| Ring halo | width = `0.18 × glow`, colour = background lerped toward the ring colour by `lerp(0.2, 0.5, glow)` |
| Depth fade | nearest two rings at full alpha, the rest fade by distance, scaled by the theme's `DepthFade` (0.50–0.60) |
| Rings on screen | up to 9 ahead, out to radius 15.2 |

---

## 12. Every string

Default language is the system locale, fallback English. Both tables are
checked for completeness and matching `{0}` slots by a test.

| Key | Russian | English |
|---|---|---|
| `app.title` | Hypnotic Rings | Hypnotic Rings |
| `menu.play` | ИГРАТЬ | PLAY |
| `menu.hardcore` | ХАРДКОР | HARDCORE |
| `menu.themes` | Темы | Themes |
| `menu.settings` | Настройки | Settings |
| `menu.best` | Рекорд: {0} | Best: {0} |
| `hud.best` | Рекорд {0} | Best {0} |
| `hint.swipe` | Проведи пальцем по кругу | Swipe in a circle |
| `hardcore.hint` | Каждого кольца можно коснуться только раз | Each ring may be touched only once |
| `hardcore.locked` | Рекорд в обычной игре {0} | Best {0} in the normal game |
| `pause.title` | Пауза | Paused |
| `pause.resume` | Продолжить | Resume |
| `pause.home` | В меню | Home |
| `over.newbest` | НОВЫЙ РЕКОРД! | NEW BEST! |
| `over.best` | Рекорд {0} | Best {0} |
| `over.retry` | ЕЩЁ РАЗ | RETRY |
| `over.home` | В меню | Home |
| `over.themes` | Темы | Themes |
| `over.unlocked` | Открыта тема «{0}» | Theme “{0}” unlocked |
| `over.continue` *(planned)* | ▶ Продолжить · реклама | ▶ Continue · ad |
| `over.touched` | Второе касание кольца № {0} | Second touch on ring {0} |
| `themes.title` | Темы | Themes |
| `theme.locked` | Рекорд {0} | Best {0} |
| `theme.monochrome` | Монохром | Monochrome |
| `theme.midnight` | Полночь | Midnight |
| `theme.neon` | Неон | Neon |
| `theme.pastel` | Пастель | Pastel |
| `theme.earth` | Земля | Earth |
| `theme.aurora` | Сияние | Aurora |
| `theme.*` *(planned)* | see §18.8 — replaces the six rows above | |
| `set.title` | Настройки | Settings |
| `set.sound` | Звук | Sound |
| `set.music` | Музыка | Music |
| `set.vibration` | Вибрация | Vibration |
| `set.sensitivity` | Чувствительность | Sensitivity |
| `set.invert` | Инверсия вращения | Invert rotation |
| `set.lowfx` | Снизить эффекты | Reduce effects |
| `set.language` | Язык | Language |
| `set.lang.auto` | Авто | Auto |
| `set.lang.ru` | Русский | Русский |
| `set.lang.en` | English | English |
| `set.about` | Об игре | About |
| `set.reset` | Сбросить прогресс | Reset progress |
| `set.reset.confirm` | Сбросить рекорд и настройки? | Reset best score and settings? |
| `about.body` *(code today — becomes false with ads)* | Игра не собирает данные, не содержит рекламы и покупок и работает без интернета. | This game collects no data, has no ads or purchases, and works offline. |
| `about.body` *(planned replacement)* | Игра показывает рекламу и использует интернет для её загрузки. Без сети игра работает, но без рекламы. | This game shows ads and uses the internet to load them. Without a connection the game still works, just without ads. |
| `ad.label` *(planned)* | Реклама | Ad |
| `set.privacy` *(planned)* | Параметры конфиденциальности | Privacy settings |
| `about.privacy` | Политика конфиденциальности | Privacy policy |
| `common.yes` / `common.no` / `common.back` | Да / Нет / Назад | Yes / No / Back |

There is **no string for the Ember theme name** — it never appears in the
carousel, so it is never asked for. Add `theme.ember` if you ever surface it.

---

## 13. What the tests will stop you from breaking

Useful to know before you change colours or shapes — `tools/run_tests.sh cheap`
covers all of these:

- Every theme defines every palette entry, and ball / ring / UI all clear **3:1**
  against the background.
- A corner radius never exceeds half a control's height.
- Dark themes must **not** sink on press (Midnight and Neon flash, Aurora
  ripples) while the light ones must (Monochrome and Pastel sink).
- Pastel must stay outline-free, at corner radius 0.5.
- The themes must not all look the same — more than three distinct corner radii
  across the six.
- An unknown theme throws rather than silently falling back.
- A shimmer only ever makes a ring **easier** to see, and nothing pulses faster
  than **1.2 s** (`MinShimmerSeconds`).
- Accent ≠ ordinary surface; switch-on ≠ switch-off; outline is visible but not
  shouting; every role is a real colour in every theme.
- No label overlaps another and nothing runs off the edge or under a notch, on
  any aspect ratio.
- Every theme paints every screen without leaving a hole.
- A theme cannot change ring thickness, and hardcore's look never appears in
  the carousel.
- `HR.Core` must not reference `UnityEngine` — so pure colour/shape logic lives
  in `Core` and anything touching `UI.Text` lives in `HR.UI`.
- *(planned, ads)* The layout test is extended with ad slots and runs on every aspect ratio, for
  every combination (MREC present / absent × banner present / absent × reward button shown /
  hidden × theme unlocked): no slot overlaps another slot, a button or a label; **every gap between
  an ad slot and the card plate is ≥ `AdGapMin` (48), and = `AdGap` (64) whenever the screen is tall
  enough**; the `Реклама` label's rect touches but never intersects its ad's rect; everything stays
  inside the safe area; the flex gap is never negative.
- *(planned, ads)* The squeeze order of §5.7 is a pure function in `HR.Core` (real slot heights and
  safe-area height in, layout out) and is unit-tested without Unity.
- *(planned, visual identity)* The test list in §18.10 **replaces** the theme-specific items above
  that name Pastel, Midnight, Aurora or the six-theme corner-radius rule.

---

## 14. Where the design docs are out of date

`docs/HYPNOTIC_RINGS_GAMEPLAY_AND_VISUALS.md` §14 describes an earlier build.
Differences, so you don't chase them:

| Doc says | Code does |
|---|---|
| Play button 210 tall | **240** (150 × 1.6) |
| Pause inset 32 | **28** |
| Carousel cards 720 tall | card **700**, viewport 720 |
| Settings has sound-volume and music-volume sliders | **no volume sliders** — only Sound / Music / Vibration switches, then one Sensitivity slider |
| Settings order: … Чувствительность → Инверсия → Вибрация → Снизить эффекты | Вибрация sits **third**, right after Музыка |
| No hardcore entry in the menu | **ХАРДКОР** button, unlocked at best 30 |
| Six themes | **seven** palettes exist; Ember is the hardcore-only skin |
| Planned palettes (Midnight `#060A18`, Neon `#19F0FF`, Aurora `#070812`, …) | **not applied** — the live values are the table in §7.3 |
| Ring/background contrast threshold 4.5:1 | still **3.0** in `Contrast.Required` |
| `glow` and `depthFade` unused | both **are** used now (ring halo and depth fade) |
| `endCaps`, `segmentSeams` theme params | **removed** |

Also stale: `Assets/_Project/Config/GameConfig.asset` still carries
`superDissolveCount`, `superWaveStagger`, `superSlowFactor`,
`superSlowSeconds`, `superSlowReturnSeconds` — none of those fields exist in
`GameConfig.cs` any more, so Unity ignores them.

---

---

## 15. Screen schematics

Every frame below is the **safe area** at the 1080 × 1920 reference.
Numbers on the right are canvas units; `↕` is a layout gap, `↔` a row gap.
`ACCENT` marks the one accent-filled control per screen.

### 15.0 Layer stack  *(planned — ads and consent added)*

```
Canvas  1080×1920 · Screen Space – Camera · plane distance 1
 ├─ SafeArea ············· reanchored to Screen.safeArea on every change
 │   ├─ Menu ........ plate 0.88 .............. GameState.Menu
 │   ├─ Hud ......... no plate ................ Countdown · Playing
 │   ├─ Pause ....... OPAQUE plate 1.0 + card . Paused  (draws its own score)
 │   ├─ GameOver .... OPAQUE plate 1.0 + card . GameOver
 │   ├─ Themes ...... plate 0.92 .............. ThemeSelect
 │   └─ Settings .... plate 0.95 .............. Settings
 │        ├─ ConfirmReset . plate 0.97 ........ overlay, hidden by default
 │        └─ About ........ plate 0.97 ........ overlay, hidden by default
 └─ Curtain ·············· ScreenJuice fade, last sibling, on the CANVAS
                           and not the safe area — it covers the notch too

NATIVE VIEWS ············· drawn OVER Unity, NOT in the Canvas
 ├─ UMP consent form ··· SDK-owned dialog, shown over the Menu at launch when required
 │                       (§17); it never blocks the Menu from being built or used
 ├─ Banner ······· Paused · GameOver ONLY   (never while a run is live,
 │                                            never in Menu / Themes / Settings)
 │                 placed by coordinates from the bottom of the SAFE AREA
 ├─ MREC ········· Paused · GameOver
 ├─ «Реклама» labels ··· regular Canvas Text, one above each banner / MREC (§5.7);
 │                       they live in the Pause / GameOver screens, not on the native views
 └─ Rewarded ····· full-screen, started by «▶ Продолжить · реклама» on GameOver
                   the Curtain fade does NOT dim the banner / MREC: show them AFTER the
                   fade-in ends, hide them at the START of the fade-out
```

Only one screen is active at a time. The Hud no longer stays under Pause:
the opaque Pause plate covers it, so Pause draws the score itself.

### 15.1 Main menu

```
┌──────────────────────────────────────────────┐   plate: theme bg @ 0.88
│                                              │   (live tunnel turning behind, 6 °/s)
│ ↑ 64 padding                                 │
│                                              │
│                Hypnotic Rings                │   title 86 · band 125
│                     ↕ 28                     │
│                  Рекорд: 42                  │   body 44 · band 64
│                                              │      hidden while best = 0
│                     ↕ 28                     │
│  ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓  │
│  ┃                 ИГРАТЬ                 ┃  │   h 240 = 150×1.6  ·  ACCENT
│  ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛  │      fill Accent, label AccentText
│                     ↕ 28                     │
│  ┌────────────────────────────────────────┐  │
│  │                ХАРДКОР                 │  │   h 150 · surface
│  └────────────────────────────────────────┘  │      label α 0.45 while locked
│                     ↕ 28                     │
│           Рекорд в обычной игре 30           │   body 44 · only while locked
│                     ↕ 28                     │
│  ┌──────────────────┐    ┌────────────────┐  │
│  │       Темы       │↔24 │   Настройки    │  │   h 150 · row gap 24
│  └──────────────────┘    └────────────────┘  │
│                                              │
│ ↓ 64 padding                                 │
└──────────────────────────────────────────────┘
```

Column is `MiddleCenter`, so the whole stack (793 of content + 140 of gaps
= **933**) is centred: it starts ≈ 493 from the top on a 9:16 screen.

### 15.2 HUD — playing  *(unchanged; ads deliberately absent)*

```
┌──────────────────────────────────────────────┐   no plate at all · NO ADS in this state
│                                  ╭────╮      │   ↕ 28 inset, 150×150 circle
│                 12               │ II │      │   score 160 · band 220 · ↕ 40
│                                  ╰────╯      │
│                  Рекорд 37                   │   body 44 @ α 0.6 · ↕ 270 · hidden while best = 0
│                                              │
│                                              │
│                ·  ·  ·  ·  ·                 │   the play field, never covered
│              (  rings + ball  )              │
│                ·  ·  ·  ·  ·                 │
│                                              │
│                                              │
│                                              │
│           Проведи пальцем по кругу           │   body 44 · band 180 → 260 ↑  (unchanged — no banner here)
│                                              │
└──────────────────────────────────────────────┘
```

Score and best both carry an `Outline` in the **background colour** — 6
units on the score, 4 on the best — so they stay legible over any ring.

No ad is shown while a run is live: AdMob discourages banners on a gameplay
screen with continuous input, and a swipe that started on a native ad view
would be swallowed. Banner and MREC appear only on Pause and Game over.

### 15.2a Run start — countdown  *(planned)*

Shown at the start of **every** run (normal and hardcore) and again after a
rewarded continue. The vertical positions are schematic; the real tunnel
centre is at y 538. No banner here (a rewarded continue hides the banner
for the whole countdown).

```
┌──────────────────────────────────────────────┐   no plate · pause button HIDDEN · NO ADS
│                                              │
│                      0                       │   score 160 · 0 on a NEW run, KEPT (e.g. 47) on a continue
│                                              │
│                                              │
│                  Рекорд 37                   │   body 44 @ α 0.6 · ↕ 270 · same as the HUD
│                                              │
│            ·    ·    ·    ·    ·             │   outer rings: FROZEN
│               ╭──────────────╮               │   the SOLID ring — no gaps, frozen, safe
│             ╭─╯              ╰─╮             │
│             │        3         │             │   countdown digit 240 · centre y 538 (72 % up)
│             ╰─╮              ╭─╯             │   3 → 2 → 1 · 1.0 s each
│               ╰──●───────────╯               │   ball ● circles INSIDE the solid ring
│            ·    ·    ·    ·    ·             │
│                                              │
│                                              │
│           Проведи пальцем по кругу           │   first-run hint (normal / hardcore text)
│                                              │
└──────────────────────────────────────────────┘
```

```
t = 0.0 s   state Countdown · rings frozen · ball orbits in the solid ring · "3"
t = 1.0 s   "2"
t = 2.0 s   "1"
t = 3.0 s   digit gone · solid ring DISSOLVES (0.25 s, existing ring dissolve)
t = 3.25 s  state Playing · rings start moving · input is live
```

### 15.3 Pause  *(planned — MREC + banner)*

```
┌──────────────────────────────────────────────┐   OPAQUE plate = theme bg @ 1.0 — frozen scene is hidden
│                      12                      │   score 160 · band FORCED 200 · y 40 → 240 · drawn by the Pause screen
│                                              │   flex gap 30 (absorbs spare height · min 0) · y 240 → 270
│    Реклама                                   │   ad label 28 · band 41 · y 270 → 311 · left edge of the ad
│    ╔════════════════════════════════════╗    │   AD · 900×750 · y 311 → 1061
│    ║                                    ║    │
│    ║                                    ║    │
│    ║          MEDIUM RECTANGLE          ║    │
│    ║             300×250 dp             ║    │
│    ║                                    ║    │
│    ║                                    ║    │
│    ╚════════════════════════════════════╝    │
│                                              │   ↕ 64 AdGap — ad ↔ card plate
│  ╭────────────────────────────────────────╮  │   card y 1125 → 1706 · 581 high
│  │                                        │  │   pad top 24
│  │                 Пауза                  │  │   title 86 · band 125
│  │                                        │  │   ↕ 24
│  │ ┌────────────────────────────────────┐ │  │   h 150 · surface
│  │ │               В меню               │ │  │
│  │ └────────────────────────────────────┘ │  │
│  │                                        │  │   ↕ 24
│  │ ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓ │  │   h 210 · ACCENT
│  │ ┃             Продолжить             ┃ │  │
│  │ ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛ │  │
│  │                                        │  │   pad bottom 24
│  ╰────────────────────────────────────────╯  │
│                                              │   ↕ 64 AdGap — card ↔ banner
│  Реклама                                     │   banner label · y 1729 → 1770 · lives inside the 64 gap
│  ╔════════════════════════════════════════╗  │   BANNER 320×50 dp · 960×150 · y 1770 → 1920
│  ║            BANNER 320×50 dp            ║  │
│  ╚════════════════════════════════════════╝  │
└──────────────────────────────────────────────┘
```

The plate is the theme background at **alpha 1.0** (it was a black 0.40 scrim): AdMob does not
want an ad hovering over a background that covers app content. All numbers are the 1080 × 1920
reference; the real ad heights come from the SDK (§5.7).

**Vertical budget, top to bottom:** 40 inset + 200 score + **30 flex** + 41 label + 750 MREC +
64 gap + 581 card + 64 gap + 150 banner = **1920**. The card is 24 + 125 + 24 + 150 + 24 + 210 + 24 = 581
(shipped card is 629: only the top/bottom padding shrinks from 48 to 24). Minimum with the flex at 0:
**1890**; with both gaps at the 48 floor: **1858**.

Fallbacks follow §5.7: no MREC → shipped 629-unit card pinned above the banner (or `CardInset` 72
when there is no banner); no banner → no banner label, card 72 from the bottom.


### 15.4 Game over  *(planned — MREC, reward button, banner)*

```
┌──────────────────────────────────────────────┐   OPAQUE plate = theme bg @ 1.0 — frozen scene is hidden
│                      47                      │   score 160 · band FORCED 200 · y 40 → 240 — OUT of the card
│                                              │   ↕ 24
│                НОВЫЙ РЕКОРД!                 │   body 44 · band 64 · or «Рекорд N» · y 264 → 328
│                                              │   flex gap 25 (min 16) · y 328 → 353
│    Реклама                                   │   ad label 28 · band 41 · y 353 → 394
│    ╔════════════════════════════════════╗    │   AD · 900×750 · y 394 → 1144
│    ║                                    ║    │
│    ║                                    ║    │
│    ║          MEDIUM RECTANGLE          ║    │
│    ║             300×250 dp             ║    │
│    ║                                    ║    │
│    ║                                    ║    │
│    ╚════════════════════════════════════╝    │
│                                              │   ↕ 64 AdGap — ad ↔ card plate
│  ╭────────────────────────────────────────╮  │   card y 1208 → 1706 · 498 high
│  │                                        │  │   pad 8
│  │ ┌────────────────────────────────────┐ │  │   h 150 · REWARD · see §5.7
│  │ │      ▶  Продолжить · реклама       │ │  │
│  │ └────────────────────────────────────┘ │  │
│  │                                        │  │   ↕ 16
│  │ ┌────────────────┐  ┌────────────────┐ │  │   h 150 · row gap 16
│  │ │     В меню     │  │      Темы      │ │  │
│  │ └────────────────┘  └────────────────┘ │  │
│  │                                        │  │   ↕ 16
│  │ ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓ │  │   h 150 (was 210) · ACCENT
│  │ ┃              ЕЩЁ РАЗ               ┃ │  │
│  │ ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛ │  │
│  │                                        │  │   pad 8
│  ╰────────────────────────────────────────╯  │
│                                              │   ↕ 64 AdGap — card ↔ banner
│  Реклама                                     │   banner label · y 1729 → 1770 · lives inside the 64 gap
│  ╔════════════════════════════════════════╗  │   BANNER 320×50 dp · 960×150 · y 1770 → 1920
│  ║            BANNER 320×50 dp            ║  │
│  ╚════════════════════════════════════════╝  │
└──────────────────────────────────────────────┘
```

**Vertical budget, top to bottom:** 40 inset + 200 score + 24 + 64 record + **25 flex** + 41 label +
750 MREC + 64 gap + 498 card + 64 gap + 150 banner = **1920**. The card is 8 + 150 + 16 + 150 + 16 +
150 + 8 = 498. Minimum with the flex at 16: **1911**; with both gaps at the 48 floor: **1879**.
Gap ad ↔ nearest button: **72** above the card (reward button, card pad 8) and **72** below it
(`ЕЩЁ РАЗ`, pad 8 + 64).

To make everything fit, `ЕЩЁ РАЗ` drops from 210 to **150** (it stays the only accent-filled
control), the card padding is 8, the card spacing is 16 (was 24) and the score band is forced to 200.
Card order top to bottom: reward, the `В меню | Темы` row, `ЕЩЁ РАЗ`.

**Fallbacks** (all decided once, when the screen is shown — §5.7):

| Case | What the player sees |
|---|---|
| (a) Not enough height for the MREC, even at the 48 floor | old layout: score and record **inside** the card, `ЕЩЁ РАЗ` 210, banner pinned at the bottom |
| (b) The run unlocked a theme | old layout with the `Открыта тема «…»` line inside the card; no MREC |
| (c) MREC not loaded at show time (offline, no consent, no fill) or ads off | old layout; a late load is ignored until the next showing |
| (d) Reward ad not loaded, or the continue was already used | reward button **hidden**, the card shrinks by **166** (150 + 16), the MREC stays 64 above it, the flex gap grows |
| (e) Banner not loaded at show time | no banner, no banner label, the card bottom edge sits `CardInset` 72 above the safe-area bottom |

### 15.5 Theme select

> **Planned:** seven cards (§18.3). The Minimal card, close up:
>
> ```
>       ┌────────────────────────┐   600 × 700
>       │      ╭─────┬─────╮     │   preview 360²: LEFT half = light palette,
>       │      │ ◎◎◎ │ ◎◎◎ │     │   RIGHT half = dark palette, each in its own colours
>       │      ╰─────┴─────╯     │   tap the left half → minimal-light
>       │                        │   tap the right half → minimal-dark
>       │        Минимал         │   label 52 · band 150 → 230 ↑
>       │        Светлая         │   caption 44 · band 60 → 130 ↑ · shows the CHOSEN variant
>       └────────────────────────┘   (Светлая / Тёмная); card fill = Accent of the chosen variant
> ```
>
> The wireframe below is the shipped six-card state.

```
┌──────────────────────────────────────────────┐   plate: theme bg @ 0.92
│ ↑ 64 padding                                 │
│                     Темы                     │   title 86 · band forced to 130
│                     ↕ 24                     │
│ ╌╌┬──────────────┬╌╌┬──────────────┬╌╌╌╌╌╌╌╌ │   carousel viewport 720 tall
│   │              │  │              │         │   card 600×700, gap 32
│   │    ╭────╮    │  │    ╭────╮    │         │
│   │    │ ◎◎ │    │  │    │ ◎◎ │    │         │   preview 360², ↕ 40 from the top
│   │    ╰────╯    │  │    ╰────╯    │         │   own theme colours, static
│   │              │  │              │         │
│   │     Неон     │  │   Пастель    │         │   label 52 · band 150 → 230 ↑
│   │              │  │              │         │
│   │              │  │  Рекорд 25   │         │   lock 44 · band 60 → 130 ↑
│   │              │  │              │         │
│ ╌╌┴──────────────┴╌╌┴──────────────┴╌╌╌╌╌╌╌╌ │
│        chosen            locked              │
│     Accent fill          α 0.45              │
│                     ↕ 24                     │
│  ┌────────────────────────────────────────┐  │
│  │                 Назад                  │  │   h 150 · surface
│  └────────────────────────────────────────┘  │
│ ↓ 64 padding                                 │
└──────────────────────────────────────────────┘
```

Six cards. Opening the screen jumps straight to the current theme; a
release snaps the nearest card to centre, eased by `1 − e^(−16 Δt)`.
Side padding is recomputed from the viewport so the first and last card
can still reach the centre.

**One card, close up — 600 × 700:**

```
      ┌────────────────────────┐  ─┐
      │                        │   │ 40
      │      ╭──────────╮      │  ─┘
      │      │  ◉ ◉ ◉   │      │   preview 360 × 360
      │      │    ●     │      │   concentric discs, bands =
      │      │  ◉ ◉ ◉   │      │   rings×2+1, scale 1 − i/bands
      │      ╰──────────╯      │   centre dot 14 % · ball colour
      │                        │
      │                        │
      │        Пастель         │   label 52 · 150 → 230 ↑ (inset 16)
      │                        │
      │       Рекорд 25        │   lock 44 · 60 → 130 ↑   (inset 16)
      │                        │
      └────────────────────────┘
```

| State | Fill | Label | Lock line |
|---|---|---|---|
| chosen | `Accent` | `AccentText`, α 1 | α 0 |
| unlocked | `Surface` | ink, α 1 | α 0 |
| locked | `Surface`, not interactable | ink, **α 0.45** | ink, **α 0.7** |

### 15.6 Settings

```
┌──────────────────────────────────────────────┐   plate: theme bg @ 0.95 · SCROLLS
│ ↑ 64 padding                                 │   spacing 16 · elastic · sens. 40
│                  Настройки                   │   title 86
│  Звук                      ┌────────────┐    │   switch · row h 150
│                            │ ▓▓▓▓▓▓   ◯ │    │   track 132×72 · knob 56
│                            └────────────┘    │   ON  = TrackOn (Accent)
│  Музыка                    ┌────────────┐    │
│                            │ ◯   ░░░░░░ │    │   OFF = TrackOff
│                            └────────────┘    │
│  Вибрация                  ┌────────────┐    │
│                            │ ▓▓▓▓▓▓   ◯ │    │
│                            └────────────┘    │
│ ↕ 28 spacer                                  │
│  Чувствительность  ├━━━━━━━●─────────────┤   │   slider 0.6 → 1.6, default 1.0
│                       fill  knob   track     │   track 420 · h 36 · knob 60
│  Инверсия вращения         ┌────────────┐    │
│                            │ ◯   ░░░░░░ │    │
│                            └────────────┘    │
│ ↕ 28 spacer                                  │
│  Снизить эффекты           ┌────────────┐    │
│                            │ ◯   ░░░░░░ │    │
│                            └────────────┘    │
│  Язык                      ┌──────────────┐  │   chip 320 × 150
│                            │     Авто     │  │   Авто → Русский → English
│                            └──────────────┘  │
│ ↕ 28 spacer                                  │
│       Параметры конфиденциальности           │   PLANNED · link, 150 · only while UMP requires it
│ ↕ 16                                         │
│        Об игре       ↔24 Сбросить прогресс   │   links: transparent fill, 44
│  ┌────────────────────────────────────────┐  │
│  │                 Назад                  │  │   h 150 · surface
│  └────────────────────────────────────────┘  │
│ ↓ 64 padding                                 │   content 1879 — scrolls on 4:3
└──────────────────────────────────────────────┘
```

Thirteen children, twelve 16-unit gaps. Content measures **1879** against
a 1920 reference, so it only just fits a 9:16 phone and genuinely scrolls
on anything squarer. *(Planned)* when the `Параметры конфиденциальности` row is
present (consent-required regions) the column has fourteen children, thirteen
gaps and measures **2045** — it scrolls on every aspect ratio. Without the row
(everywhere else) nothing changes.

### 15.7 Reset confirm and About — overlays on Settings  *(planned: About text)*

```
  RESET CONFIRM  (plate 0.97)        ABOUT  (plate 0.97, scrolls)
  ┌────────────────────┐        ┌────────────────────┐
  │                    │        │   Hypnotic Rings   │
  │   Сбросить рекорд  │        │                    │
  │     и настройки?   │        │       1.0.0        │
  │                    │        │                    │
  │ ┌────────────────┐ │        │  Игра показывает   │
  │ │      Нет       │ │        │рекламу и использует│
  │ └────────────────┘ │        │  интернет для её   │
  │                    │        │ загрузки. Без сети │
  │ ┌────────────────┐ │        │ игра работает, но  │
  │ │       Да       │ │        │    без рекламы.    │
  │ └────────────────┘ │        │                    │
  │                    │        │      Политика      │
  │                    │        │ конфиденциальности │
  │                    │        │ ┌────────────────┐ │
  │                    │        │ │     Назад      │ │
  │                    │        │ └────────────────┘ │
  └────────────────────┘        └────────────────────┘
```

`Нет` is listed **first** on purpose. Both overlays are forced hidden on
every `Show(state)` call, so leaving and returning always closes them.

About body text is `about.body` — the **planned replacement** in §12 (the shipped text claims
"no ads, no data, works offline" and becomes false the moment ads ship). The wireframe wraps at
the schematic width; at the real 952-unit text width it is about three lines, comfortably inside
the 340-unit band. The privacy-policy line stays a link to the policy hosted on the developer site.

## 16. Control anatomy

### Button

```
   ordinary                              primary
   ┌──────────────────────────────┐              ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
   │          Настройки           │   h 150      ┃            ИГРАТЬ            ┃   h 240
   └──────────────────────────────┘              ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
    fill   = Surface                      fill   = Accent
    label  = theme ink, 52                label  = AccentText, 52
    edge   = Outline, skin width          + halo when the theme glows

   link (About / Reset)                   chip (Language)
    ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄                   ┌──────────────┐
      Об игре          44                 │     Авто     │   320 × 150
    ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄                   └──────────────┘
    fill fully transparent                fill = Surface
```

### Toggle row — 150 tall

```
  ┌────────────────────────────────────────────┐
  │  Звук                    ┌──────────────┐  │      ON : TrackOn = Accent
  │                          │ ▓▓▓▓▓▓▓▓   ◯ │  │      track 132 × 72
  │                          └──────────────┘  │      knob 56 × 56, bg colour
  └────────────────────────────────────────────┘

  ┌────────────────────────────────────────────┐
  │  Музыка                  ┌──────────────┐  │      OFF : TrackOff
  │                          │ ◯   ░░░░░░░░ │  │      knob snaps to the
  │                          └──────────────┘  │      other end
  └────────────────────────────────────────────┘

  label 44, MiddleLeft, inset 32 from the left, stopping 180 from the right
  track pinned to the right edge at −32
  knob shape follows the skin: SQUARE on Monochrome, round everywhere else
```

### Slider row — 150 tall

```
  ┌────────────────────────────────────────────┐
  │  Чувствительность  ├━━━━━━●───────────┤    │
  └────────────────────────────────────────────┘
        44                 fill   knob    track
                         Accent    60   TrackOff

  track 420 wide + 2 × 32 end inset, 36 tall, right inset 32
  value 0.6 → 1.6, default 1.0
```

### Press feedback — one kind per theme

```
  SINK  (Monochrome, Pastel, Earth)   FLASH (Midnight, Neon, Ember)

    ┌──────────┐                        ┌──────────┐
    │  resting │                        │  resting │
    └──────────┘                        └──────────┘
         ↓ 7                                 ↓
    ┌──────────┐                        ┌──────────┐
    │ pressed  │  moves down 7          │░ pressed░│  fill lerps 35 %
    └──────────┘  springs back 0.10 s   └──────────┘  toward white

  RIPPLE (Aurora)
    ┌──────────────────┐
    │        ·         │   a disc grows from the exact touch point,
    │      ( ○ )       │   clipped by a RectMask2D,
    │     (  ●  )      │   radius 2 × |half-size + offset|,
    │      ( ○ )       │   alpha 0.30 fading to 0 over 0.10 s
    └──────────────────┘
```

### Corner radius on a 150-tall button

```
  Monochrome  0.10 → 15     ┌──────────────────┐   nearly square
  Ember       0.15 → 22.5   ╭──────────────────╮
  Earth       0.20 → 30     ╭──────────────────╮
  Midnight    0.25 → 37.5   ╭──────────────────╮
  Neon        0.30 → 45     ╭──────────────────╮
  Aurora      0.35 → 52.5   ╭──────────────────╮
  Pastel      0.50 → 75     (──────────────────)   a full pill, at the clamp

  radius = CornerRadius × control height, clamped to half the height;
  below 1 unit the sprite is dropped and the control is drawn square
```

## 17. Navigation  *(planned — consent, countdown and rewarded continue)*

```
LAUNCH
 └─ UMP consent  (SDK-owned form over the MENU, only where required; never blocks the Menu)
      ├─ resolved / not required ──▶ ads allowed for the session
      └─ declined / error / offline ──▶ ads off for the session, every slot collapsed,
                                         the game is fully playable

MENU
 ├── «ИГРАТЬ»     ──▶ COUNTDOWN ──▶ PLAYING   normal mode
 ├── «ХАРДКОР»    ──▶ COUNTDOWN ──▶ PLAYING   hardcore; the whole look turns Ember
 │                                            (locked until normal best ≥ 30)
 ├── «Темы»       ──▶ THEMES    ──«Назад»──▶ MENU
 └── «Настройки»  ──▶ SETTINGS  ──«Назад»──▶ MENU
                         ├── «Параметры конфиденциальности» ──▶ UMP privacy form (SDK)
                         │      (row exists only where the UMP SDK requires it)
                         ├── «Сбросить прогресс» ──▶ CONFIRM  (overlay)
                         └── «Об игре»           ──▶ ABOUT    (overlay)

COUNTDOWN   rings frozen · solid ring · 3 · 2 · 1 · ring dissolves ──▶ PLAYING
 (no pause button, no ads, input ignored)

PLAYING     (no ads)
 ├── pause button ──▶ PAUSED   (opaque plate · MREC + banner)
 │                     ├── «Продолжить» ──▶ PLAYING
 │                     └── «В меню»     ──▶ MENU
 │
 └── a ring reaches the red circle ──▶ GAMEOVER   (opaque plate · MREC + banner)
     (hardcore: a second touch              ├── «▶ Продолжить · реклама» ──▶ REWARDED AD
      on one ring also ends it)             │      ├── reward granted ──▶ COUNTDOWN (score + tempo kept) ──▶ PLAYING
                                            │      └── closed / failed ──▶ GAMEOVER (button gone)
                                            ├── «ЕЩЁ РАЗ» ──▶ COUNTDOWN (new run) ──▶ PLAYING
                                            ├── «В меню»  ──▶ MENU
                                            └── «Темы»    ──▶ THEMES
```

The Android back button is handled by `AndroidBackButton` and follows the
same `Back()` transition as the on-screen `Назад`. During COUNTDOWN and while
a rewarded ad is open, Back does nothing; while the UMP form is open, Back
belongs to the form. The banner and MREC are hidden while a rewarded ad is open.

**The look changes along the way:** entering a hardcore run swaps the
palette to **Ember** — world and UI together — and leaving the run swaps it
back to the chosen theme. Nothing reloads; the switch is live. A rewarded
continue inside hardcore keeps the Ember look.

## 18. Visual identity — fonts, themes, effects  *(planned)*

Everything visual that is not layout. Layout stays in §5 / §15, ads in §5.7. All numbers marked
*starting value* come from the approved previews and are tuned by the tests in §18.10, not by eye.

### 18.1 One style, many skins

**Identical in every theme:** fonts and sizes (§18.2), spacing and touch tokens (§4), screen structure
and layout (§5, §15), motion timings (§10), ring thickness 0.18 u, ball size, all strings.
**A theme may change only:** palette (§18.3), glow and shimmer, backdrop (§18.4), UI skin (§18.5).
A theme never changes physics or layout — the existing test (ring thickness, safe-area fit on every
aspect ratio) stays and now runs for every theme in §18.3.

### 18.2 Fonts

| Role | Font | Used for |
|---|---|---|
| `FontRole.Display` | **Unbounded Bold** (static) | score, countdown digit, screen titles, button labels, pause `II`, theme-card names |
| `FontRole.Text` | **Manrope Medium** (static) | body text, row labels, hints, lock lines, chips, link buttons, the `Реклама` label, About text |

- Both are SIL OFL 1.1, with Cyrillic and Latin. `UI.Text` cannot use variable fonts, so bundle
  **static instances** (the `static/` folder of each Google Fonts download):
  `Assets/_Project/Resources/Fonts/Unbounded-Bold.ttf`, `Manrope-Medium.ttf`, plus the two OFL
  licence texts next to them. About gets one credits line (`about.fonts`, §18.8).
- `FontRole { Display, Text }` lives in `HR.Core` (no `UnityEngine`); `UiFonts` in `HR.UI` maps a role to
  a `Font` loaded from `Resources`. If a file is missing, fall back to `LegacyRuntime.ttf` and log once.
- **Sizes and bands do not change.** The table in §3 (160 / 86 / 52 / 44, bands 232 / 125 / 76 / 64) and
  every forced band in §5 and §15 stay exactly as written, so the font swap moves nothing.
- Unbounded is wide, so **every label uses best-fit**: `resizeTextForBestFit = true`,
  `maxSize` = the token size, `minSize` = `round(0.75 × size)`. Horizontal wrap and vertical overflow
  stay as today.
- Strings are rendered as typed (`ИГРАТЬ`, `ХАРДКОР`, `ЕЩЁ РАЗ`, `НОВЫЙ РЕКОРД!`); no run-time upper-casing.
- `▶` and `·` in `over.continue` may not exist in either font; the OS fallback usually supplies them.
  If a device shows a hole, draw the triangle as a sprite (open item, §18.11).

### 18.3 Theme set

Seven cards in the carousel plus the hardcore skin: **Minimal** (light and dark, one card), **Neon
cosmos**, **Art deco**, **Sumi-e ink**, **Deep ocean**, **Bauhaus**, **Retro CRT**, and **Ember**
(hardcore only, unchanged). Nine palettes exist in code.

Ball outline is the Background colour in every theme (existing rule). Unless declared, `TrailHot` =
Ball and `TrailCool` = `RingColours[0]`; Ember keeps its own trail.

| Theme | Id | Background | Ring cycle | Ball | UI ink | DepthFade | Glow | Shimmer | Dark |
|---|---|---|---|---|---|---|---|---|---|
| **Minimal · light** | `minimal-light` | `#F7F7F5` | `#141414` | `#141414` | `#141414` | 0.55 | 0.00 | — | no |
| **Minimal · dark** | `minimal-dark` | `#0E0E10` | `#F2F2F0` | `#F2F2F0` | `#F2F2F0` | 0.55 | 0.00 | — | yes |
| **Neon cosmos** | `neon-cosmos` | `#05060F` | `#19F0FF`, `#FF2BD6`, `#8B5CFF` | `#FFFFFF` | `#E8F0FF` | 0.50 | 0.80 | 2.4 s, depth 0.10 | yes |
| **Art deco** | `art-deco` | `#0C1412` | `#D9B25F`, `#F0E2B6`, `#2E8B72` | `#FFF6DC` | `#F0E2B6` | 0.55 | 0.25 | — | yes |
| **Sumi-e ink** | `sumi-e` | `#F1EAD9` | `#1B1B1B`, `#4A4A4A`, `#6B6B6B` | `#D7381F` | `#1B1B1B` | 0.50 | 0.00 | — | no |
| **Deep ocean** | `deep-ocean` | `#03161F` | `#3DE8D0`, `#5AB8FF`, `#7A7CFF` | `#FFD6A5` | `#D6F5F2` | 0.55 | 0.45 | 3.6 s, depth 0.12 | yes |
| **Retro CRT** | `retro-crt` | `#020A04` | `#4DFF7A`, `#2BBF55`, `#8CFFA8` | `#FFB000` | `#B8FFC9` | 0.50 | 0.35 | — | yes |
| **Bauhaus** | `bauhaus` | `#F2E9DC` | `#D62828`, `#1E40AF`, `#111111` | `#111111` | `#111111` | 0.50 | 0.00 | — | no |
| **Ember** (hardcore) | `ember` | unchanged — §7.3 | | | | 0.50 | 0.70 | — | yes |

**Derived UI colours** (computed with the §7.2 rules; every theme resolved in the first step, so
Surface = 14 %, TrackOff = 22 %, Outline = 45 %; `AccentText` resolves to the Background everywhere):

| Theme | Surface | TrackOff | Outline | Accent | AccentText |
|---|---|---|---|---|---|
| Minimal · light | `#D7D7D6` | `#C5C5C4` | `#919190` | `#141414` | `#F7F7F5` |
| Minimal · dark | `#2E2E2F` | `#404041` | `#757575` | `#F2F2F0` | `#0E0E10` |
| Neon cosmos | `#252731` | `#373944` | `#6B6F7B` | `#19F0FF` | `#05060F` |
| Art deco | `#2C3129` | `#3E4136` | `#73715C` | `#D9B25F` | `#0C1412` |
| Sumi-e ink | `#D3CDBE` | `#C2BCAF` | `#918D84` | `#1B1B1B` | `#F1EAD9` |
| Deep ocean | `#21353D` | `#31474D` | `#627A7E` | `#3DE8D0` | `#03161F` |
| Retro CRT | `#1B2C20` | `#2A402F` | `#54785D` | `#4DFF7A` | `#020A04` |
| Bauhaus | `#D2CBC0` | `#C0B9AF` | `#8D8881` | `#D62828` | `#F2E9DC` |
| Ember | `#352520` | `#483730` | `#7E6A5E` | `#FF7A1A` | `#140604` |

**Contrast against the Background** (WCAG; the required ratio stays **3.0**) — all pass:

| Theme | Ring cycle | Ball | UI ink |
|---|---|---|---|
| Minimal · light | 17.17 | 17.17 | 17.17 |
| Minimal · dark | 17.20 | 17.20 | 17.20 |
| Neon cosmos | 14.38, 6.32, 4.88 | 20.20 | 17.64 |
| Art deco | 9.32, 14.46, **4.49** | 17.31 | 14.46 |
| Sumi-e ink | 14.36, 7.39, **4.44** | **3.91** | 14.36 |
| Deep ocean | 11.99, 8.55, 5.41 | 13.54 | 15.99 |
| Retro CRT | 15.18, 8.29, 16.21 | 10.94 | 17.36 |
| Bauhaus | **4.16**, 7.25, 15.70 | 15.70 | 15.70 |
| Ember | 7.61, 10.88, 4.97 | 18.15 | 16.28 |

Closest to the floor: Sumi-e ball (3.91), Bauhaus red ring (4.16), Sumi-e third ring (4.44), Art deco
emerald ring (4.49). Darkening a background or lightening one of these trips the test first.

**Carousel order** (by unlock): Minimal, Neon cosmos, Art deco, Sumi-e ink, Deep ocean, Bauhaus,
Retro CRT. `ThemePalette.All` lists the seven carousel themes (Minimal as one entry with two
variants); `ThemePalette.Every` lists all nine palettes.

### 18.4 Backdrops

A **backdrop** is a decorative layer drawn **behind the rings and the ball**, above the Background colour,
screen-anchored (the camera is static). It is visible through the Menu plate (alpha 0.88) and during
Countdown and Playing; the opaque Pause / Game over plates hide it. All sizes are fractions of the
screen height *H* so they scale; positions come from a fixed seed per theme (identical every run).

| Theme | Elements (starting values) | Motion |
|---|---|---|
| Minimal | none | — |
| Neon cosmos | nebula: two flat discs, radius 0.34 H (`#8B5CFF` α 0.13, lower left) and 0.29 H (`#19F0FF` α 0.07, right). Stars: 46 dots, radius 0.0025–0.0046 H, white α 0.3–0.9 | stars twinkle α ±0.2, each with its own period ≥ 2.4 s; nebula static |
| Art deco | sunburst: 12 wedges (every other 7.5° slice) from the tunnel centre, `#D9B25F` α 0.06. Frame: outline with notched corners, inset 3 % of width, `#D9B25F` α 0.5, 1 unit | static |
| Sumi-e ink | enso: open ring, radius 0.42 H, width 0.057 H, 80 % of the circumference, `#1B1B1B` α 0.05. Paper grain: 70 specks, radius 0.002–0.004 H, `#3A2F20` α 0.08–0.20. Seal: 22-unit square `#D7381F` with a cream inner outline, bottom right | static |
| Deep ocean | three light shafts from the top edge, `#5AB8FF` α 0.05 / 0.05 / 0.04. 16 bubbles: hollow circles, radius 0.007–0.018 H, stroke `#9FFFF0` α 0.4 | bubbles rise 3 % H/s and wrap |
| Retro CRT | scanlines every 0.0143 H, 0.005 H thick, black α 0.28. Vignette: black frame 0.085 H wide α 0.35 plus 0.029 H α 0.30 | static — **no flicker, no roll, no aberration** |
| Bauhaus | yellow disc `#F2B705`, radius 0.16 H, bottom right, α ≤ 0.55. Blue wash triangle `#1E40AF`, bottom left, α ≤ 0.15. Flat, hard edges | static |
| Ember (hardcore) | 22 sparks, radius 0.003–0.007 H, `#FFB02E` α 0.2–0.8 (brighter toward the bottom) | sparks rise 4 % H/s and wrap |

Rules for every backdrop:

1. **Slow or still.** Drift ≤ 4 % H/s; any periodic change ≥ 2.4 s; nothing flashes, flickers or changes
   brightness at 3 Hz or faster (photosensitivity). This is stricter than the 1.2 s shimmer rule in §13.
2. **Never above gameplay.** Order: Background, backdrop, rings, ball, trail, HUD.
3. **Never costs ring readability.** At its maximum alpha, each backdrop colour blended over the Background
   must keep **every ring colour at ≥ 3.0**. Checked: Bauhaus yellow at α 0.55 gives 3.29 for the red ring,
   the blue wash at α 0.15 stays above 3.1. If the test fails, lower the alpha — never the ring colour.
4. **Procedural, no texture assets.** Each backdrop is one mesh / sprite batch, ≤ 2 draw calls, generated at load.
5. ***Reduce effects* removes the backdrop completely** (flat Background only — scanlines and shapes
   included), together with the effects already listed in §10.
6. The hardcore skin keeps its own backdrop; leaving the run restores the chosen theme's backdrop with the palette.

### 18.5 UI skin per theme

Corner radius is a fraction of the control's height, clamped to half of it (existing rule). Two new
properties: **Chamfer** (corner cut at 45°, size = fraction × height, new procedurally generated 9-slice
sprite `HR.Chamfer`) and **HardShadow** (a solid Ink-colour copy of the control offset right and down,
drawn behind it; used by buttons, chips and theme cards, not by toggles or sliders; on press the control
moves onto its shadow by the same offset).

| Theme | Corner / shape | Outline | Press | Knob | Extra | Halo reach / alpha |
|---|---|---|---|---|---|---|
| Minimal · light | 0.20 round | 2.0 | Sink | Round | — | — |
| Minimal · dark | 0.20 round | 2.0 | **Flash** | Round | — | — |
| Neon cosmos | 0.30 round | 4.0 | Flash | Round | — | 7.2 / 0.30 |
| Art deco | **0.18 chamfer** | 3.0 | Flash | **Square** | gold outline | 2.25 / 0.10 |
| Sumi-e ink | 0.10 round | 3.0 | Sink | **Square** | — | — |
| Deep ocean | 0.37 round | 2.0 | **Ripple** | Round | — | 4.05 / 0.17 |
| Retro CRT | 0.06 round | 3.0 | Flash | **Square** | — | 3.15 / 0.13 |
| Bauhaus | **0** square | 3.0 | Sink (by the shadow offset) | **Square** | **HardShadow 6** | — |
| Ember (hardcore) | 0.15 round | 3.0 | Flash | Round | unchanged | 6.3 / 0.27 |

Resolved on a 150-tall button: Minimal 30, Neon 45, Art deco chamfer 27, Sumi-e 15, Deep ocean 55.5,
Retro CRT 9, Bauhaus 0, Ember 22.5. Halo, press recovery (0.10 s) and ripple rules are as in §8.
Breathing (`UiShimmer`): Neon cosmos 2.4 s / 0.10 and Deep ocean 3.6 s / 0.12; nothing faster than 1.2 s.
The press rule stays: **dark palettes never Sink, light palettes never Flash** (hence Minimal's two press types).

### 18.6 Minimal — one theme, light and dark

- One carousel card (§15.5). The preview circle is split down the middle: left half drawn with the
  light palette, right half with the dark palette. Tapping a half chooses that variant; the caption under
  the name shows the chosen word (`Светлая` / `Тёмная`).
- Persisted as the theme id `minimal-light` or `minimal-dark`. The default on a fresh install is
  **`minimal-light`** (no system dark-mode detection in v1).
- Both variants are always unlocked.

### 18.7 Unlocks and migration

| Theme | Required best score |
|---|---|
| Minimal (both variants), Neon cosmos | 0 — available from the start |
| Art deco | **25** |
| Sumi-e ink | **50** |
| Deep ocean | **75** |
| Bauhaus | **100** |
| Retro CRT | **150** |
| Ember | never by score — the hardcore skin |

`ScoreKeeper` thresholds change accordingly; hardcore (best ≥ 30), its separate record and "awards no
themes" are untouched. **Saved `chosenTheme` migration** (`ThemeMigration` in `HR.Core`, runs once on
load, mapped so the new theme is always already unlocked): `monochrome` → `minimal-light`, `midnight` →
`minimal-dark`, `neon` → `neon-cosmos`, `pastel` → `art-deco` (25 → 25), `earth` → `sumi-e` (50 → 50),
`aurora` → `deep-ocean` (100 → 75). An id that is neither new nor legacy still **throws**.

### 18.8 Strings

Replace the `theme.monochrome`, `theme.midnight`, `theme.neon`, `theme.pastel`, `theme.earth` and
`theme.aurora` rows with these (the existing completeness / `{0}` test covers them):

| Key | Russian | English |
|---|---|---|
| `theme.minimal` | Минимал | Minimal |
| `theme.light` | Светлая | Light |
| `theme.dark` | Тёмная | Dark |
| `theme.neon` | Неон-космос | Neon cosmos |
| `theme.deco` | Арт-деко | Art deco |
| `theme.sumie` | Суми-э | Sumi-e |
| `theme.ocean` | Глубина | Deep ocean |
| `theme.bauhaus` | Баухаус | Bauhaus |
| `theme.crt` | Ретро ЭЛТ | Retro CRT |
| `theme.ember` | Жар | Ember (never shown in the carousel; no unlock line) |
| `about.fonts` | Шрифты: Unbounded и Manrope (SIL OFL 1.1) | Fonts: Unbounded and Manrope (SIL OFL 1.1) |

The `Открыта тема «…»` line uses these names. `theme.locked` (`Рекорд {0}`) is unchanged.

### 18.9 Files to change (checklist)

| File | Change |
|---|---|
| `Core/ThemePalette.cs` | nine palettes from §18.3, `IsDark`, `BackdropKind`, `All` (7) / `Every` (9), ids from §18.3 |
| `Core/UiSkin.cs` | skins from §18.5, new `Chamfer` and `HardShadow` fields |
| `Core/ScoreKeeper.cs` | thresholds from §18.7 |
| `Core/ThemeMigration.cs` *(new)* | legacy-id map from §18.7 |
| `Core/Strings.cs` | rows from §18.8 |
| `UI/UiPainter.cs`, `UI/UiControls.cs` | chamfer sprite, hard shadow, press-by-shadow-offset |
| `UI/UiBuilder.cs`, `UI/UiFonts.cs` *(new)* | font roles, best-fit on every label |
| `UI/GameUi.Screens.cs` | seven cards, split Minimal card, About credits line |
| `UI/Backdrop*.cs` *(new)* | procedural backdrops from §18.4, removed by *Reduce effects* |
| `Resources/Fonts/` *(new)* | the two TTFs and licence texts |
| `Tests/` | §18.10 |

### 18.10 Tests (planned — replace the theme items in §13)

- Every palette defines every entry; every ring colour, the ball and the UI ink clear **3:1** against the
  Background (§18.3 numbers are the expected minimums); backdrop blend test from §18.4 rule 3.
- A corner radius never exceeds half a control's height; the chamfer cut never exceeds it either.
- **Dark palettes must not Sink** (Minimal dark, Neon cosmos, Art deco, Deep ocean, Retro CRT, Ember);
  **light palettes must Sink** (Minimal light, Sumi-e ink, Bauhaus); Deep ocean ripples.
- The seven carousel themes use more than three distinct corner shapes; Art deco and Bauhaus are the only
  non-round ones.
- No backdrop drifts faster than 4 % H/s, nothing changes brightness faster than every 2.4 s, no
  breathing faster than 1.2 s.
- *Reduce effects* leaves no backdrop, halo, breathing, trail, flash, shake or collapse.
- Theme id migration: every legacy id maps to an unlocked theme at the old threshold; an unknown id throws.
- Fonts: both load; each covers Cyrillic, Latin, digits and `«»` (`▶ ·` may come from the OS fallback);
  the longest string of every label role fits at ≥ its best-fit minimum at the narrowest supported width
  (candidates: `Параметры конфиденциальности`, `Сбросить прогресс`, `Рекорд в обычной игре 30`,
  `▶ Продолжить · реклама`, `Hypnotic Rings`, the longest `Открыта тема «…»`).
- Every theme paints every screen without a hole; no label overlaps another and nothing leaves the safe area on
  any aspect ratio — now with the real fonts.
- A theme cannot change ring thickness; hardcore's look never appears in the carousel.

### 18.11 Open items (do not block the first build)

- **Best-fit minimum 0.75** is a starting value; the font test may require 0.7 for Unbounded in German-length
  English strings.
- **`▶` glyph** on devices whose fallback font lacks it: swap for a small sprite.
- **Backdrop alphas and counts** are starting values taken from the previews; the contrast test decides.
- **Default Minimal variant** is light; following the system dark mode would need a native call (later).

---

*Generated 5 November 2026 from the sources listed in §1; planned sections added the same day.
Revision 2: reconciled with the ads plan. Revision 3: visual identity (§18).*
