#!/usr/bin/env python3
"""Tiles the captured screens into one sheet, so all of them can be looked at together.

Thirty files get reviewed one at a time or not at all, and "not at all" is what
actually happens. Side by side, a screen that is wrong on one shape and right on
the other four stops being something you have to remember to check.
"""

import os
import sys
from PIL import Image, ImageDraw

SRC = "docs/publishing/art/screenshots/screens"
OUT = "docs/publishing/art/screenshots/contact-sheet.png"

# Ordered as a player meets them; anything captured but not listed is appended, so a new
# screen shows up on the sheet without anyone remembering to add it here.
PREFERRED = ["menu", "hud", "pause", "gameover", "themes", "settings", "about", "confirm-reset"]
SHAPES = ["9x16", "9x18", "9x20", "9x21", "3x4"]

CELL_H = 420
PAD = 14
LABEL = 26


def discover():
    names = {f.rsplit("-", 1)[0] for f in os.listdir(SRC) if f.endswith(".png")}
    ordered = [n for n in PREFERRED if n in names]
    return ordered + sorted(names - set(ordered))


def main():
    screens = discover()
    cells = {}
    tallest = 0
    for screen in screens:
        for shape in SHAPES:
            path = os.path.join(SRC, f"{screen}-{shape}.png")
            if not os.path.exists(path):
                print(f"missing {path}")
                return 1
            image = Image.open(path).convert("RGB")
            scale = CELL_H / image.height
            image = image.resize((max(1, int(image.width * scale)), CELL_H), Image.LANCZOS)
            cells[(screen, shape)] = image
            tallest = max(tallest, image.width)

    width = PAD + len(SHAPES) * (tallest + PAD)
    height = PAD + len(screens) * (CELL_H + LABEL + PAD)
    sheet = Image.new("RGB", (width, height), (24, 24, 27))
    draw = ImageDraw.Draw(sheet)

    for row, screen in enumerate(screens):
        for col, shape in enumerate(SHAPES):
            image = cells[(screen, shape)]
            x = PAD + col * (tallest + PAD) + (tallest - image.width) // 2
            y = PAD + row * (CELL_H + LABEL + PAD)
            sheet.paste(image, (x, y))
            draw.text((x, y + CELL_H + 6), f"{screen} · {shape}", fill=(170, 170, 178))

    sheet.save(OUT)
    print(f"  {OUT}  {sheet.width}x{sheet.height}  "
          f"{os.path.getsize(OUT) / 1024:.0f} KiB  ({len(cells)} frames)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
