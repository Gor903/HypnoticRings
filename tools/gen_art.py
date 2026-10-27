#!/usr/bin/env python3
"""Generates the icon set from the game's own geometry.

The icon is the tunnel: concentric arcs with gaps in them and the ball resting
on the bottom of one. Drawing it from the same description the game draws from
means it cannot drift away from what the player sees, and it means a change to
the palette is a change to one tuple rather than a trip through an image editor.

Midnight rather than the default Monochrome: on a launcher full of bright
squares, a dark navy tile with one amber dot is the thing that gets noticed,
and the icon is the only art that competes for attention before install.

Everything is drawn at four times size and reduced, which is the cheapest
anti-aliasing there is and the reason the arcs do not come out with stepped
edges at 48 dp.
"""

import math
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Generated")
STORE_DIR = os.path.join(ROOT, "docs", "publishing", "art")

SUPERSAMPLE = 4

# ThemePalette.Midnight, kept in step with Assets/_Project/Scripts/Core/ThemePalette.cs.
BACKGROUND = (0x0B, 0x10, 0x20)
RING = (0xE8, 0xEC, 0xF4)
BALL = (0xFF, 0xB7, 0x03)

# Radius as a fraction of the half-size, the gap's centre in degrees, and its width.
RINGS = [
    (0.92, 90.0, 54.0),
    (0.70, 215.0, 60.0),
    (0.48, 340.0, 66.0),
]
RING_THICKNESS = 0.085
BALL_RADIUS = 0.10
BALL_AT = 0.29


def draw_tunnel(size, margin=0.0, background=None):
    """One tile of tunnel. margin shrinks the art inside the canvas."""
    big = size * SUPERSAMPLE
    mode = "RGB" if background else "RGBA"
    image = Image.new(mode, (big, big), background or (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    centre = big / 2.0
    unit = (big / 2.0) * (1.0 - margin)
    thickness = max(1, int(unit * RING_THICKNESS))

    for fraction, gap_centre, gap_width in RINGS:
        radius = unit * fraction
        box = [centre - radius, centre - radius, centre + radius, centre + radius]

        # Pillow measures arcs clockwise from three o'clock; the gap is simply the
        # stretch that is not drawn, so the arc runs from one edge of it to the other.
        start = gap_centre + gap_width / 2.0
        end = gap_centre - gap_width / 2.0 + 360.0
        draw.arc(box, start=start, end=end, fill=RING, width=thickness)

    ball = unit * BALL_RADIUS
    cy = centre + unit * BALL_AT
    draw.ellipse([centre - ball, cy - ball, centre + ball, cy + ball], fill=BALL)

    return image.resize((size, size), Image.LANCZOS)


def write(image, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    return f"{os.path.relpath(path, ROOT):<56} {image.size[0]}x{image.size[1]:<5}  " \
           f"{os.path.getsize(path) / 1024:6.1f} KiB"


def feature_graphic(width=1024, height=500):
    """The 1024x500 banner the store shows above the listing."""
    big_w, big_h = width * SUPERSAMPLE, height * SUPERSAMPLE
    image = Image.new("RGB", (big_w, big_h), BACKGROUND)
    draw = ImageDraw.Draw(image)

    # Two tunnels running off the edges: the banner is wide, and one centred circle
    # in the middle of it reads as a logo floating in a lot of empty navy.
    for centre_x, scale in ((big_w * 0.22, 1.25), (big_w * 0.78, 0.85)):
        unit = big_h / 2.0 * scale
        thickness = max(1, int(unit * RING_THICKNESS))
        for fraction, gap_centre, gap_width in RINGS:
            radius = unit * fraction
            box = [centre_x - radius, big_h / 2 - radius, centre_x + radius, big_h / 2 + radius]
            draw.arc(box, start=gap_centre + gap_width / 2.0,
                     end=gap_centre - gap_width / 2.0 + 360.0, fill=RING, width=thickness)

        ball = unit * BALL_RADIUS
        cy = big_h / 2 + unit * BALL_AT
        draw.ellipse([centre_x - ball, cy - ball, centre_x + ball, cy + ball], fill=BALL)

    return image.resize((width, height), Image.LANCZOS)


def verify(path, expected_size, expect_alpha, flat_is_fine=False):
    """Reads the file back and checks what actually landed on disk."""
    with Image.open(path) as image:
        problems = []
        if image.size != expected_size:
            problems.append(f"size {image.size}, expected {expected_size}")

        has_alpha = image.mode in ("RGBA", "LA")
        if expect_alpha and not has_alpha:
            problems.append(f"mode {image.mode}, expected transparency")
        if not expect_alpha and has_alpha:
            problems.append(f"mode {image.mode}; the store rejects alpha in an icon")

        # A flat tile normally means the drawing silently produced nothing — the one
        # exception is the adaptive background, which is a flat colour by definition.
        if not flat_is_fine:
            colours = image.convert("RGB").getcolors(maxcolors=1 << 20)
            if colours is not None and len(colours) < 3:
                problems.append(f"only {len(colours)} colour(s) — nothing was drawn")

        return problems


def main():
    jobs = []

    # Unity's adaptive icon: the foreground is transparent and must keep its art
    # inside the middle two thirds, because the launcher masks and moves the rest.
    jobs.append((draw_tunnel(1024, margin=0.06, background=BACKGROUND),
                 os.path.join(OUT_DIR, "icon_1024.png"), (1024, 1024), False, False))
    jobs.append((draw_tunnel(432, margin=0.34),
                 os.path.join(OUT_DIR, "icon_fg.png"), (432, 432), True, False))
    jobs.append((Image.new("RGB", (432, 432), BACKGROUND),
                 os.path.join(OUT_DIR, "icon_bg.png"), (432, 432), False, True))

    # The store wants 512 for the listing icon and 1024x500 for the banner.
    jobs.append((draw_tunnel(512, margin=0.06, background=BACKGROUND),
                 os.path.join(STORE_DIR, "store-icon-512.png"), (512, 512), False, False))
    jobs.append((feature_graphic(),
                 os.path.join(STORE_DIR, "feature-graphic-1024x500.png"), (1024, 500), False, False))

    failures = 0
    for image, path, expected, alpha, flat_ok in jobs:
        line = write(image, path)
        problems = verify(path, expected, alpha, flat_is_fine=flat_ok)
        print(("  FAIL " if problems else "  ok   ") + line)
        for problem in problems:
            print(f"        {problem}")
            failures += 1

    print("\nart: clean" if not failures else f"\nart: {failures} problem(s)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
