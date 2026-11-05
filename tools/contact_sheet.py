#!/usr/bin/env python3
"""Tiles captured frames into one sheet, so all of them can be looked at together.

Thirty files get reviewed one at a time or not at all, and "not at all" is what
actually happens. Side by side, a screen that is wrong on one shape and right on
the other four stops being something you have to remember to check.

Two sheets, because there are two things worth comparing and they are different
questions. The shapes sheet asks "does this screen survive every phone we ship
on"; the skins sheet asks "do the six themes look like six places". Laid out the
same way they would answer neither.

    tools/contact_sheet.py            both sheets
    tools/contact_sheet.py shapes     one screen per row, one phone per column
    tools/contact_sheet.py skins      one theme per row, one screen per column
"""

import os
import sys
from PIL import Image, ImageDraw

# Outputs live outside the project — see HR.Core.ArtifactPaths for why, and for the one
# answer the shell and the editor tooling share.
ROOT = os.environ.get("HR_ARTIFACTS") or os.path.join(
    os.path.expanduser("~"), "Desktop", "game-builds", "HypnoticRings")
ROOT = os.path.join(ROOT, "screenshots")

# Ordered as a player meets them; anything captured but not listed is appended, so a new
# screen shows up on the sheet without anyone remembering to add it here.
SCREENS = ["menu", "hud", "pause", "gameover", "themes", "settings", "about", "confirm-reset"]
SHAPES = ["9x16", "9x18", "9x20", "9x21", "3x4"]

# Light, dark, light, dark — so the sheet does not read as two unrelated halves.
THEMES = ["MinimalLight", "MinimalDark", "Neon", "Deco", "Sumie", "Ocean", "Bauhaus", "Crt"]
SKIN_SCREENS = ["menu", "settings", "gameover"]

CELL_H = 420
PAD = 14
LABEL = 26


def discover(src, preferred):
    """Row names present on disk, in the order a player meets them."""
    names = {f.rsplit("-", 1)[0] for f in os.listdir(src) if f.endswith(".png")}
    ordered = [n for n in preferred if n in names]
    return ordered + sorted(names - set(ordered))


def sheet(src, out, rows, cols, path_for, label_for):
    cells = {}
    widest = 0
    for row in rows:
        for col in cols:
            path = os.path.join(src, path_for(row, col))
            if not os.path.exists(path):
                print(f"missing {path}")
                return 1

            image = Image.open(path).convert("RGB")
            scale = CELL_H / image.height
            image = image.resize((max(1, int(image.width * scale)), CELL_H), Image.LANCZOS)
            cells[(row, col)] = image
            widest = max(widest, image.width)

    width = PAD + len(cols) * (widest + PAD)
    height = PAD + len(rows) * (CELL_H + LABEL + PAD)
    page = Image.new("RGB", (width, height), (24, 24, 27))
    draw = ImageDraw.Draw(page)

    for r, row in enumerate(rows):
        for c, col in enumerate(cols):
            image = cells[(row, col)]
            x = PAD + c * (widest + PAD) + (widest - image.width) // 2
            y = PAD + r * (CELL_H + LABEL + PAD)
            page.paste(image, (x, y))
            draw.text((x, y + CELL_H + 6), label_for(row, col), fill=(170, 170, 178))

    page.save(out)
    print(f"  {out}  {page.width}x{page.height}  "
          f"{os.path.getsize(out) / 1024:.0f} KiB  ({len(cells)} frames)")
    return 0


def shapes():
    src = os.path.join(ROOT, "screens")
    return sheet(src, os.path.join(ROOT, "contact-sheet.png"),
                 discover(src, SCREENS), SHAPES,
                 lambda screen, shape: f"{screen}-{shape}.png",
                 lambda screen, shape: f"{screen} · {shape}")


def skins():
    src = os.path.join(ROOT, "skins")
    if not os.path.isdir(src):
        print(f"missing {src}")
        return 1

    return sheet(src, os.path.join(ROOT, "skin-sheet.png"), THEMES, SKIN_SCREENS,
                 lambda theme, screen: f"{theme}-{screen}-9x16.png",
                 lambda theme, screen: f"{theme} · {screen}")


def main():
    wanted = sys.argv[1] if len(sys.argv) > 1 else "both"
    if wanted == "shapes":
        return shapes()
    if wanted == "skins":
        return skins()

    return shapes() or skins()


if __name__ == "__main__":
    sys.exit(main())
